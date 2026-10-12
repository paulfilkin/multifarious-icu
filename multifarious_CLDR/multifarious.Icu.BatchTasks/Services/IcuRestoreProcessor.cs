using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Icu.Cldr;
using Icu.Core;
using multifarious.Icu.Expansion;
using Sdl.Core.Globalization;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>A restore check failed under a fail-task setting: the run stops, naming the unit.</summary>
    public sealed class RestoreFailedException : Exception
    {
        public RestoreFailedException(string paragraphUnitId, string reason)
            : base("Paragraph unit " + paragraphUnitId + ": " + reason)
        {
            ParagraphUnitId = paragraphUnitId;
        }

        public string ParagraphUnitId { get; }
    }

    /// <summary>
    /// The restore task's engine, as a bilingual content processor. For every paragraph unit
    /// laid out one segment per form: prune the branches of category-expanded selectors to the
    /// target language's exact set, both paragraphs in step; fill an empty target segment from
    /// the source; escape ICU special characters typed as target text; check that every
    /// protected span in the source is in the target; and then put the message back together
    /// as one plain-text segment on each side, the shape the filter delivered and the shape
    /// Studio's writers emit as it stands (Paul, 11 October 2026). The target comes back in the
    /// hoisted shape the translator worked in; the source comes back as the developer wrote
    /// it, from the pattern the unit context kept, so a memory updated from the restored file
    /// matches a new project's source (Project 59, 12 October 2026). The filter's own tags,
    /// where a configuration tags embedded content, stay tags: each is cloned into the
    /// restored segment where its form held it, which the writers accept (Project 56 probes,
    /// 12 October 2026).
    ///
    /// Before the collapse, each form's confirmation level, lock and translation origin are
    /// recorded on the unit context by branch path, and the context's state becomes restored.
    /// The expand task reads both back, so expand and restore can alternate without loss.
    /// A translator's comment on a form wraps that form's part of the restored text.
    ///
    /// A placeholder mismatch under the warn setting leaves the message laid out, its
    /// placeholders as tags on both sides and a comment on the target segment, so the
    /// translator can place the missing one; it must be restored again before the target file
    /// is generated, and the comment says so.
    ///
    /// The layout is read from the content itself. The syntax between segments is locked text
    /// (or a placeholder tag under the variant) whose text is one of three shapes: a selector
    /// open, a branch open, or a close brace. That grammar is unambiguous for the writer's
    /// output, and which selectors were category expanded is in the unit context.
    ///
    /// Escaping is unescape-then-escape, so restoring a message a translator has already typed
    /// ICU escapes into produces the same text: an apostrophe typed as one or as two comes out
    /// as two either way.
    /// </summary>
    public class IcuRestoreProcessor : AbstractBilingualContentProcessor
    {
        private static readonly Regex SelectorOpen =
            new Regex(@"^\{(?<arg>[^,{}\s]+),\s*(?<kind>plural|selectordinal|select),", RegexOptions.CultureInvariant);

        private static readonly Regex BranchOpen =
            new Regex(@"^\s*(?<key>\S+)\s*\{$", RegexOptions.CultureInvariant);

        private static readonly HashSet<string> CategoryKeywords =
            new HashSet<string>(StringComparer.Ordinal) { "zero", "one", "two", "few", "many", "other" };

        private readonly string _targetLanguageTag;
        private readonly RestoreOptions _options;
        private readonly CldrPlurals _plurals = CldrPlurals.Default;
        private readonly List<ExpansionWarning> _warnings = new List<ExpansionWarning>();
        private readonly List<RestoreUnitOutcome> _outcomes = new List<RestoreUnitOutcome>();

        private string _fileTargetLanguage;
        private Dictionary<SelectorKind, HashSet<string>> _keepSets;

        /// <summary>The branch path of every segment met during the prune walk, by segment; the empty path for a protected message.</summary>
        private readonly Dictionary<ISegment, string> _paths = new Dictionary<ISegment, string>(new ReferenceComparer());

        /// <summary>The next segment number in the file; see <see cref="SegmentNumbering"/>.</summary>
        private int _nextSegmentNumber = 1;

        public IcuRestoreProcessor(string targetLanguageTag, RestoreOptions options)
        {
            _targetLanguageTag = targetLanguageTag;
            _options = options ?? RestoreOptions.Default;
        }

        /// <summary>Units carrying the plugin's context.</summary>
        public int Units { get; private set; }

        /// <summary>Branch triples removed.</summary>
        public int Pruned { get; private set; }

        /// <summary>Target segments filled from the source.</summary>
        public int Filled { get; private set; }

        /// <summary>The plugin's own comment markers removed from target segments, where Studio had copied them from the source.</summary>
        public int CommentsStripped { get; private set; }

        /// <summary>Units put back together as one segment per side.</summary>
        public int Restored { get; private set; }

        public IReadOnlyList<ExpansionWarning> Warnings { get { return _warnings; } }

        /// <summary>What happened to every unit this plugin owns, in document order, for the report.</summary>
        public IReadOnlyList<RestoreUnitOutcome> Outcomes { get { return _outcomes; } }

        public override void SetFileProperties(IFileProperties fileInfo)
        {
            base.SetFileProperties(fileInfo);
            var conversion = fileInfo == null ? null : fileInfo.FileConversionProperties;
            var language = conversion == null ? null : conversion.TargetLanguage;
            _fileTargetLanguage = language == null ? null
                : language.CultureInfo != null ? language.CultureInfo.Name : language.IsoAbbreviation;
        }

        public override void ProcessParagraphUnit(IParagraphUnit paragraphUnit)
        {
            if (paragraphUnit != null && !paragraphUnit.IsStructure && paragraphUnit.Source != null)
            {
                if (paragraphUnit.Target != null && ResourceKey.IsExpanded(paragraphUnit))
                {
                    Restore(paragraphUnit);
                }

                // The numbers run on through the file, restored units and the rest alike.
                SegmentNumbering.Renumber(paragraphUnit, ref _nextSegmentNumber);
            }

            base.ProcessParagraphUnit(paragraphUnit);
        }

        private Dictionary<SelectorKind, HashSet<string>> KeepSets
        {
            get
            {
                if (_keepSets == null)
                {
                    var language = _targetLanguageTag ?? _fileTargetLanguage;
                    if (string.IsNullOrEmpty(language))
                    {
                        throw new InvalidOperationException(
                            "The target language is unknown: neither the task nor the file supplied one.");
                    }

                    _keepSets = new Dictionary<SelectorKind, HashSet<string>>
                    {
                        [SelectorKind.Cardinal] = KeywordsOf(language, SelectorKind.Cardinal),
                        [SelectorKind.Ordinal] = KeywordsOf(language, SelectorKind.Ordinal),
                    };
                }
                return _keepSets;
            }
        }

        private HashSet<string> KeywordsOf(string language, SelectorKind kind)
        {
            return new HashSet<string>(
                _plurals.Resolve(language, kind).Categories.Select(c => c.ToKeyword()), StringComparer.Ordinal);
        }

        private void Restore(IParagraphUnit unit)
        {
            Units++;
            _paths.Clear();
            var unitId = unit.Properties.ParagraphUnitId.Id;
            var expandedPaths = ExpandedSelectorPaths(unit);
            var prunedBefore = Pruned;
            var filledBefore = Filled;
            var unitWarnings = new List<string>();

            // 1. Prune both paragraphs with identical decisions; they are mirrored, so the same
            // branches disappear from each. The walk also notes each segment's branch path, for
            // the state record the collapse writes.
            var inPluralContext = false;
            Pruned += Prune(unit.Source, expandedPaths, ref inPluralContext);
            Prune(unit.Target, expandedPaths, ref inPluralContext);

            // 2. Per remaining segment pair: fill empty targets from the source, escape target
            // text, and check placeholder parity. Filling precedes escaping so copied source
            // content is escaped like everything else. First, the plugin's own comments are
            // taken out of the targets: Studio's pseudo-translation and Copy Source to Target
            // copy a source segment's content with its comment marker (Project 46), and target
            // comments are the translator's. Theirs stay.
            var sourceSegments = SegmentsOf(unit.Source);
            var targetSegments = SegmentsOf(unit.Target);
            foreach (var segment in targetSegments)
            {
                CommentsStripped += StripOwnComments(segment);
            }

            var mismatch = false;
            for (var index = 0; index < targetSegments.Count; index++)
            {
                var target = targetSegments[index];
                var source = index < sourceSegments.Count ? sourceSegments[index] : null;
                var reasons = new List<string>();

                if (source != null && IsEmpty(target))
                {
                    if (_options.OnEmptyBranch == EmptyBranchBehaviour.FailTask)
                    {
                        throw new RestoreFailedException(unitId,
                            "Target segment " + target.Properties.Id.Id + " has no translation.");
                    }

                    FillFromSource(source, target);
                    Filled++;
                    reasons.Add("The segment was untranslated; the source text was used.");
                }

                EscapeText(target, inPluralContext);

                string parityDetail;
                if (source != null && !PlaceholdersMatch(source, target, out parityDetail))
                {
                    if (_options.OnPlaceholderMismatch == PlaceholderMismatchBehaviour.FailTask)
                    {
                        throw new RestoreFailedException(unitId,
                            "Target segment " + target.Properties.Id.Id + ": " + parityDetail);
                    }

                    reasons.Add("Placeholder mismatch: " + parityDetail + ". "
                        + "The message is left expanded with its placeholders as tags; correct it and run ICU Restore again before Generate Target Translations.");
                    mismatch = true;
                }

                if (reasons.Count > 0)
                {
                    // On the target segment, where the problem is and where a reviewer would put
                    // it (Paul, 6 September 2026, Project 56): the comment icon shows on the row
                    // and the Comments window jumps to it. The next run clears it first.
                    AddTargetComment(target, string.Join("\n", reasons), mismatch ? Severity.High : Severity.Medium);
                    foreach (var reason in reasons)
                    {
                        var text = "Segment " + target.Properties.Id.Id + ": " + reason;
                        _warnings.Add(new ExpansionWarning(unitId, text));
                        unitWarnings.Add(text);
                    }
                }
            }

            // 3. The message takes the shape its state calls for. Clean: the forms are put back
            // together as one plain segment per side, with the forms' states recorded on the
            // unit context first. With a placeholder mismatch, and the task set to warn: the
            // message stays laid out and editable, tags on both sides, and locked spans from an
            // earlier run turn back into tags, so the translator can place the missing one with
            // QuickPlace. It must be restored again before the target file is generated, and the
            // warning says so.
            if (mismatch)
            {
                foreach (var segment in sourceSegments) RestorePlaceholders(segment);
                foreach (var segment in targetSegments) RestorePlaceholders(segment);
            }
            else
            {
                Collapse(unit, targetSegments);
                Restored++;
            }

            _outcomes.Add(new RestoreUnitOutcome(unitId, ResourceKey.Of(unit), targetSegments.Count,
                Pruned - prunedBefore, Filled - filledBefore, unitWarnings));
            Diagnostics.Write("  unit " + unitId + ": " + (mismatch ? "left expanded with tags" : "restored")
                + ", forms=" + targetSegments.Count + ", comments stripped=" + CommentsStripped);
        }

        /// <summary>
        /// The paths of the selectors the expansion category-expanded, from the unit context.
        /// A file expanded before the key existed reports null, and every plural-kind selector
        /// is then treated as expanded.
        /// </summary>
        private static HashSet<string> ExpandedSelectorPaths(IParagraphUnit unit)
        {
            foreach (var context in unit.Properties.Contexts.Contexts)
            {
                if (context != null && context.ContextType == Constants.IcuContextType
                    && context.MetaDataContainsKey("icu:expandedSelectors"))
                {
                    var value = context.GetMetaData("icu:expandedSelectors") ?? string.Empty;
                    return new HashSet<string>(
                        value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
                }
            }
            return null;
        }

        // ---- pruning -------------------------------------------------------------------

        private int Prune(IParagraph paragraph, HashSet<string> expandedPaths, ref bool inPluralContext)
        {
            var items = ItemsOf(paragraph);
            if (items.Count == 0 || SyntaxOf(items[0]) == null || !SelectorOpen.IsMatch(SyntaxOf(items[0])))
            {
                // A protected argument-only message: one segment, nothing to prune.
                foreach (var item in items) NotePath(item, string.Empty);
                return 0;
            }

            var index = 0;
            var removed = 0;
            var kept = RebuildSelector(items, ref index, "", expandedPaths, ref removed, ref inPluralContext);

            paragraph.Clear();
            foreach (var item in kept)
            {
                paragraph.Add(item);
            }
            return removed;
        }

        private List<IAbstractMarkupData> RebuildSelector(List<IAbstractMarkupData> items, ref int index,
            string parentBranchPath, HashSet<string> expandedPaths, ref int removed, ref bool inPluralContext)
        {
            var kept = new List<IAbstractMarkupData>();
            var open = items[index];
            var openMatch = SelectorOpen.Match(SyntaxOf(open) ?? string.Empty);
            if (!openMatch.Success)
            {
                throw new InvalidOperationException("Expected a selector open marker at item " + index + ".");
            }

            var argument = openMatch.Groups["arg"].Value;
            var kindText = openMatch.Groups["kind"].Value;
            SelectorKind? kind = kindText == "plural" ? SelectorKind.Cardinal
                : kindText == "selectordinal" ? SelectorKind.Ordinal
                : (SelectorKind?)null;
            if (kind != null) inPluralContext = true;

            var selectorPath = parentBranchPath.Length == 0 ? argument : parentBranchPath + "/" + argument;
            var expanded = kind != null && (expandedPaths == null || expandedPaths.Contains(selectorPath));

            kept.Add(open);
            index++;

            while (index < items.Count)
            {
                var text = SyntaxOf(items[index]);
                if (text == "}")
                {
                    // The selector close.
                    kept.Add(items[index]);
                    index++;
                    return kept;
                }

                var branchMatch = text == null ? null : BranchOpen.Match(text);
                if (branchMatch == null || !branchMatch.Success)
                {
                    throw new InvalidOperationException("Expected a branch open marker at item " + index + ".");
                }

                var branchOpen = items[index];
                var key = branchMatch.Groups["key"].Value;
                index++;

                // A leaf is the segment, or the comment marker the expansion wrote around it;
                // anything that is not a syntax marker. A nested selector opens with syntax.
                var content = new List<IAbstractMarkupData>();
                if (SyntaxOf(items[index]) == null)
                {
                    NotePath(items[index], selectorPath + ":" + key);
                    content.Add(items[index]);
                    index++;
                }
                else
                {
                    content.AddRange(RebuildSelector(items, ref index, selectorPath + ":" + key, expandedPaths,
                        ref removed, ref inPluralContext));
                }

                var branchClose = items[index];
                index++;

                var keep = !expanded
                    || key.StartsWith("=", StringComparison.Ordinal)
                    || !CategoryKeywords.Contains(key)
                    || KeepSets[kind.Value].Contains(key);
                if (keep)
                {
                    kept.Add(branchOpen);
                    kept.AddRange(content);
                    kept.Add(branchClose);
                }
                else
                {
                    removed++;
                }
            }

            throw new InvalidOperationException("The selector open marker for '" + argument + "' has no close.");
        }

        /// <summary>The syntax text a marker carries, whichever construct wrote it; null for anything that is not a marker.</summary>
        private static string SyntaxOf(IAbstractMarkupData item)
        {
            var locked = item as ILockedContent;
            if (locked != null) return RawValueReconstruction.Reconstruct(locked.Content).RawValue;

            var tag = item as IPlaceholderTag;
            if (tag != null) return tag.Properties.TagContent;

            return null;
        }

        // ---- segments -------------------------------------------------------------------

        private static List<IAbstractMarkupData> ItemsOf(IAbstractMarkupDataContainer container)
        {
            var items = new List<IAbstractMarkupData>();
            for (var i = 0; i < container.Count; i++)
            {
                items.Add(container[i]);
            }
            return items;
        }

        private static List<ISegment> SegmentsOf(IParagraph paragraph)
        {
            var segments = new List<ISegment>();
            Collect(paragraph, segments);
            return segments;
        }

        private static void Collect(IAbstractMarkupDataContainer container, List<ISegment> into)
        {
            for (var i = 0; i < container.Count; i++)
            {
                var segment = container[i] as ISegment;
                if (segment != null)
                {
                    into.Add(segment);
                    continue;
                }

                var nested = container[i] as IAbstractMarkupDataContainer;
                if (nested != null) Collect(nested, into);
            }
        }

        /// <summary>No text, no protected span and no argument tag: a filter's own tags with nothing between them are still empty.</summary>
        private static bool IsEmpty(ISegment segment)
        {
            var empty = true;
            Walk(segment, item =>
            {
                var text = item as IText;
                if (text != null && !string.IsNullOrWhiteSpace(text.Properties.Text)) empty = false;
                if (item is ILockedContent) empty = false;
                var tag = item as IPlaceholderTag;
                if (tag != null && IsArgumentTag(tag)) empty = false;
            });
            return empty;
        }

        /// <summary>
        /// Whether a placeholder tag is one the expansion wrote for an argument or '#', as
        /// against a filter's own placeholder. The expansion marks its tags; a file expanded
        /// before the mark existed is read by the tag's content, which is ICU syntax.
        /// </summary>
        public static bool IsArgumentTag(IPlaceholderTag tag)
        {
            if (tag.Properties.MetaDataContainsKey(Constants.ArgumentTagKey)) return true;
            var content = tag.Properties.TagContent ?? string.Empty;
            return content == "#" || (content.StartsWith("{", StringComparison.Ordinal) && content.EndsWith("}", StringComparison.Ordinal));
        }

        /// <summary>Visits every leaf under a container, through markers, not into locked content.</summary>
        private static void Walk(IAbstractMarkupDataContainer container, Action<IAbstractMarkupData> visit)
        {
            for (var i = 0; i < container.Count; i++)
            {
                var item = container[i];
                visit(item);

                if (item is ILockedContent) continue;

                var nested = item as IAbstractMarkupDataContainer;
                if (nested != null) Walk(nested, visit);
            }
        }

        /// <summary>
        /// Fresh copies of the source segment's translatable content: text, protected spans and
        /// the filter's tags rebuilt, a tag pair with its content inside it, comment wrappers
        /// not carried over.
        /// </summary>
        private void FillFromSource(ISegment source, ISegment target)
        {
            var copies = CopiesOf(source);
            target.Clear();
            foreach (var copy in copies)
            {
                target.Add(copy);
            }
        }

        private List<IAbstractMarkupData> CopiesOf(IAbstractMarkupDataContainer container)
        {
            var copies = new List<IAbstractMarkupData>();
            for (var i = 0; i < container.Count; i++)
            {
                var item = container[i];

                var text = item as IText;
                if (text != null)
                {
                    copies.Add(ItemFactory.CreateText(PropertiesFactory.CreateTextProperties(text.Properties.Text)));
                    continue;
                }

                var locked = item as ILockedContent;
                if (locked != null)
                {
                    var copy = ItemFactory.CreateLockedContent(
                        PropertiesFactory.CreateLockedContentProperties(locked.Properties.LockType));
                    copy.Content.Add(ItemFactory.CreateText(PropertiesFactory.CreateTextProperties(
                        RawValueReconstruction.Reconstruct(locked.Content).RawValue)));
                    copies.Add(copy);
                    continue;
                }

                var pair = item as ITagPair;
                if (pair != null)
                {
                    var copy = ClonePair(pair);
                    foreach (var child in CopiesOf(pair))
                    {
                        copy.Add(child);
                    }
                    copies.Add(copy);
                    continue;
                }

                if (item is IPlaceholderTag || item is IStructureTag)
                {
                    copies.Add((IAbstractMarkupData)item.Clone());
                    continue;
                }

                var nested = item as IAbstractMarkupDataContainer;
                if (nested != null) copies.AddRange(CopiesOf(nested));
            }
            return copies;
        }

        /// <summary>An empty pair with the filter's own start and end properties, tag id included.</summary>
        private ITagPair ClonePair(ITagPair pair)
        {
            return ItemFactory.CreateTagPair(
                (IStartTagProperties)pair.StartTagProperties.Clone(),
                (IEndTagProperties)pair.EndTagProperties.Clone());
        }

        /// <summary>
        /// Escapes the target's text runs for ICU: apostrophes doubled and literal braces (and
        /// '#' inside a plural) quoted. Text inside a locked span is syntax and is never touched.
        /// Unescaped first, so the result is the same whether the translator typed ICU escapes
        /// or plain text, and whether an earlier run has escaped it before.
        /// </summary>
        private static void EscapeText(ISegment segment, bool inPluralContext)
        {
            Walk(segment, item =>
            {
                var text = item as IText;
                if (text == null) return;

                var plain = IcuEscaping.Unescape(text.Properties.Text, inPluralContext);
                text.Properties.Text = IcuEscaping.Escape(plain, inPluralContext);
            });
        }

        /// <summary>
        /// Removes the plugin's own comments from a target segment, leaving the content in
        /// place. A marker holding only the plugin's comments is unwrapped; one that also holds
        /// a translator's comment keeps the marker and loses only the plugin's. Returns the
        /// number of markers touched.
        /// </summary>
        private static int StripOwnComments(IAbstractMarkupDataContainer container)
        {
            var touched = 0;
            for (var i = 0; i < container.Count; i++)
            {
                var item = container[i];
                if (item is ILockedContent) continue;

                var marker = item as ICommentMarker;
                if (marker == null)
                {
                    var nested = item as IAbstractMarkupDataContainer;
                    if (nested != null) touched += StripOwnComments(nested);
                    continue;
                }

                touched += StripOwnComments(marker);

                var own = new List<IComment>();
                for (var c = 0; c < marker.Comments.Count; c++)
                {
                    var comment = marker.Comments.GetItem(c);
                    if (comment.Author == Constants.CommentAuthor) own.Add(comment);
                }
                if (own.Count == 0) continue;

                touched++;
                if (own.Count < marker.Comments.Count)
                {
                    foreach (var comment in own) marker.Comments.Delete(comment);
                    continue;
                }

                // Unwrap: the marker's children take its place, in order.
                var children = ItemsOf(marker);
                container.RemoveAt(i);
                foreach (var child in children)
                {
                    child.RemoveFromParent();
                }
                for (var k = 0; k < children.Count; k++)
                {
                    container.Insert(i + k, children[k]);
                }
                i += children.Count - 1;
            }
            return touched;
        }

        // ---- the collapse -----------------------------------------------------------------

        /// <summary>
        /// Puts the message back together: one segment on each side, plain text. The source is
        /// the pattern as the developer wrote it (<see cref="OriginalSource"/>); the target is
        /// the text of its paragraph in the hoisted shape the forms were laid out in, with each
        /// translator's comment rebuilt around the text it wrapped. The pair's confirmation
        /// level is the forms' common one or Draft, and it is locked only where every form was.
        /// First the forms' states go on the unit context by path, and the state becomes
        /// restored, for the next expansion to read.
        /// </summary>
        private void Collapse(IParagraphUnit unit, List<ISegment> targetSegments)
        {
            var states = new List<SegmentState>();
            var levels = new List<ConfirmationLevel>();
            var allLocked = targetSegments.Count > 0;
            foreach (var segment in targetSegments)
            {
                string path;
                if (!_paths.TryGetValue(segment, out path)) path = string.Empty;
                states.Add(SegmentState.Capture(path, segment.Properties));
                levels.Add(segment.Properties.ConfirmationLevel);
                if (!segment.Properties.IsLocked) allLocked = false;
            }

            // The id is provisional; the processor numbers the unit with the rest of the file.
            var properties = ItemFactory.CreateSegmentPairProperties();
            properties.Id = new SegmentId("1");
            properties.ConfirmationLevel = SegmentStates.Combined(levels);
            properties.IsLocked = allLocked;

            var source = ItemFactory.CreateSegment(properties);
            foreach (var item in OriginalSource(unit))
            {
                source.Add(item);
            }

            var target = ItemFactory.CreateSegment(properties);
            foreach (var item in Collapsed(unit.Target))
            {
                target.Add(item);
            }

            unit.Source.Clear();
            unit.Source.Add(source);
            unit.Target.Clear();
            unit.Target.Add(target);

            MarkRestored(unit, states);
        }

        /// <summary>Notes the branch path of the segment an item is or wraps, for the state record.</summary>
        private void NotePath(IAbstractMarkupData item, string path)
        {
            var segment = item as ISegment;
            if (segment != null)
            {
                _paths[segment] = path;
                return;
            }

            if (item is ILockedContent) return;
            var container = item as IAbstractMarkupDataContainer;
            if (container == null) return;
            for (var i = 0; i < container.Count; i++)
            {
                NotePath(container[i], path);
            }
        }

        /// <summary>Segments compared by identity: the framework's markup types are not keys by content.</summary>
        private sealed class ReferenceComparer : IEqualityComparer<ISegment>
        {
            public bool Equals(ISegment x, ISegment y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(ISegment obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }

        /// <summary>
        /// A paragraph as one segment's content: text runs, with the syntax between and inside
        /// the forms as text; every comment marker rebuilt around the text of what it wrapped,
        /// a marker inside another rebuilt inside its copy; and the filter's own tags cloned
        /// where they stood, a pair around its collapsed content. The expansion's argument tags
        /// and locked spans become their text: a restored segment is plain apart from what the
        /// filter put there.
        /// </summary>
        private List<IAbstractMarkupData> Collapsed(IParagraph paragraph)
        {
            var items = new List<IAbstractMarkupData>();
            var run = new StringBuilder();
            AppendCollapsed(paragraph, items, run);
            Flush(items, run);
            return items;
        }

        private void AppendCollapsed(IAbstractMarkupDataContainer container, List<IAbstractMarkupData> items, StringBuilder run)
        {
            for (var i = 0; i < container.Count; i++)
            {
                var item = container[i];

                var text = item as IText;
                if (text != null)
                {
                    run.Append(text.Properties.Text);
                    continue;
                }

                var pair = item as ITagPair;
                if (pair != null)
                {
                    Flush(items, run);
                    var copy = ClonePair(pair);
                    var inner = new List<IAbstractMarkupData>();
                    var innerRun = new StringBuilder();
                    AppendCollapsed(pair, inner, innerRun);
                    Flush(inner, innerRun);
                    foreach (var child in inner)
                    {
                        copy.Add(child);
                    }
                    items.Add(copy);
                    continue;
                }

                var placeholder = item as IPlaceholderTag;
                if ((placeholder != null && !IsArgumentTag(placeholder)) || item is IStructureTag)
                {
                    Flush(items, run);
                    items.Add((IAbstractMarkupData)item.Clone());
                    continue;
                }

                var syntax = SyntaxOf(item);
                if (syntax != null)
                {
                    run.Append(syntax);
                    continue;
                }

                var marker = item as ICommentMarker;
                if (marker != null)
                {
                    Flush(items, run);
                    var copy = ItemFactory.CreateCommentMarker((ICommentProperties)marker.Comments.Clone());
                    var inner = new List<IAbstractMarkupData>();
                    var innerRun = new StringBuilder();
                    AppendCollapsed(marker, inner, innerRun);
                    Flush(inner, innerRun);
                    foreach (var child in inner)
                    {
                        copy.Add(child);
                    }
                    items.Add(copy);
                    continue;
                }

                var nested = item as IAbstractMarkupDataContainer;
                if (nested != null) AppendCollapsed(nested, items, run);
            }
        }

        private void Flush(List<IAbstractMarkupData> items, StringBuilder run)
        {
            if (run.Length == 0) return;
            items.Add(Text(run.ToString()));
            run.Clear();
        }

        // ---- the source as the developer wrote it ------------------------------------------

        /// <summary>
        /// The restored source: the pattern as the file carried it, from the unit context, with
        /// the filter's own tags where they stood in it. A memory updated from the restored
        /// file then matches the message as a new project's source carries it; the hoisted
        /// source of 2.0.0 matched nothing (Project 59, 12 October 2026). The pattern is
        /// readable, each tag as its native text, so the tags are found by that text, longest
        /// first, an end closing the innermost open pair. The clones come from the expanded
        /// source, one per tag id in document order, so a second pair with the same text keeps
        /// its own id. A unit without the pattern collapses as the target does.
        /// </summary>
        private List<IAbstractMarkupData> OriginalSource(IParagraphUnit unit)
        {
            var pattern = IcuMetadata(unit, "icu:pattern");
            if (pattern == null) return Collapsed(unit.Source);

            var tags = FilterTagsOf(unit.Source);
            if (tags.Count == 0) return new List<IAbstractMarkupData> { Text(pattern) };

            var used = new HashSet<IAbstractMarkupData>();
            var levels = new Stack<OpenPair>();
            var level = new OpenPair(null);
            var index = 0;
            while (index < pattern.Length)
            {
                int length;
                if (level.Pair != null && Matches(pattern, index, level.Pair.EndTagProperties.TagContent, out length))
                {
                    level.Close(this);
                    var closed = level;
                    level = levels.Pop();
                    level.Items.Add(closed.Pair);
                    index += length;
                    continue;
                }

                var tag = LongestTagAt(pattern, index, tags, used, out length);
                if (tag == null)
                {
                    level.Run.Append(pattern[index]);
                    index++;
                    continue;
                }

                level.Flush(this);
                index += length;
                var pair = tag as ITagPair;
                if (pair != null)
                {
                    levels.Push(level);
                    level = new OpenPair(ClonePair(pair));
                }
                else
                {
                    level.Items.Add((IAbstractMarkupData)tag.Clone());
                }
            }

            // A pair the pattern opened and never closed keeps its content, as it stands.
            level.Flush(this);
            while (level.Pair != null)
            {
                level.Close(this);
                var closed = level;
                level = levels.Pop();
                level.Flush(this);
                level.Items.Add(closed.Pair);
            }
            return level.Items;
        }

        /// <summary>One nesting level of the rebuilt source: the pair being filled, or the segment itself, with its items and open text run.</summary>
        private sealed class OpenPair
        {
            public OpenPair(ITagPair pair)
            {
                Pair = pair;
            }

            public ITagPair Pair { get; }

            public List<IAbstractMarkupData> Items { get; } = new List<IAbstractMarkupData>();

            public StringBuilder Run { get; } = new StringBuilder();

            public void Flush(IcuRestoreProcessor owner)
            {
                owner.Flush(Items, Run);
            }

            /// <summary>Moves the level's content into its pair, the open run first.</summary>
            public void Close(IcuRestoreProcessor owner)
            {
                Flush(owner);
                foreach (var item in Items)
                {
                    Pair.Add(item);
                }
                Items.Clear();
            }
        }

        /// <summary>
        /// The filter's tags under the expanded source, one per tag id in document order: tag
        /// pairs, placeholders that are not the expansion's own, and structure tags.
        /// </summary>
        private static List<IAbstractMarkupData> FilterTagsOf(IParagraph paragraph)
        {
            var tags = new List<IAbstractMarkupData>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Walk(paragraph, item =>
            {
                string id = null;
                var pair = item as ITagPair;
                var placeholder = item as IPlaceholderTag;
                var structure = item as IStructureTag;
                if (pair != null) id = pair.StartTagProperties.TagId == null ? null : pair.StartTagProperties.TagId.Id;
                else if (placeholder != null && !IsArgumentTag(placeholder)) id = placeholder.Properties.TagId == null ? null : placeholder.Properties.TagId.Id;
                else if (structure != null) id = structure.Properties.TagId == null ? null : structure.Properties.TagId.Id;
                else return;

                if (!string.IsNullOrEmpty(id) && !ids.Add(id)) return;
                tags.Add(item);
            });
            return tags;
        }

        /// <summary>
        /// The tag whose native text stands at the index, the longest where several do and the
        /// first not yet used among those with the same text, or null for an ordinary character.
        /// </summary>
        private static IAbstractMarkupData LongestTagAt(string pattern, int index, List<IAbstractMarkupData> tags,
            HashSet<IAbstractMarkupData> used, out int length)
        {
            IAbstractMarkupData best = null;
            length = 0;
            foreach (var tag in tags)
            {
                int candidate;
                if (!Matches(pattern, index, NativeTextOf(tag), out candidate)) continue;
                var longer = candidate > length;
                var fresher = candidate == length && best != null && used.Contains(best) && !used.Contains(tag);
                if (longer || fresher)
                {
                    best = tag;
                    length = candidate;
                }
            }
            if (best != null) used.Add(best);
            return best;
        }

        private static string NativeTextOf(IAbstractMarkupData tag)
        {
            var pair = tag as ITagPair;
            if (pair != null) return pair.StartTagProperties.TagContent;
            var placeholder = tag as IPlaceholderTag;
            if (placeholder != null) return placeholder.Properties.TagContent;
            var structure = tag as IStructureTag;
            return structure == null ? null : structure.Properties.TagContent;
        }

        private static bool Matches(string pattern, int index, string text, out int length)
        {
            length = 0;
            if (string.IsNullOrEmpty(text) || index + text.Length > pattern.Length) return false;
            if (string.CompareOrdinal(pattern, index, text, 0, text.Length) != 0) return false;
            length = text.Length;
            return true;
        }

        private static string IcuMetadata(IParagraphUnit unit, string key)
        {
            foreach (var context in unit.Properties.Contexts.Contexts)
            {
                if (context != null && context.ContextType == Constants.IcuContextType && context.MetaDataContainsKey(key))
                {
                    return context.GetMetaData(key);
                }
            }
            return null;
        }

        /// <summary>
        /// The unit's own context, copied with the state set to restored and the forms' states
        /// recorded. Copied, never changed in place: the SDLXLIFF reader hands every unit in a
        /// group the same context objects, as the expansion writer found (Phase 1 Studio run).
        /// </summary>
        private void MarkRestored(IParagraphUnit unit, List<SegmentState> states)
        {
            var contexts = (IContextProperties)unit.Properties.Contexts.Clone();
            for (var i = 0; i < contexts.Contexts.Count; i++)
            {
                var context = contexts.Contexts[i];
                if (context == null || context.ContextType != Constants.IcuContextType) continue;

                var copy = (IContextInfo)context.Clone();
                copy.SetMetaData(Constants.StateKey, Constants.StateRestored);
                copy.SetMetaData(Constants.SegmentStatesKey, SegmentStates.Serialise(states));
                contexts.Contexts[i] = copy;
            }
            unit.Properties.Contexts = contexts;
        }

        private IText Text(string value)
        {
            return ItemFactory.CreateText(PropertiesFactory.CreateTextProperties(value));
        }

        /// <summary>
        /// Every locked span inside the segment becomes a bare placeholder tag carrying its
        /// text, so a message left laid out over a mismatch is editable whatever an earlier
        /// release left in it. Inside a segment a locked span can only be a placeholder; the
        /// selector syntax sits between segments. A locked span that already holds a tag is
        /// left as it is.
        /// </summary>
        private void RestorePlaceholders(IAbstractMarkupDataContainer container)
        {
            for (var i = 0; i < container.Count; i++)
            {
                var item = container[i];

                var locked = item as ILockedContent;
                if (locked != null)
                {
                    if (!ContainsTag(locked.Content))
                    {
                        Replace(container, i, Tag(RawValueReconstruction.Reconstruct(locked.Content).RawValue));
                    }
                    continue;
                }

                var nested = item as IAbstractMarkupDataContainer;
                if (nested != null && !(item is IPlaceholderTag)) RestorePlaceholders(nested);
            }
        }

        private IPlaceholderTag Tag(string syntax)
        {
            var properties = PropertiesFactory.CreatePlaceholderTagProperties(syntax);
            properties.DisplayText = syntax;
            properties.SegmentationHint = SegmentationHint.Include;
            properties.SetMetaData(Constants.ArgumentTagKey, "true");
            return ItemFactory.CreatePlaceholderTag(properties);
        }

        private static bool ContainsTag(IAbstractMarkupDataContainer container)
        {
            for (var i = 0; i < container.Count; i++)
            {
                if (container[i] is IPlaceholderTag) return true;
                var nested = container[i] as IAbstractMarkupDataContainer;
                if (nested != null && ContainsTag(nested)) return true;
            }
            return false;
        }

        private static void Replace(IAbstractMarkupDataContainer container, int index, IAbstractMarkupData item)
        {
            container.RemoveAt(index);
            container.Insert(index, item);
        }

        private static bool PlaceholdersMatch(ISegment source, ISegment target, out string detail)
        {
            var sourceKeys = Counts(PlaceableKeys(source));
            var targetKeys = Counts(PlaceableKeys(target));

            var missing = sourceKeys.Count(pair => !targetKeys.ContainsKey(pair.Key) || targetKeys[pair.Key] < pair.Value);
            var extra = targetKeys.Count(pair => !sourceKeys.ContainsKey(pair.Key) || sourceKeys[pair.Key] < pair.Value);

            if (missing == 0 && extra == 0)
            {
                detail = string.Empty;
                return true;
            }

            detail = (missing > 0 ? missing + " source placeholder(s) missing from the target" : string.Empty)
                + (missing > 0 && extra > 0 ? "; " : string.Empty)
                + (extra > 0 ? extra + " placeholder(s) in the target with no source counterpart" : string.Empty);
            return false;
        }

        private static Dictionary<string, int> Counts(IEnumerable<string> keys)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
            }
            return counts;
        }

        /// <summary>
        /// The parity keys of a segment's protected spans: the syntax text, whichever construct
        /// carries it, so a tag in the target matches a locked span in the source and the other
        /// way round.
        /// </summary>
        private static IEnumerable<string> PlaceableKeys(ISegment segment)
        {
            var keys = new List<string>();
            Walk(segment, item =>
            {
                var syntax = SyntaxOf(item);
                if (syntax != null) keys.Add(syntax);
            });
            return keys;
        }

        /// <summary>
        /// Records a finding on the target segment it concerns, as a reviewer would: a comment
        /// marker around the segment's content, by the plugin's author name, which the next run
        /// removes before it checks again. The translator's own comments stay.
        /// </summary>
        private void AddTargetComment(ISegment target, string text, Severity severity)
        {
            var comment = PropertiesFactory.CreateComment(text, Constants.CommentAuthor, severity);
            comment.Date = DateTime.Now;
            comment.DateSpecified = true;

            var properties = PropertiesFactory.CreateCommentProperties();
            properties.Add(comment);

            var marker = ItemFactory.CreateCommentMarker(properties);
            var content = ItemsOf(target);
            target.Clear();
            foreach (var item in content)
            {
                marker.Add(item);
            }
            target.Add(marker);
        }
    }
}
