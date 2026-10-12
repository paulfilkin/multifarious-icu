using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Icu.Core.Tree;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// Applies an <see cref="ExpansionPlan"/> to one paragraph unit, in place. The unit's id,
    /// position and existing contexts are untouched; the source and target paragraphs are both
    /// rebuilt, the target as an empty mirror of the source, which is what a target file looks
    /// like after Copy to Target Languages.
    ///
    /// Every branch is an (open, segment, close) triple whose open and close carry the selector
    /// syntax as locked text between the segments. Locked content is what both of Studio's
    /// writers emit verbatim: the JSON writer drops placeholder tags wherever they sit, shown
    /// first by the cloud project's T4 and again by the first Studio generation here (Project
    /// 39, 5 Sep 2026), where the target paragraph carried every tag and the generated file
    /// none; its decompiled writer emits text for two special-character tags only.
    ///
    /// Arguments and '#' inside a segment are placeholder tags carrying the exact source text
    /// (Paul, 6 September 2026): a tag is what QuickPlace, Ctrl+Alt+Down, tag verification and
    /// the translation memory's placeables work with, where a locked span cannot be placed by
    /// the translator at all. Optionally each tag is wrapped in locked content so it cannot be
    /// moved or deleted. The restore task puts the message back together as plain text before
    /// the target file is generated.
    ///
    /// No comment is written for a form (Paul, 6 September 2026): a comment marker inside a
    /// source segment is copied into the target by Studio's pseudo-translation and Copy Source
    /// to Target (Project 46), and target comments are the translator's; a marker around the
    /// segment fails SDLXLIFF validation (Project 47); and a comment on the unit repeats what
    /// the ICU Forms window shows per row, live. The layout, the unit context and CLDR say
    /// everything the restore task and the window need. Only warnings go on the unit.
    ///
    /// A translator's comment on a restored or pre-translated target does come back, around
    /// the text it wrapped, in every form that text reaches: the seed carries each marker's
    /// range in the raw target, every parsed node carries its range, and hoisting reuses the
    /// nodes, so the two line up (<see cref="Assemble"/>).
    ///
    /// The filter's own tags, where a configuration tags embedded content, survive the same
    /// way: the value is parsed with a sentinel character in place of each tag
    /// (<see cref="RawValueReconstruction.Encode"/>), hoisting carries the characters into the
    /// forms with the text around them, and each form gets a clone of the tag where its
    /// character lands. Studio's writers accept the same tag cloned into several segments and
    /// write each as markup; a pair split across segments they drop (Project 56 probes,
    /// 12 October 2026), so a form holding one half of a pair refuses, and the unit passes
    /// through with a warning.
    /// </summary>
    public sealed class ExpansionWriter
    {
        private readonly IDocumentItemFactory _itemFactory;
        private readonly IPropertiesFactory _propertiesFactory;
        private readonly bool _lockPlaceholders;
        private readonly string _cldrVersion;
        private readonly string _appVersion;

        public ExpansionWriter(IDocumentItemFactory itemFactory, IPropertiesFactory propertiesFactory,
            bool lockPlaceholders, string cldrVersion, string appVersion)
        {
            if (itemFactory == null) throw new ArgumentNullException(nameof(itemFactory));
            if (propertiesFactory == null) throw new ArgumentNullException(nameof(propertiesFactory));

            _itemFactory = itemFactory;
            _propertiesFactory = propertiesFactory;
            _lockPlaceholders = lockPlaceholders;
            _cldrVersion = cldrVersion ?? string.Empty;
            _appVersion = appVersion ?? string.Empty;
        }

        public WriteResult Write(IParagraphUnit unit, ExpansionPlan plan, string resourceKey)
        {
            return Write(unit, plan, resourceKey, null);
        }

        /// <summary>
        /// Writes the plan with what the unit already held: the translation of each form, where
        /// the target carried a restored or pre-translated message, and the state each form
        /// segment had before the message was collapsed. A form the seed has nothing for starts
        /// empty, as on a first expansion.
        /// </summary>
        public WriteResult Write(IParagraphUnit unit, ExpansionPlan plan, string resourceKey, ExpansionSeed seed)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var source = new List<IAbstractMarkupData>();
            var target = new List<IAbstractMarkupData>();
            var state = new WriteState { Seed = seed ?? new ExpansionSeed() };
            state.NextSegmentNumber = Math.Max(1, state.Seed.FirstSegmentNumber);
            WriteNode(plan.Root, source, target, state);

            Refill(unit.Source, source);
            if (unit.Target != null) Refill(unit.Target, target);

            AddContext(unit, plan, resourceKey, state);
            return new WriteResult(state.Written.Count, state.TargetFormsFilled, state.UnfilledPaths, state.NextSegmentNumber,
                state.CommentsCarried, state.TagsCloned);
        }

        private sealed class WriteState
        {
            /// <summary>The next segment number: the seed's first, then on through the unit. The processor keeps the count running through the file (<see cref="SegmentNumbering"/>).</summary>
            public int NextSegmentNumber = 1;

            /// <summary>The segments written, in document order, for the seeding record on the unit context.</summary>
            public List<KeyValuePair<int, PlannedSegment>> Written = new List<KeyValuePair<int, PlannedSegment>>();

            public ExpansionSeed Seed;

            public int TargetFormsFilled;

            /// <summary>Comment markers rebuilt in the target segments from the seed's ranges, counted per form they reach.</summary>
            public int CommentsCarried;

            /// <summary>Filter tags cloned into the segments, both sides, one count per clone.</summary>
            public int TagsCloned;

            /// <summary>Paths the seed had a target for none of, where it had a target message at all.</summary>
            public List<string> UnfilledPaths = new List<string>();
        }

        private static void Refill(IParagraph paragraph, List<IAbstractMarkupData> content)
        {
            paragraph.Clear();
            foreach (var item in content)
            {
                paragraph.Add(item);
            }
        }

        private void WriteNode(PlannedNode node, List<IAbstractMarkupData> source, List<IAbstractMarkupData> target,
            WriteState state)
        {
            var selector = node as PlannedSelector;
            if (selector != null)
            {
                WriteSelector(selector, source, target, state);
                return;
            }

            var segment = node as PlannedSegment;
            if (segment != null)
            {
                WriteSegment(segment, source, target, state);
                return;
            }

            throw new InvalidOperationException("Unknown plan node '" + node.GetType().Name + "'.");
        }

        private void WriteSelector(PlannedSelector selector, List<IAbstractMarkupData> source,
            List<IAbstractMarkupData> target, WriteState state)
        {
            var openText = new StringBuilder()
                .Append('{').Append(selector.ArgumentName)
                .Append(", ").Append(selector.Type.ToKeyword()).Append(',');
            if (selector.OffsetRaw != null)
            {
                openText.Append(" offset:").Append(selector.OffsetRaw);
            }

            // Which selectors were category expanded is recorded on the unit context, not here:
            // restore prunes only expanded selectors, because a walked select or a disabled kind
            // carries the developer's branches.
            AddSyntaxPair(openText.ToString(), source, target);

            foreach (var branch in selector.Branches)
            {
                AddSyntaxPair(" " + branch.KeyText + " {", source, target);
                WriteNode(branch.Content, source, target, state);
                AddSyntaxPair("}", source, target);
            }

            AddSyntaxPair("}", source, target);
        }

        private void WriteSegment(PlannedSegment planned, List<IAbstractMarkupData> source,
            List<IAbstractMarkupData> target, WriteState state)
        {
            var properties = _itemFactory.CreateSegmentPairProperties();
            properties.Id = new SegmentId(state.NextSegmentNumber.ToString(CultureInfo.InvariantCulture));
            state.NextSegmentNumber++;

            // The segment holds text and tags only; the ICU Forms window is its note.
            var sourceSegment = _itemFactory.CreateSegment(properties);
            foreach (var item in Content(planned.Nodes, state.Seed.SourceTags, null, planned.Path, state))
            {
                sourceSegment.Add(item);
            }
            source.Add(sourceSegment);
            state.Written.Add(new KeyValuePair<int, PlannedSegment>(state.NextSegmentNumber - 1, planned));

            // The target segment shares the pair properties with the source, which is how the
            // framework models a segment pair. On a first expansion it is left empty for Copy
            // Source to Target or pre-translation to fill; after a restore, or over a
            // pre-translated message, it gets the form's translation back, with the state the
            // form had.
            var targetSegment = _itemFactory.CreateSegment(properties);
            var seed = state.Seed;
            if (seed.TargetForms != null)
            {
                IReadOnlyList<MessageNode> nodes;
                if (seed.TargetForms.TryGetValue(planned.Path, out nodes))
                {
                    foreach (var item in Content(nodes, seed.TargetTags, seed.TargetComments, planned.Path, state))
                    {
                        targetSegment.Add(item);
                    }
                    state.TargetFormsFilled++;
                }
                else
                {
                    state.UnfilledPaths.Add(planned.Path);
                }
            }
            target.Add(targetSegment);

            var segmentState = seed.StateOf(planned.Path);
            if (segmentState != null) segmentState.Apply(properties, _itemFactory);
        }

        // ---- a form's content: text, arguments, the filter's tags and the seed's comments -----

        /// <summary>
        /// A form's content as markup. Text and quoted text become text runs; an argument or '#'
        /// becomes a placeholder tag; a sentinel character becomes a clone of the filter tag it
        /// stands for, a pair's start and end becoming one cloned pair around the content
        /// between them; and the comment markers are rebuilt around the text they wrapped.
        /// A pair with only one half in the form has crossed the message's branch structure,
        /// which Studio's writers drop, so the form refuses with
        /// <see cref="TagPairSplitException"/>.
        /// </summary>
        private List<IAbstractMarkupData> Content(IReadOnlyList<MessageNode> nodes, EncodedValue tags,
            IReadOnlyList<CommentRange> comments, string path, WriteState state)
        {
            var pieces = Pieces(nodes, tags);
            if (comments != null && comments.Count > 0) pieces = Split(pieces, comments);

            var output = new List<IAbstractMarkupData>();
            var consumed = Assemble(pieces, 0, -1, path, Ordered(comments), state, output);
            if (consumed != pieces.Count) throw new TagPairSplitException(path);
            return output;
        }

        /// <summary>One run of a form's content with its offsets in the raw value: text, an argument's markup, or one half of a filter tag pair.</summary>
        private sealed class Piece
        {
            public int Start;
            public int End;
            public string Text;
            public IAbstractMarkupData Markup;

            /// <summary>Text that cannot be cut at a raw offset: a quoted node, whose value is shorter than its syntax.</summary>
            public bool Atomic;

            /// <summary>The pair this piece opens or closes, else -1.</summary>
            public int PairOpen = -1;
            public int PairClose = -1;
            public TagSentinel Sentinel;
        }

        /// <summary>An item built for one level of the content, with the raw range it covers; a container is wrapped by a comment only when the comment covers all of it.</summary>
        private sealed class Entry
        {
            public IAbstractMarkupData Item;
            public int Start;
            public int End;
            public bool Container;
        }

        private List<Piece> Pieces(IReadOnlyList<MessageNode> nodes, EncodedValue tags)
        {
            var pieces = new List<Piece>();
            foreach (var node in nodes)
            {
                var literal = node as TextNode;
                if (literal != null)
                {
                    AddTextPieces(pieces, literal.Value, literal.Span.Start, literal.Span.End,
                        literal.Value.Length != literal.Span.Length, tags);
                    continue;
                }

                // The decoded value is the unescaping of design 5.8: the translator sees clean text.
                var quoted = node as QuotedTextNode;
                if (quoted != null)
                {
                    AddTextPieces(pieces, quoted.Value, quoted.Span.Start, quoted.Span.End, true, tags);
                    continue;
                }

                string syntax;
                var argument = node as ArgumentNode;
                var typed = node as TypedArgumentNode;
                if (argument != null) syntax = "{" + argument.Name + "}";
                else if (typed != null) syntax = TypedArgumentText(typed);
                else if (node is PoundNode) syntax = "#";
                else throw new InvalidOperationException("A planned segment may not contain a '" + node.GetType().Name + "'.");

                pieces.Add(new Piece { Start = node.Span.Start, End = node.Span.End, Markup = ArgumentMarkup(syntax), Atomic = true });
            }
            return pieces;
        }

        /// <summary>
        /// A text value as pieces: runs of ordinary characters, and one piece per sentinel
        /// character. A plain text node's characters map one to one onto the raw value, so a
        /// run's offsets are exact; a quoted node's runs are atomic and share its span.
        /// </summary>
        private static void AddTextPieces(List<Piece> pieces, string value, int spanStart, int spanEnd, bool atomic, EncodedValue tags)
        {
            if (tags == null || !tags.HasTags)
            {
                if (value.Length > 0) pieces.Add(new Piece { Start = spanStart, End = spanEnd, Text = value, Atomic = atomic });
                return;
            }

            var run = new StringBuilder();
            var runStart = spanStart;
            for (var i = 0; i < value.Length; i++)
            {
                var sentinel = tags.SentinelOf(value[i]);
                if (sentinel == null)
                {
                    if (run.Length == 0) runStart = atomic ? spanStart : spanStart + i;
                    run.Append(value[i]);
                    continue;
                }

                if (run.Length > 0)
                {
                    pieces.Add(new Piece { Start = runStart, End = atomic ? spanEnd : spanStart + i, Text = run.ToString(), Atomic = atomic });
                    run.Clear();
                }

                var at = atomic ? spanStart : spanStart + i;
                var piece = new Piece { Start = at, End = atomic ? spanEnd : at + 1, Atomic = true, Sentinel = sentinel };
                switch (sentinel.Kind)
                {
                    case SentinelKind.PairStart:
                        piece.PairOpen = sentinel.PairIndex;
                        break;
                    case SentinelKind.PairEnd:
                        piece.PairClose = sentinel.PairIndex;
                        break;
                    default:
                        piece.Markup = (IAbstractMarkupData)sentinel.Tag.Clone();
                        break;
                }
                pieces.Add(piece);
            }

            if (run.Length > 0)
            {
                pieces.Add(new Piece { Start = runStart, End = spanEnd, Text = run.ToString(), Atomic = atomic });
            }
        }

        /// <summary>Cuts every non-atomic text piece at each comment boundary that falls inside it.</summary>
        private static List<Piece> Split(List<Piece> pieces, IReadOnlyList<CommentRange> comments)
        {
            var boundaries = new SortedSet<int>();
            foreach (var comment in comments)
            {
                boundaries.Add(comment.Start);
                boundaries.Add(comment.End);
            }

            var result = new List<Piece>();
            foreach (var piece in pieces)
            {
                if (piece.Atomic || piece.Text == null)
                {
                    result.Add(piece);
                    continue;
                }

                var at = piece.Start;
                foreach (var boundary in boundaries)
                {
                    if (boundary <= at) continue;
                    if (boundary >= piece.End) break;
                    result.Add(new Piece { Start = at, End = boundary, Text = piece.Text.Substring(at - piece.Start, boundary - at) });
                    at = boundary;
                }
                if (at < piece.End)
                {
                    result.Add(new Piece { Start = at, End = piece.End, Text = piece.Text.Substring(at - piece.Start) });
                }
            }
            return result;
        }

        /// <summary>The comments by start, then longest first, then as listed, so one inside another nests as it did.</summary>
        private static List<CommentRange> Ordered(IReadOnlyList<CommentRange> comments)
        {
            var ordered = new List<CommentRange>();
            if (comments == null) return ordered;
            ordered.AddRange(comments);
            ordered.Sort((a, b) =>
            {
                var byStart = a.Start.CompareTo(b.Start);
                if (byStart != 0) return byStart;
                var byLength = b.End.CompareTo(a.End);
                return byLength != 0 ? byLength : IndexOf(comments, a).CompareTo(IndexOf(comments, b));
            });
            return ordered;
        }

        private static int IndexOf(IReadOnlyList<CommentRange> comments, CommentRange comment)
        {
            for (var i = 0; i < comments.Count; i++)
            {
                if (ReferenceEquals(comments[i], comment)) return i;
            }
            return -1;
        }

        /// <summary>
        /// Builds one level of content from the pieces: a leaf per text or markup piece, and a
        /// cloned pair per pair start, filled by building the pieces up to its end at the next
        /// level. Returns the index after the last piece consumed, which for a nested level is
        /// the one after the pair's end; a level that runs out of pieces before its pair closes,
        /// or meets a close it did not open, stops short, which the caller treats as a split
        /// pair. The level's comment markers are then put around its entries: a leaf is
        /// wrapped by every comment touching it, a pair only by a comment covering all of it,
        /// so a comment that reaches into a pair wraps the text inside the pair at that level.
        /// </summary>
        private int Assemble(List<Piece> pieces, int from, int closingPair, string path, List<CommentRange> comments,
            WriteState state, List<IAbstractMarkupData> into)
        {
            var entries = new List<Entry>();
            var i = from;
            var closed = false;
            while (i < pieces.Count)
            {
                var piece = pieces[i];
                if (piece.PairClose >= 0)
                {
                    if (piece.PairClose != closingPair) throw new TagPairSplitException(path);
                    closed = true;
                    i++;
                    break;
                }

                if (piece.PairOpen >= 0)
                {
                    var close = IndexOfClose(pieces, i + 1, piece.PairOpen);
                    if (close < 0) throw new TagPairSplitException(path);

                    // A comment covering the whole pair wraps the pair at this level and is
                    // not applied again to the content inside it.
                    var innerComments = new List<CommentRange>();
                    foreach (var comment in comments)
                    {
                        if (!(comment.Start <= piece.Start && comment.End >= pieces[close].End)) innerComments.Add(comment);
                    }

                    var clone = ClonePair((ITagPair)piece.Sentinel.Tag);
                    state.TagsCloned++;
                    var inner = new List<IAbstractMarkupData>();
                    var next = Assemble(pieces, i + 1, piece.PairOpen, path, innerComments, state, inner);
                    foreach (var child in inner)
                    {
                        clone.Add(child);
                    }
                    entries.Add(new Entry { Item = clone, Start = piece.Start, End = pieces[next - 1].End, Container = true });
                    i = next;
                    continue;
                }

                if (piece.Markup != null && piece.Sentinel != null) state.TagsCloned++;
                entries.Add(new Entry { Item = piece.Markup ?? Text(piece.Text), Start = piece.Start, End = piece.End });
                i++;
            }

            if (closingPair >= 0 && !closed) throw new TagPairSplitException(path);

            Wrap(entries, comments, state, into);
            MergeText(into);
            return i;
        }

        /// <summary>The index of the piece closing the given pair, or -1 where the form does not hold it.</summary>
        private static int IndexOfClose(List<Piece> pieces, int from, int pair)
        {
            for (var i = from; i < pieces.Count; i++)
            {
                if (pieces[i].PairClose == pair) return i;
            }
            return -1;
        }

        /// <summary>Puts the comment markers around a level's entries, the open markers kept as a stack.</summary>
        private void Wrap(List<Entry> entries, List<CommentRange> comments, WriteState state, List<IAbstractMarkupData> into)
        {
            var open = new List<KeyValuePair<CommentRange, ICommentMarker>>();
            foreach (var entry in entries)
            {
                var wanted = new List<CommentRange>();
                foreach (var comment in comments)
                {
                    var applies = entry.Container
                        ? comment.Start <= entry.Start && comment.End >= entry.End && entry.End > entry.Start
                        : comment.Overlaps(entry.Start, entry.End);
                    if (applies) wanted.Add(comment);
                }

                // Keep the open markers that are still wanted, in order; close the rest and
                // open what the entry needs below them.
                var kept = 0;
                while (kept < open.Count && kept < wanted.Count && ReferenceEquals(open[kept].Key, wanted[kept])) kept++;
                open.RemoveRange(kept, open.Count - kept);
                for (var c = kept; c < wanted.Count; c++)
                {
                    var marker = _itemFactory.CreateCommentMarker((ICommentProperties)wanted[c].Comments.Clone());
                    Add(into, open, marker);
                    open.Add(new KeyValuePair<CommentRange, ICommentMarker>(wanted[c], marker));
                    state.CommentsCarried++;
                }

                Add(into, open, entry.Item);
            }
        }

        private static void Add(List<IAbstractMarkupData> output, List<KeyValuePair<CommentRange, ICommentMarker>> open,
            IAbstractMarkupData item)
        {
            if (open.Count == 0) output.Add(item);
            else open[open.Count - 1].Value.Add(item);
        }

        /// <summary>An empty pair with the filter's own start and end properties, tag id included, so the writer sees the same tag again.</summary>
        private ITagPair ClonePair(ITagPair pair)
        {
            return _itemFactory.CreateTagPair(
                (IStartTagProperties)pair.StartTagProperties.Clone(),
                (IEndTagProperties)pair.EndTagProperties.Clone());
        }

        /// <summary>Joins adjacent text items, at every level, so a form reads as the runs the translator typed.</summary>
        private static void MergeText(List<IAbstractMarkupData> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var current = items[i] as IText;
                var previous = items[i - 1] as IText;
                if (current == null || previous == null) continue;
                previous.Properties.Text += current.Properties.Text;
                items.RemoveAt(i);
            }

            foreach (var item in items)
            {
                var marker = item as ICommentMarker;
                if (marker == null) continue;
                var inner = new List<IAbstractMarkupData>();
                for (var i = 0; i < marker.Count; i++) inner.Add(marker[i]);
                MergeText(inner);
                marker.Clear();
                foreach (var child in inner) marker.Add(child);
            }
        }

        private static string TypedArgumentText(TypedArgumentNode typed)
        {
            return typed.Style == null
                ? "{" + typed.Name + ", " + typed.Type + "}"
                : "{" + typed.Name + ", " + typed.Type + ", " + typed.Style + "}";
        }

        private IText Text(string value)
        {
            return _itemFactory.CreateText(_propertiesFactory.CreateTextProperties(value));
        }

        /// <summary>
        /// An argument or '#' inside a segment: a placeholder tag whose content and display text
        /// are the exact source syntax, wrapped in locked content when the placeholders are locked.
        /// </summary>
        private IAbstractMarkupData ArgumentMarkup(string content)
        {
            var properties = _propertiesFactory.CreatePlaceholderTagProperties(content);
            properties.DisplayText = content;
            properties.SegmentationHint = SegmentationHint.Include;
            properties.SetMetaData(Constants.ArgumentTagKey, "true");
            var tag = _itemFactory.CreatePlaceholderTag(properties);
            if (!_lockPlaceholders) return tag;

            var locked = _itemFactory.CreateLockedContent(
                _propertiesFactory.CreateLockedContentProperties(LockTypeFlags.Manual));
            locked.Content.Add(tag);
            return locked;
        }

        /// <summary>
        /// One piece of selector syntax between segments, added to both paragraphs as locked text.
        /// Each side gets its own object: the two paragraphs are separate documents to the writer.
        /// Locked content carries no metadata; the segment's comment and the unit context are the
        /// machine-readable carriers, and restore reads the branch path from the syntax itself.
        /// </summary>
        private void AddSyntaxPair(string content, List<IAbstractMarkupData> source, List<IAbstractMarkupData> target)
        {
            source.Add(Locked(content));
            target.Add(Locked(content));
        }

        private ILockedContent Locked(string content)
        {
            var locked = _itemFactory.CreateLockedContent(
                _propertiesFactory.CreateLockedContentProperties(LockTypeFlags.Manual));
            locked.Content.Add(Text(content));
            return locked;
        }

        /// <summary>
        /// The unit-level record: one context of this plugin's own type, alongside the filter's,
        /// so the Document Structure column shows the resource key and that the unit is an ICU
        /// message, and the restore task can find the units it owns. Context metadata persists
        /// in SDLXLIFF.
        /// </summary>
        private void AddContext(IParagraphUnit unit, ExpansionPlan plan, string resourceKey, WriteState state)
        {
            var context = _propertiesFactory.CreateContextInfo(Constants.IcuContextType);
            context.DisplayName = Constants.IcuContextDisplayName;
            context.DisplayCode = Constants.IcuContextDisplayCode;
            context.Purpose = ContextPurpose.Information;
            context.Description = string.IsNullOrEmpty(resourceKey) ? Constants.IcuContextDisplayName : resourceKey;

            // The pattern is the value as the file carried it. A unit expanded again after a
            // restore is read in the hoisted shape, so the pattern recorded the first time is
            // kept rather than overwritten with the hoisted text.
            // Readable: with the filter's tags as their native text, not as sentinels.
            var tags = state.Seed.SourceTags;
            context.SetMetaData("icu:pattern", state.Seed.OriginalPattern ?? (tags == null ? plan.RawValue : tags.Decode(plan.RawValue)));
            context.SetMetaData("icu:hoisted", tags == null ? plan.HoistedText : tags.Decode(plan.HoistedText));
            context.SetMetaData(Constants.StateKey, Constants.StateExpanded);
            if (!string.IsNullOrEmpty(resourceKey))
            {
                context.SetMetaData("icu:key", resourceKey);
            }
            context.SetMetaData("icu:cldrVersion", _cldrVersion);
            context.SetMetaData("icu:appVersion", _appVersion);
            context.SetMetaData("icu:unitCount", plan.Segments.Count.ToString(CultureInfo.InvariantCulture));

            // Which selectors were category expanded, by path. Locked content carries no
            // metadata, so restore learns from here which selectors it may prune; a walked
            // select or a disabled plural kind keeps the developer's branches.
            var expanded = new List<string>();
            CollectExpanded(plan.Root, expanded);
            context.SetMetaData("icu:expandedSelectors", string.Join(",", expanded));

            // Which segments started from a source form other than their own, by segment number:
            // "2:other,3:other". The ICU Forms window says so in the row's tooltip.
            var seeded = new List<string>();
            var synthetic = new List<string>();
            foreach (var pair in state.Written)
            {
                string value;
                if (pair.Value.Metadata.TryGetValue("icu:seededFrom", out value) && !string.IsNullOrEmpty(value))
                {
                    seeded.Add(pair.Key.ToString(CultureInfo.InvariantCulture) + ":" + value);
                }
                if (pair.Value.Metadata.TryGetValue("icu:syntheticSource", out value) && value == "true")
                {
                    synthetic.Add(pair.Key.ToString(CultureInfo.InvariantCulture));
                }
            }
            context.SetMetaData("icu:seededFrom", string.Join(",", seeded));
            context.SetMetaData("icu:syntheticSource", string.Join(",", synthetic));

            // The unit gets its own context properties, never an addition to the object it
            // arrived with. The SDLXLIFF reader hands every paragraph unit in a group the same
            // IContextProperties instance, and the Java Resources filter groups the value with
            // the whitespace units around it; adding to the shared object made those structure
            // units carry the context too, and the writer then referenced a definition it never
            // wrote, which the next task read back as a null context (Phase 1 Studio run).
            var contexts = unit.Properties.Contexts == null
                ? _propertiesFactory.CreateContextProperties()
                : (IContextProperties)unit.Properties.Contexts.Clone();

            // A unit expanded again after a restore already carries this plugin's context; the
            // new one replaces it rather than sitting beside it.
            for (var i = contexts.Contexts.Count - 1; i >= 0; i--)
            {
                var existing = contexts.Contexts[i];
                if (existing != null && existing.ContextType == Constants.IcuContextType)
                {
                    contexts.Contexts.RemoveAt(i);
                }
            }

            contexts.Contexts.Add(context);
            unit.Properties.Contexts = contexts;
        }

        private static void CollectExpanded(PlannedNode node, List<string> paths)
        {
            var selector = node as PlannedSelector;
            if (selector == null) return;

            if (selector.IsExpanded) paths.Add(selector.Path);
            foreach (var branch in selector.Branches)
            {
                CollectExpanded(branch.Content, paths);
            }
        }
    }

    /// <summary>
    /// A filter tag pair with one half in a form and the other outside it: the pair crosses the
    /// message's branch structure, and Studio's writers drop a pair split across segments. The
    /// unit is left as it stands and the processor says why.
    /// </summary>
    public sealed class TagPairSplitException : Exception
    {
        public TagPairSplitException(string path)
            : base("A filter tag pair crosses the message's branch structure"
                   + (string.IsNullOrEmpty(path) ? string.Empty : " at form '" + path + "'")
                   + "; the message is left as it is, because a tag pair split across segments would be lost on output.")
        {
            Path = path ?? string.Empty;
        }

        public string Path { get; }
    }

    /// <summary>
    /// What a unit already holds when it is expanded: the forms of a target message by branch
    /// path, the state each form had, and the pattern recorded by an earlier expansion. All
    /// optional; an empty seed is a first expansion.
    /// </summary>
    public sealed class ExpansionSeed
    {
        /// <summary>The target's translation per form, from <see cref="TargetBranches"/>; null where the target was empty or did not read.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<MessageNode>> TargetForms { get; set; }

        /// <summary>The comment markers on the target and the ranges of the raw target they wrapped; null or empty where there were none.</summary>
        public IReadOnlyList<CommentRange> TargetComments { get; set; }

        /// <summary>The source as encoded for the plan, with the table from sentinel character to filter tag; null where the source holds no tags.</summary>
        public EncodedValue SourceTags { get; set; }

        /// <summary>The same for the target the forms were read from; null where it holds no tags.</summary>
        public EncodedValue TargetTags { get; set; }

        /// <summary>The state each form had before the restore task collapsed the message, by path.</summary>
        public IReadOnlyDictionary<string, SegmentState> States { get; set; }

        /// <summary>One state for every form: a pre-translated whole message's, handed to each of its forms.</summary>
        public SegmentState StateForAll { get; set; }

        /// <summary>The pattern as the file carried it, recorded by the first expansion.</summary>
        public string OriginalPattern { get; set; }

        /// <summary>Why the target message gave no forms, where it did not read; the processor reports it.</summary>
        public string TargetFailure { get; set; }

        /// <summary>The number the unit's first segment takes, so the numbers run on from the units before it. 1 by default.</summary>
        public int FirstSegmentNumber { get; set; } = 1;

        public SegmentState StateOf(string path)
        {
            SegmentState state;
            if (States != null && States.TryGetValue(path, out state)) return state;
            return StateForAll == null ? null : StateForAll.At(path);
        }
    }

    public sealed class WriteResult
    {
        public WriteResult(int segments, int targetFormsFilled, IReadOnlyList<string> unfilledPaths, int nextSegmentNumber)
            : this(segments, targetFormsFilled, unfilledPaths, nextSegmentNumber, 0)
        {
        }

        public WriteResult(int segments, int targetFormsFilled, IReadOnlyList<string> unfilledPaths, int nextSegmentNumber,
            int commentsCarried)
            : this(segments, targetFormsFilled, unfilledPaths, nextSegmentNumber, commentsCarried, 0)
        {
        }

        public WriteResult(int segments, int targetFormsFilled, IReadOnlyList<string> unfilledPaths, int nextSegmentNumber,
            int commentsCarried, int tagsCloned)
        {
            Segments = segments;
            TargetFormsFilled = targetFormsFilled;
            UnfilledPaths = unfilledPaths ?? new List<string>();
            NextSegmentNumber = nextSegmentNumber;
            CommentsCarried = commentsCarried;
            TagsCloned = tagsCloned;
        }

        /// <summary>Filter tags cloned into the segments on both sides.</summary>
        public int TagsCloned { get; }

        /// <summary>Comment markers rebuilt in the target segments from the seed's target, one count per form a comment reached.</summary>
        public int CommentsCarried { get; }

        /// <summary>The number after the last segment written.</summary>
        public int NextSegmentNumber { get; }

        public int Segments { get; }

        /// <summary>Target segments filled from the seed's target message.</summary>
        public int TargetFormsFilled { get; }

        /// <summary>Forms the seed's target message had nothing for; empty where there was no target message.</summary>
        public IReadOnlyList<string> UnfilledPaths { get; }
    }
}
