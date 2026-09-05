using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Icu.Cldr;
using Icu.Core;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>A finalise check failed under a fail-task setting: the run stops, naming the unit.</summary>
    public sealed class FinaliseFailedException : Exception
    {
        public FinaliseFailedException(string paragraphUnitId, string reason)
            : base("Paragraph unit " + paragraphUnitId + ": " + reason)
        {
            ParagraphUnitId = paragraphUnitId;
        }

        public string ParagraphUnitId { get; }
    }

    /// <summary>
    /// The finalise task's engine (design 8.6), as a bilingual content processor. For every
    /// paragraph unit carrying this plugin's context: prune the branches of category-expanded
    /// selectors to the target language's exact set, both paragraphs in step; fill an empty
    /// target segment from the source; escape ICU special characters typed as target text; and
    /// check that every protected span in the source is in the target. Anything done to a
    /// segment beyond pruning and escaping is recorded as a comment on it.
    ///
    /// The layout is read from the content itself. The syntax between segments is locked text
    /// (or a placeholder tag under the variant) whose text is one of three shapes: a selector
    /// open, a branch open, or a close brace. That grammar is unambiguous for the writer's
    /// output, and which selectors were category expanded is in the unit context.
    ///
    /// Escaping is unescape-then-escape, so running finalise twice, or after a translator has
    /// already typed ICU escapes, produces the same file: an apostrophe typed as one or as two
    /// comes out as two either way.
    /// </summary>
    public class IcuFinaliseProcessor : AbstractBilingualContentProcessor
    {
        private static readonly Regex SelectorOpen =
            new Regex(@"^\{(?<arg>[^,{}\s]+),\s*(?<kind>plural|selectordinal|select),", RegexOptions.CultureInvariant);

        private static readonly Regex BranchOpen =
            new Regex(@"^\s*(?<key>\S+)\s*\{$", RegexOptions.CultureInvariant);

        private static readonly HashSet<string> CategoryKeywords =
            new HashSet<string>(StringComparer.Ordinal) { "zero", "one", "two", "few", "many", "other" };

        private readonly string _targetLanguageTag;
        private readonly FinaliseOptions _options;
        private readonly CldrPlurals _plurals = CldrPlurals.Default;
        private readonly List<ExpansionWarning> _warnings = new List<ExpansionWarning>();

        private string _fileTargetLanguage;
        private Dictionary<SelectorKind, HashSet<string>> _keepSets;

        public IcuFinaliseProcessor(string targetLanguageTag, FinaliseOptions options)
        {
            _targetLanguageTag = targetLanguageTag;
            _options = options ?? FinaliseOptions.Default;
        }

        /// <summary>Units carrying the plugin's context.</summary>
        public int Units { get; private set; }

        /// <summary>Branch triples removed.</summary>
        public int Pruned { get; private set; }

        /// <summary>Target segments filled from the source.</summary>
        public int Filled { get; private set; }

        public IReadOnlyList<ExpansionWarning> Warnings { get { return _warnings; } }

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
            if (paragraphUnit != null && !paragraphUnit.IsStructure && paragraphUnit.Source != null
                && paragraphUnit.Target != null && ResourceKey.IsExpanded(paragraphUnit))
            {
                Finalise(paragraphUnit);
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

        private void Finalise(IParagraphUnit unit)
        {
            Units++;
            var unitId = unit.Properties.ParagraphUnitId.Id;
            var expandedPaths = ExpandedSelectorPaths(unit);

            // 1. Prune both paragraphs with identical decisions; they are mirrored, so the same
            // branches disappear from each.
            var inPluralContext = false;
            Pruned += Prune(unit.Source, expandedPaths, ref inPluralContext);
            Prune(unit.Target, expandedPaths, ref inPluralContext);

            // 2. Per remaining segment pair: fill empty targets from the source, escape target
            // text, and check placeholder parity. Filling precedes escaping so copied source
            // content is escaped like everything else.
            var sourceSegments = SegmentsOf(unit.Source);
            var targetSegments = SegmentsOf(unit.Target);

            for (var index = 0; index < targetSegments.Count; index++)
            {
                var target = targetSegments[index];
                var source = index < sourceSegments.Count ? sourceSegments[index] : null;
                var reasons = new List<string>();

                if (source != null && IsEmpty(target))
                {
                    if (_options.OnEmptyBranch == EmptyBranchBehaviour.FailTask)
                    {
                        throw new FinaliseFailedException(unitId,
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
                        throw new FinaliseFailedException(unitId,
                            "Target segment " + target.Properties.Id.Id + ": " + parityDetail);
                    }

                    reasons.Add("Placeholder mismatch: " + parityDetail);
                }

                if (reasons.Count > 0)
                {
                    AddWarningComment(target, string.Join("\n", reasons));
                    foreach (var reason in reasons)
                    {
                        _warnings.Add(new ExpansionWarning(unitId, "Segment " + target.Properties.Id.Id + ": " + reason));
                    }
                }
            }

            Diagnostics.Write("  unit " + unitId + ": finalised, segments=" + targetSegments.Count);
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

                var content = new List<IAbstractMarkupData>();
                if (items[index] is ISegment)
                {
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

        private static bool IsEmpty(ISegment segment)
        {
            var empty = true;
            Walk(segment, item =>
            {
                var text = item as IText;
                if (text != null && !string.IsNullOrWhiteSpace(text.Properties.Text)) empty = false;
                if (item is ILockedContent || item is IPlaceholderTag) empty = false;
            });
            return empty;
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
        /// Fresh copies of the source segment's translatable content: text and protected spans
        /// rebuilt, comment wrappers not carried over.
        /// </summary>
        private void FillFromSource(ISegment source, ISegment target)
        {
            var copies = new List<IAbstractMarkupData>();
            Walk(source, item =>
            {
                var text = item as IText;
                if (text != null)
                {
                    copies.Add(ItemFactory.CreateText(PropertiesFactory.CreateTextProperties(text.Properties.Text)));
                    return;
                }

                var locked = item as ILockedContent;
                if (locked != null)
                {
                    var copy = ItemFactory.CreateLockedContent(
                        PropertiesFactory.CreateLockedContentProperties(locked.Properties.LockType));
                    copy.Content.Add(ItemFactory.CreateText(PropertiesFactory.CreateTextProperties(
                        RawValueReconstruction.Reconstruct(locked.Content).RawValue)));
                    copies.Add(copy);
                    return;
                }

                var tag = item as IPlaceholderTag;
                if (tag != null)
                {
                    copies.Add((IPlaceholderTag)tag.Clone());
                }
            });

            target.Clear();
            foreach (var copy in copies)
            {
                target.Add(copy);
            }
        }

        /// <summary>
        /// Escapes the target's text runs for ICU: apostrophes doubled and literal braces (and
        /// '#' inside a plural) quoted. Text inside a locked span is syntax and is never touched.
        /// Unescaped first, so the result is the same whether the translator typed ICU escapes
        /// or plain text, and whether finalise has run before.
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

        /// <summary>The parity keys of a segment's protected spans: locked spans and tags by their text, so either construct is checked.</summary>
        private static IEnumerable<string> PlaceableKeys(ISegment segment)
        {
            var keys = new List<string>();
            Walk(segment, item =>
            {
                var locked = item as ILockedContent;
                if (locked != null) keys.Add("locked:" + RawValueReconstruction.Reconstruct(locked.Content).RawValue);

                var tag = item as IPlaceholderTag;
                if (tag != null) keys.Add("tag:" + tag.Properties.TagContent);
            });
            return keys;
        }

        private void AddWarningComment(ISegment segment, string text)
        {
            var comment = PropertiesFactory.CreateComment(text, Constants.CommentAuthor, Severity.Medium);
            comment.Date = DateTime.Now;
            comment.DateSpecified = true;
            var properties = PropertiesFactory.CreateCommentProperties();
            properties.Add(comment);

            var marker = ItemFactory.CreateCommentMarker(properties);
            var content = ItemsOf(segment);
            segment.Clear();
            foreach (var item in content)
            {
                marker.Add(item);
            }
            segment.Add(marker);
        }
    }
}
