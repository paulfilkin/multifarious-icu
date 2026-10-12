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
        /// True where the filter had fragmented the value into its own tags. When such a value is
        /// expanded those tags are discarded, because the expansion's layout supersedes them.
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
    /// </summary>
    public static class RawValueReconstruction
    {
        public static ReconstructedValue Reconstruct(IAbstractMarkupDataContainer paragraph)
        {
            if (paragraph == null) throw new ArgumentNullException(nameof(paragraph));

            var builder = new StringBuilder();
            var usedTags = false;
            var comments = new List<CommentRange>();
            Append(paragraph, builder, comments, ref usedTags);
            return new ReconstructedValue(builder.ToString(), usedTags, comments);
        }

        private static void Append(IAbstractMarkupDataContainer container, StringBuilder builder,
            List<CommentRange> comments, ref bool usedTags)
        {
            // Indexed rather than enumerated: at least one ISegment implementation in the framework
            // throws NotImplementedException from GetEnumerator.
            for (var i = 0; i < container.Count; i++)
            {
                var item = container[i];

                var text = item as IText;
                if (text != null)
                {
                    builder.Append(text.Properties.Text);
                    continue;
                }

                var placeholder = item as IPlaceholderTag;
                if (placeholder != null)
                {
                    builder.Append(placeholder.Properties.TagContent);
                    usedTags = true;
                    continue;
                }

                var structure = item as IStructureTag;
                if (structure != null)
                {
                    builder.Append(structure.Properties.TagContent);
                    usedTags = true;
                    continue;
                }

                var pair = item as ITagPair;
                if (pair != null)
                {
                    builder.Append(pair.StartTagProperties.TagContent);
                    usedTags = true;
                    Append(pair, builder, comments, ref usedTags);
                    builder.Append(pair.EndTagProperties.TagContent);
                    continue;
                }

                var locked = item as ILockedContent;
                if (locked != null)
                {
                    Append(locked.Content, builder, comments, ref usedTags);
                    continue;
                }

                // A comment marker's range is noted before its content is walked, so an outer
                // marker precedes the markers inside it.
                var marker = item as ICommentMarker;
                if (marker != null)
                {
                    var index = comments.Count;
                    comments.Add(null);
                    var start = builder.Length;
                    Append(marker, builder, comments, ref usedTags);
                    comments[index] = new CommentRange(marker.Comments, start, builder.Length);
                    continue;
                }

                var nested = item as IAbstractMarkupDataContainer;
                if (nested != null)
                {
                    Append(nested, builder, comments, ref usedTags);
                }
            }
        }
    }
}
