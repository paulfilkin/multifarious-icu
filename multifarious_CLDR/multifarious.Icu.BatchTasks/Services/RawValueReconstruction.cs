using System;
using System.Collections.Generic;
using System.Text;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>What a paragraph's content amounts to as raw native text.</summary>
    public sealed class ReconstructedValue
    {
        public ReconstructedValue(string rawValue, bool usedFilterTags)
            : this(rawValue, usedFilterTags, null)
        {
        }

        public ReconstructedValue(string rawValue, bool usedFilterTags, IReadOnlyList<CommentRange> comments)
        {
            RawValue = rawValue;
            UsedFilterTags = usedFilterTags;
            Comments = comments ?? new List<CommentRange>();
        }

        /// <summary>
        /// Every comment marker met on the walk, in document order, with the range of
        /// <see cref="RawValue"/> its content amounts to. A marker inside another is listed
        /// after it, and a marker wrapping nothing has an empty range.
        /// </summary>
        public IReadOnlyList<CommentRange> Comments { get; }

        /// <summary>The native value: text and tag content concatenated in document order.</summary>
        public string RawValue { get; }

        /// <summary>
        /// True where the filter had fragmented the value into its own tags, under a
        /// configuration that tags embedded content. The expansion keeps those tags through
        /// <see cref="RawValueReconstruction.Encode"/>; this projection folds their content in
        /// so the text reads as the native file.
        /// </summary>
        public bool UsedFilterTags { get; }
    }

    /// <summary>A comment marker's comments and the half-open range of raw text it wrapped.</summary>
    public sealed class CommentRange
    {
        public CommentRange(ICommentProperties comments, int start, int end)
        {
            if (comments == null) throw new ArgumentNullException(nameof(comments));
            Comments = comments;
            Start = start;
            End = end;
        }

        public ICommentProperties Comments { get; }

        public int Start { get; }

        public int End { get; }

        /// <summary>True where the range shares at least one character with [start, end).</summary>
        public bool Overlaps(int start, int end)
        {
            return Start < end && End > start;
        }
    }

    /// <summary>What a sentinel character stands for.</summary>
    public enum SentinelKind
    {
        /// <summary>The start tag of a tag pair.</summary>
        PairStart,

        /// <summary>The end tag of a tag pair.</summary>
        PairEnd,

        /// <summary>A placeholder or structure tag, whole in itself.</summary>
        Placeholder,
    }

    /// <summary>One filter tag as a sentinel character in the encoded value, with the tag it stands for.</summary>
    public sealed class TagSentinel
    {
        public TagSentinel(char character, SentinelKind kind, IAbstractMarkupData tag, int pairIndex, string text)
        {
            Character = character;
            Kind = kind;
            Tag = tag;
            PairIndex = pairIndex;
            Text = text ?? string.Empty;
        }

        public char Character { get; }

        public SentinelKind Kind { get; }

        /// <summary>The tag in the paragraph: an <see cref="ITagPair"/> for either half of a pair, else the placeholder or structure tag.</summary>
        public IAbstractMarkupData Tag { get; }

        /// <summary>Which pair the start and end belong to, numbered in document order; -1 for a placeholder.</summary>
        public int PairIndex { get; }

        /// <summary>The tag's native text, for the readable projection.</summary>
        public string Text { get; }
    }

    /// <summary>
    /// A paragraph's value with every filter tag replaced by one private-use character, so the
    /// ICU parser sees the tags as text, hoisting carries them into the forms with the text
    /// around them, and the writer can put a clone of each back where its character lands.
    /// </summary>
    public sealed class EncodedValue
    {
        private readonly Dictionary<char, TagSentinel> _byCharacter = new Dictionary<char, TagSentinel>();

        public EncodedValue(string rawValue, IReadOnlyList<CommentRange> comments, IReadOnlyList<TagSentinel> sentinels)
        {
            RawValue = rawValue ?? string.Empty;
            Comments = comments ?? new List<CommentRange>();
            Sentinels = sentinels ?? new List<TagSentinel>();
            foreach (var sentinel in Sentinels)
            {
                _byCharacter[sentinel.Character] = sentinel;
            }
        }

        /// <summary>The value with a sentinel character for each tag.</summary>
        public string RawValue { get; }

        /// <summary>The comment markers' ranges, as offsets into <see cref="RawValue"/>.</summary>
        public IReadOnlyList<CommentRange> Comments { get; }

        /// <summary>The sentinels in document order.</summary>
        public IReadOnlyList<TagSentinel> Sentinels { get; }

        public bool HasTags
        {
            get { return Sentinels.Count > 0; }
        }

        /// <summary>The sentinel a character stands for, or null for an ordinary character.</summary>
        public TagSentinel SentinelOf(char character)
        {
            TagSentinel sentinel;
            return _byCharacter.TryGetValue(character, out sentinel) ? sentinel : null;
        }

        /// <summary>The readable form of an encoded text: each sentinel replaced by its tag's native text.</summary>
        public string Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded) || Sentinels.Count == 0) return encoded ?? string.Empty;

            var builder = new StringBuilder(encoded.Length);
            foreach (var character in encoded)
            {
                var sentinel = SentinelOf(character);
                if (sentinel == null) builder.Append(character);
                else builder.Append(sentinel.Text);
            }
            return builder.ToString();
        }
    }

    /// <summary>
    /// Rebuilds the raw native value of a paragraph: text runs and tag content concatenated in
    /// document order, straight through segments, comment markers, revision markers and locked
    /// content, none of which has native text of its own.
    ///
    /// Two things make this more than a join. Studio's segmentation splits a message at sentence
    /// punctuation inside its branches, so a value arrives as several segments with text runs
    /// between them, cut mid-syntax; only the whole paragraph is the value. And under a
    /// configuration that tags embedded content the filter's tag content is folded back in, so
    /// the ICU parser sees what the native file carried. Under the default JSON and Java
    /// Resources configurations the value is plain text (Phase 0 evidence).
    ///
    /// The same walk over an expanded paragraph yields its native projection: the text Studio's
    /// writer will emit, which is what the restore checks parse.
    ///
    /// <see cref="Encode"/> is the second mode, for the tasks' own rewriting: each filter tag
    /// becomes one private-use character instead of its text, so the tags survive the layout
    /// as tags. Project 56 (12 October 2026) showed why: the HTML embedded-content writer
    /// escapes text, so markup folded to text came out as entities, and the translator was
    /// handed raw markup inside the forms either way.
    /// </summary>
    public static class RawValueReconstruction
    {
        private const char PrivateUseFirst = '';
        private const char PrivateUseLast = '';

        public static ReconstructedValue Reconstruct(IAbstractMarkupDataContainer paragraph)
        {
            if (paragraph == null) throw new ArgumentNullException(nameof(paragraph));

            var builder = new StringBuilder();
            var comments = new List<CommentRange>();
            var walk = new Walk(builder, comments, new FoldingTexts());
            walk.Append(paragraph);
            return new ReconstructedValue(builder.ToString(), walk.UsedTags, comments);
        }

        /// <summary>
        /// The paragraph's value with each filter tag as a sentinel character. The characters
        /// are taken from the private-use area, skipping any the paragraph's own text already
        /// uses, so a character in the result is a tag if and only if the table says so.
        /// </summary>
        public static EncodedValue Encode(IAbstractMarkupDataContainer paragraph)
        {
            if (paragraph == null) throw new ArgumentNullException(nameof(paragraph));

            var inUse = new HashSet<char>();
            foreach (var character in Reconstruct(paragraph).RawValue)
            {
                if (character >= PrivateUseFirst && character <= PrivateUseLast) inUse.Add(character);
            }

            var builder = new StringBuilder();
            var comments = new List<CommentRange>();
            var texts = new SentinelTexts(inUse);
            new Walk(builder, comments, texts).Append(paragraph);
            return new EncodedValue(builder.ToString(), comments, texts.Sentinels);
        }

        /// <summary>What a tag contributes to the value: its native text, or a sentinel for it.</summary>
        private interface ITagTexts
        {
            string Start(ITagPair pair);

            string End(ITagPair pair);

            string Placeholder(IPlaceholderTag tag);

            string Structure(IStructureTag tag);
        }

        private sealed class FoldingTexts : ITagTexts
        {
            public string Start(ITagPair pair)
            {
                return pair.StartTagProperties.TagContent;
            }

            public string End(ITagPair pair)
            {
                return pair.EndTagProperties.TagContent;
            }

            public string Placeholder(IPlaceholderTag tag)
            {
                return tag.Properties.TagContent;
            }

            public string Structure(IStructureTag tag)
            {
                return tag.Properties.TagContent;
            }
        }

        private sealed class SentinelTexts : ITagTexts
        {
            private readonly HashSet<char> _inUse;
            private readonly List<TagSentinel> _sentinels = new List<TagSentinel>();
            private char _next = PrivateUseFirst;
            private int _pairs;

            public SentinelTexts(HashSet<char> inUse)
            {
                _inUse = inUse;
            }

            public IReadOnlyList<TagSentinel> Sentinels
            {
                get { return _sentinels; }
            }

            public string Start(ITagPair pair)
            {
                // The pair's index is taken at its start; the end looks it up, so nested pairs
                // keep their own numbers.
                var index = _pairs++;
                _open[pair] = index;
                return Add(SentinelKind.PairStart, pair, index, pair.StartTagProperties.TagContent);
            }

            public string End(ITagPair pair)
            {
                int index;
                if (!_open.TryGetValue(pair, out index)) index = -1;
                return Add(SentinelKind.PairEnd, pair, index, pair.EndTagProperties.TagContent);
            }

            public string Placeholder(IPlaceholderTag tag)
            {
                return Add(SentinelKind.Placeholder, tag, -1, tag.Properties.TagContent);
            }

            public string Structure(IStructureTag tag)
            {
                return Add(SentinelKind.Placeholder, tag, -1, tag.Properties.TagContent);
            }

            private readonly Dictionary<ITagPair, int> _open = new Dictionary<ITagPair, int>(new ReferenceComparer());

            private string Add(SentinelKind kind, IAbstractMarkupData tag, int pairIndex, string text)
            {
                while (_inUse.Contains(_next))
                {
                    _next++;
                }
                if (_next > PrivateUseLast)
                {
                    throw new InvalidOperationException("The paragraph holds more tags than the private-use area can stand for.");
                }

                var character = _next++;
                _sentinels.Add(new TagSentinel(character, kind, tag, pairIndex, text));
                return character.ToString();
            }

            private sealed class ReferenceComparer : IEqualityComparer<ITagPair>
            {
                public bool Equals(ITagPair x, ITagPair y)
                {
                    return ReferenceEquals(x, y);
                }

                public int GetHashCode(ITagPair obj)
                {
                    return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
                }
            }
        }

        private sealed class Walk
        {
            private readonly StringBuilder _builder;
            private readonly List<CommentRange> _comments;
            private readonly ITagTexts _texts;

            public Walk(StringBuilder builder, List<CommentRange> comments, ITagTexts texts)
            {
                _builder = builder;
                _comments = comments;
                _texts = texts;
            }

            public bool UsedTags { get; private set; }

            public void Append(IAbstractMarkupDataContainer container)
            {
                // Indexed rather than enumerated: at least one ISegment implementation in the
                // framework throws NotImplementedException from GetEnumerator.
                for (var i = 0; i < container.Count; i++)
                {
                    var item = container[i];

                    var text = item as IText;
                    if (text != null)
                    {
                        _builder.Append(text.Properties.Text);
                        continue;
                    }

                    var placeholder = item as IPlaceholderTag;
                    if (placeholder != null)
                    {
                        _builder.Append(_texts.Placeholder(placeholder));
                        UsedTags = true;
                        continue;
                    }

                    var structure = item as IStructureTag;
                    if (structure != null)
                    {
                        _builder.Append(_texts.Structure(structure));
                        UsedTags = true;
                        continue;
                    }

                    var pair = item as ITagPair;
                    if (pair != null)
                    {
                        _builder.Append(_texts.Start(pair));
                        UsedTags = true;
                        Append(pair);
                        _builder.Append(_texts.End(pair));
                        continue;
                    }

                    var locked = item as ILockedContent;
                    if (locked != null)
                    {
                        Append(locked.Content);
                        continue;
                    }

                    // A comment marker's range is noted before its content is walked, so an
                    // outer marker precedes the markers inside it.
                    var marker = item as ICommentMarker;
                    if (marker != null)
                    {
                        var index = _comments.Count;
                        _comments.Add(null);
                        var start = _builder.Length;
                        Append(marker);
                        _comments[index] = new CommentRange(marker.Comments, start, _builder.Length);
                        continue;
                    }

                    var nested = item as IAbstractMarkupDataContainer;
                    if (nested != null)
                    {
                        Append(nested);
                    }
                }
            }
        }
    }
}
