using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Icu.Cldr;
using Icu.Cldr.Rules;
using Icu.Core;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>One segment of an expanded message, as the ICU Forms view shows it.</summary>
    public sealed class IcuFormRow
    {
        public string SegmentId { get; set; }

        /// <summary>The branch path, such as <c>count:few</c> or <c>gender:female/count:many</c>; empty for a protected message.</summary>
        public string Path { get; set; }

        /// <summary>plural, selectordinal, select, or none.</summary>
        public string Selector { get; set; }

        /// <summary>The category keyword, explicit value or select key of the innermost branch.</summary>
        public string Category { get; set; }

        /// <summary>The counts that select this form, from the CLDR rules for the target language.</summary>
        public IReadOnlyList<string> Counts { get; set; }

        public bool FractionalOnly { get; set; }

        /// <summary>The count substituted for '#' in the rendered text; empty where there is none.</summary>
        public string SampleCount { get; set; }

        /// <summary>The source form this one was seeded from, where the expansion's comment says so; empty otherwise.</summary>
        public string SeededFrom { get; set; }

        public bool SyntheticSource { get; set; }

        /// <summary>The expansion's comment where there is one, else the same facts composed from the layout and CLDR, for a tooltip.</summary>
        public string Comment { get; set; }

        public string SourceRendered { get; set; }

        public string TargetRendered { get; set; }

        public bool TargetEmpty { get; set; }

        /// <summary>The parity problem, or null.</summary>
        public string PlaceholderWarning { get; set; }

        /// <summary>
        /// True where the target text carries a '#' typed as text while the source form uses the
        /// protected count marker: almost always a translator reaching for the count, and Finalise
        /// would quote it into a literal hash.
        /// </summary>
        public bool TypedPound { get; set; }
    }

    /// <summary>A whole expanded message, read from its paragraph unit.</summary>
    public sealed class IcuFormsModel
    {
        public string Key { get; set; }
        public string Pattern { get; set; }
        public string Hoisted { get; set; }
        public string TargetLanguage { get; set; }
        public IReadOnlyList<string> ExpandedSelectors { get; set; }
        public IReadOnlyList<IcuFormRow> Rows { get; set; }

        /// <summary>The target paragraph as Finalise will write it: syntax verbatim, text escaped.</summary>
        public string TargetProjection { get; set; }

        public bool TargetParses { get; set; }

        public string ParseError { get; set; }

        /// <summary>The argument the outermost plural-kind selector switches on, for the count box; null for a select-only or protected message.</summary>
        public string CountArgument { get; set; }
    }

    /// <summary>
    /// Reads an expanded paragraph unit back into rows for the ICU Forms view. The layout itself
    /// says what each segment is: the selector opens and branch opens between the segments give
    /// the path and the category, the way Finalise reads them, and the CLDR data for the target
    /// language gives the counts. The segment's comment, where the expansion wrote one, is only
    /// the tooltip, so a file expanded without comments shows the same rows. '#' and the
    /// arguments are replaced by sample values so the translator sees a sentence rather than
    /// syntax. Reads the editor's document model, never the file on disk.
    /// </summary>
    public sealed class IcuFormsReader
    {
        private const int ExampleCount = 6;

        private static readonly Regex SelectorOpen =
            new Regex(@"^\{(?<arg>[^,{}\s]+),\s*(?<kind>plural|selectordinal|select),", RegexOptions.CultureInvariant);

        private static readonly Regex BranchOpen =
            new Regex(@"^\s*(?<key>\S+)\s*\{$", RegexOptions.CultureInvariant);

        private readonly PreviewForms _forms;
        private readonly CldrPlurals _plurals;
        private readonly GrammaticalHints _hints;

        public IcuFormsReader(PreviewForms forms = null, CldrPlurals plurals = null, GrammaticalHints hints = null)
        {
            _forms = forms ?? new PreviewForms();
            _plurals = plurals ?? CldrPlurals.Default;
            _hints = hints ?? GrammaticalHints.Embedded;
        }

        /// <summary>
        /// The model, or null where the unit is not one this plugin expanded. Where the editor
        /// hands over the active segment's own target, that content stands in for the unit's copy
        /// of the same segment, in the row and in the projection: the paragraph unit the editor
        /// gives out is what was last committed, and the translator is typing into the segment.
        /// </summary>
        public IcuFormsModel Read(IParagraphUnit unit, string targetLanguageTag, ISegment activeTarget = null)
        {
            if (unit == null || unit.Source == null || unit.Target == null || !ResourceKey.IsExpanded(unit)) return null;
            var activeId = activeTarget != null && activeTarget.Properties != null && activeTarget.Properties.Id != null
                ? activeTarget.Properties.Id.Id
                : null;

            var context = unit.Properties.Contexts.Contexts.First(c => c != null && c.ContextType == Constants.IcuContextType);
            var pattern = Metadata(context, "icu:pattern");
            var hoisted = Metadata(context, "icu:hoisted");
            var expanded = Metadata(context, "icu:expandedSelectors")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            // Which segments were seeded from another source form, by segment number.
            var seededFrom = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in Metadata(context, "icu:seededFrom").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var colon = entry.IndexOf(':');
                if (colon > 0) seededFrom[entry.Substring(0, colon)] = entry.Substring(colon + 1);
            }
            var synthetic = new HashSet<string>(
                Metadata(context, "icu:syntheticSource").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

            var samples = new Dictionary<string, string>(StringComparer.Ordinal);
            var nameOrdinal = 0;
            var inPluralContext = ContainsPluralKindSyntax(unit.Target);

            var placed = PlaceSegments(unit.Source);
            var targetSegments = SegmentsOf(unit.Target);
            var rows = new List<IcuFormRow>();
            for (var index = 0; index < placed.Count; index++)
            {
                var place = placed[index];
                var target = index < targetSegments.Count ? targetSegments[index] : null;
                if (target != null && activeId != null && target.Properties.Id.Id == activeId) target = activeTarget;
                rows.Add(Row(place, target, targetLanguageTag, samples, ref nameOrdinal, inPluralContext, seededFrom, synthetic));
            }

            var projection = EscapedProjection(unit.Target, inPluralContext, activeId, activeTarget);
            IcuParseException error;
            IcuMessage parsed;
            var parses = IcuParser.TryParse(projection, out parsed, out error);

            return new IcuFormsModel
            {
                Key = ResourceKey.Of(unit) ?? Metadata(context, "icu:key"),
                Pattern = pattern,
                Hoisted = hoisted,
                TargetLanguage = targetLanguageTag ?? string.Empty,
                ExpandedSelectors = expanded,
                Rows = rows,
                TargetProjection = projection,
                TargetParses = parses,
                ParseError = parses ? null : error.Description + " (offset " + error.Position.ToString(CultureInfo.InvariantCulture) + ")",
                CountArgument = PreviewForms.OutermostPluralArgument(hoisted.Length > 0 ? hoisted : pattern),
            };
        }

        /// <summary>
        /// The rows a typed count lands on: explicit values first, then the target language's
        /// rule, as the count box of design 12.1. Empty where the count is not a number or the
        /// message has no plural-kind outermost selector.
        /// </summary>
        public IReadOnlyList<IcuFormRow> RowsFor(IcuFormsModel model, string count)
        {
            if (model == null || model.CountArgument == null || string.IsNullOrWhiteSpace(count)) return new List<IcuFormRow>();

            var message = model.Hoisted.Length > 0 ? model.Hoisted : model.Pattern;
            var resolution = _forms.ResolveCount(message, model.TargetLanguage, count.Trim());
            if (resolution == null) return new List<IcuFormRow>();

            var component = model.CountArgument + ":" + resolution.BranchKey;
            return model.Rows
                .Where(row => row.Path.Split('/').Any(part => part == component))
                .ToList();
        }

        // ---- the layout -----------------------------------------------------------------

        /// <summary>A segment and where it sits in the selector tree.</summary>
        private sealed class PlacedSegment
        {
            public ISegment Segment;
            public string Path = string.Empty;
            public string Selector = "none";
            public string Key = string.Empty;
            /// <summary>The selectors above the segment, outermost first, as they stood when it was placed.</summary>
            public List<Frame> Frames = new List<Frame>();
        }

        private sealed class Frame
        {
            public string Argument;
            public string Kind;
            public string Key;
        }

        /// <summary>
        /// Walks the writer's layout: a selector open pushes a frame, a branch open names the
        /// frame's current branch, a close brace ends the branch if one is open and the selector
        /// otherwise, and a segment takes the path of the frames above it.
        /// </summary>
        private static List<PlacedSegment> PlaceSegments(IParagraph source)
        {
            var frames = new List<Frame>();
            var placed = new List<PlacedSegment>();
            Place(source, frames, placed);
            return placed;
        }

        private static void Place(IAbstractMarkupDataContainer container, List<Frame> frames, List<PlacedSegment> placed)
        {
            for (var i = 0; i < container.Count; i++)
            {
                var item = container[i];
                var syntax = SyntaxOf(item);
                if (syntax != null)
                {
                    var open = SelectorOpen.Match(syntax);
                    if (open.Success)
                    {
                        frames.Add(new Frame { Argument = open.Groups["arg"].Value, Kind = open.Groups["kind"].Value });
                        continue;
                    }

                    if (syntax == "}")
                    {
                        if (frames.Count == 0) continue;
                        var top = frames[frames.Count - 1];
                        if (top.Key != null) top.Key = null;
                        else frames.RemoveAt(frames.Count - 1);
                        continue;
                    }

                    var branch = BranchOpen.Match(syntax);
                    if (branch.Success && frames.Count > 0)
                    {
                        frames[frames.Count - 1].Key = branch.Groups["key"].Value;
                    }
                    continue;
                }

                var segment = item as ISegment;
                if (segment == null)
                {
                    // The comment marker around a segment, or any other wrapper: the segments
                    // inside take the frames as they stand.
                    var wrapper = item as IAbstractMarkupDataContainer;
                    if (wrapper != null) Place(wrapper, frames, placed);
                    continue;
                }

                var place = new PlacedSegment { Segment = segment };
                if (frames.Count > 0)
                {
                    var innermost = frames[frames.Count - 1];
                    place.Path = string.Join("/", frames.Select(f => f.Argument + ":" + (f.Key ?? string.Empty)));
                    place.Selector = innermost.Kind;
                    place.Key = innermost.Key ?? string.Empty;
                    place.Frames = frames.Select(f => new Frame { Argument = f.Argument, Kind = f.Kind, Key = f.Key }).ToList();
                }
                placed.Add(place);
            }
        }

        private IcuFormRow Row(PlacedSegment place, ISegment target, string language, Dictionary<string, string> samples,
            ref int nameOrdinal, bool inPluralContext, Dictionary<string, string> seededFrom, HashSet<string> synthetic)
        {
            var source = place.Segment;
            var row = new IcuFormRow
            {
                SegmentId = source.Properties.Id.Id,
                Path = place.Path,
                Selector = place.Selector,
                Category = place.Key,
                SeededFrom = string.Empty,
                Comment = string.Empty,
            };

            // The counts from CLDR: the explicit value itself, the category's samples, nothing
            // for a select key.
            string hint = null;
            var counts = new List<string>();
            if (place.Key.StartsWith("=", StringComparison.Ordinal))
            {
                counts.Add(place.Key.Substring(1));
            }
            else if (place.Selector != "select" && place.Selector != "none" && !string.IsNullOrEmpty(language))
            {
                PluralCategory category;
                if (PluralCategories.TryParse(place.Key, out category))
                {
                    var kind = place.Selector == "selectordinal" ? SelectorKind.Ordinal : SelectorKind.Cardinal;
                    var resolution = _plurals.Resolve(language, kind);
                    var rule = resolution.RuleSet != null ? resolution.RuleSet.GetRule(category) : null;
                    var pluralSamples = rule != null ? rule.Samples : PluralSamples.Empty;
                    row.FractionalOnly = pluralSamples.IsFractionalOnly;
                    counts.AddRange(row.FractionalOnly
                        ? pluralSamples.TakeDecimalExamples(ExampleCount)
                        : pluralSamples.TakeIntegerExamples(ExampleCount));
                    hint = _hints.Find(resolution.ResolvedKey, category);
                }
            }
            row.Counts = counts;
            row.SampleCount = counts.FirstOrDefault() ?? string.Empty;

            // Which source form seeded this segment comes from the unit context; a file expanded
            // when the segments carried comments has it on the comment, which then also serves
            // as the tooltip. Otherwise the tooltip is composed from the same facts.
            string seed;
            row.SeededFrom = seededFrom.TryGetValue(row.SegmentId, out seed) ? seed : string.Empty;
            row.SyntheticSource = synthetic.Contains(row.SegmentId);
            var comment = FirstComment(source);
            if (comment != null)
            {
                if (comment.MetaDataContainsKey("icu:seededFrom")) row.SeededFrom = Metadata(comment, "icu:seededFrom");
                if (comment.MetaDataContainsKey("icu:syntheticSource")) row.SyntheticSource = Metadata(comment, "icu:syntheticSource") == "true";
                row.Comment = comment.Text ?? string.Empty;
            }
            else
            {
                row.Comment = ComposedComment(place, counts, row.FractionalOnly, hint, row.SeededFrom);
            }

            var pound = row.SampleCount.Length == 0 ? "#" : row.SampleCount;

            // An outer plural's '#' was rewritten by hoisting to "{argument, number}", so it
            // renders as that selector's own sample count for this row, not a fixed number.
            var bound = BoundCounts(place, language);

            row.SourceRendered = Rendered(source, pound, bound, samples, ref nameOrdinal, inPluralContext);
            row.TargetEmpty = target == null || IsEmpty(target);
            row.TargetRendered = target == null ? string.Empty : Rendered(target, pound, bound, samples, ref nameOrdinal, inPluralContext);

            // An empty target has no placeholders by definition; "not translated" is the whole
            // story and parity is only checked once there is a translation to check.
            if (target != null && !row.TargetEmpty)
            {
                row.PlaceholderWarning = ParityProblem(source, target);
                row.TypedPound = inPluralContext && PlaceableKeys(source).Contains("#") && TypedText(target).Contains("#");
            }

            return row;
        }

        private static string ComposedComment(PlacedSegment place, List<string> counts, bool fractionalOnly, string hint, string seededFrom)
        {
            if (place.Selector == "none") return string.Empty;

            var builder = new StringBuilder();
            if (place.Selector == "select")
            {
                builder.Append("Select branch: ").Append(place.Key);
            }
            else if (place.Key.StartsWith("=", StringComparison.Ordinal))
            {
                builder.Append("Exact match: ").Append(place.Key);
            }
            else
            {
                builder.Append("CLDR category: ").Append(place.Key);
                if (counts.Count > 0)
                {
                    builder.Append('\n').Append(fractionalOnly
                        ? "Used when the count is: fractional counts only, e.g. " + string.Join(", ", counts)
                        : "Used when the count is: " + string.Join(", ", counts));
                }
            }

            if (!string.IsNullOrEmpty(seededFrom))
            {
                builder.Append('\n').Append("Source form: seeded from \"").Append(seededFrom).Append('"');
            }
            if (hint != null) builder.Append('\n').Append("Grammar: ").Append(hint);
            return builder.ToString();
        }

        /// <summary>The target's text runs as the translator typed them, with ICU escapes undone.</summary>
        private static string TypedText(ISegment segment)
        {
            var builder = new StringBuilder();
            Walk(segment, item =>
            {
                var text = item as IText;
                if (text != null) builder.Append(IcuEscaping.Unescape(text.Properties.Text, true));
            });
            return builder.ToString();
        }

        /// <summary>
        /// The sample count for each plural or ordinal selector on the row's path, by argument
        /// name: the explicit value itself, otherwise the first CLDR example for the category.
        /// </summary>
        private Dictionary<string, string> BoundCounts(PlacedSegment place, string language)
        {
            var bound = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var frame in place.Frames)
            {
                if (frame.Kind == "select" || frame.Key == null) continue;
                if (frame.Key.StartsWith("=", StringComparison.Ordinal))
                {
                    bound[frame.Argument] = frame.Key.Substring(1);
                    continue;
                }

                PluralCategory category;
                if (string.IsNullOrEmpty(language) || !PluralCategories.TryParse(frame.Key, out category)) continue;
                var kind = frame.Kind == "selectordinal" ? SelectorKind.Ordinal : SelectorKind.Cardinal;
                var ruleSet = _plurals.Resolve(language, kind).RuleSet;
                var rule = ruleSet != null ? ruleSet.GetRule(category) : null;
                var pluralSamples = rule != null ? rule.Samples : PluralSamples.Empty;
                var first = (pluralSamples.IsFractionalOnly
                    ? pluralSamples.TakeDecimalExamples(1)
                    : pluralSamples.TakeIntegerExamples(1)).FirstOrDefault();
                if (first != null) bound[frame.Argument] = first;
            }
            return bound;
        }

        /// <summary>
        /// The segment as a sentence: text runs as the translator sees them (typed escapes
        /// undone), '#' as the sample count, an outer selector's rewritten '#' as that
        /// selector's count for the row, and other arguments as sample values that stay the
        /// same across the rows of one message.
        /// </summary>
        private static string Rendered(ISegment segment, string pound, Dictionary<string, string> bound, Dictionary<string, string> samples, ref int nameOrdinal, bool inPluralContext)
        {
            var builder = new StringBuilder();
            var ordinal = nameOrdinal;
            Walk(segment, item =>
            {
                var text = item as IText;
                if (text != null)
                {
                    builder.Append(IcuEscaping.Unescape(text.Properties.Text, inPluralContext));
                    return;
                }

                var syntax = SyntaxOf(item);
                if (syntax == null) return;

                if (syntax == "#")
                {
                    builder.Append(pound);
                    return;
                }

                string count;
                if (bound.TryGetValue(ArgumentName(syntax), out count))
                {
                    builder.Append(count);
                    return;
                }

                builder.Append(SampleFor(syntax, samples, ref ordinal));
            });
            nameOrdinal = ordinal;
            return builder.ToString();
        }

        /// <summary>The argument name of a protected span's syntax: "amount" for <c>{amount, number, ::currency/EUR}</c>.</summary>
        private static string ArgumentName(string syntax)
        {
            var inner = syntax.Trim();
            if (inner.StartsWith("{", StringComparison.Ordinal) && inner.EndsWith("}", StringComparison.Ordinal))
            {
                inner = inner.Substring(1, inner.Length - 2);
            }
            return inner.Split(',')[0].Trim();
        }

        /// <summary>A sample for a protected argument such as <c>{name}</c> or <c>{amount, number, ::currency/EUR}</c>, one per argument name per message.</summary>
        private static string SampleFor(string syntax, Dictionary<string, string> samples, ref int ordinal)
        {
            var inner = syntax.Trim();
            if (inner.StartsWith("{", StringComparison.Ordinal) && inner.EndsWith("}", StringComparison.Ordinal))
            {
                inner = inner.Substring(1, inner.Length - 2);
            }

            var parts = inner.Split(',');
            var name = parts[0].Trim();
            var type = parts.Length > 1 ? parts[1].Trim() : null;

            string sample;
            if (!samples.TryGetValue(name, out sample))
            {
                sample = PreviewForms.SampleFor(type, ref ordinal);
                samples[name] = sample;
            }
            return sample;
        }

        /// <summary>The target paragraph as Finalise will write it, so it can be parsed as ICU while the translator types.</summary>
        private static string EscapedProjection(IParagraph target, bool inPluralContext, string activeId, ISegment activeTarget)
        {
            var builder = new StringBuilder();
            WalkReplacing(target, activeId, activeTarget, item =>
            {
                var text = item as IText;
                if (text != null)
                {
                    builder.Append(IcuEscaping.Escape(IcuEscaping.Unescape(text.Properties.Text, inPluralContext), inPluralContext));
                    return;
                }

                var syntax = SyntaxOf(item);
                if (syntax != null) builder.Append(syntax);
            });
            return builder.ToString();
        }

        private static bool ContainsPluralKindSyntax(IParagraph paragraph)
        {
            var found = false;
            Walk(paragraph, item =>
            {
                var syntax = SyntaxOf(item);
                if (syntax != null && (syntax.Contains(", plural,") || syntax.Contains(", selectordinal,"))) found = true;
            });
            return found;
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

        private static string ParityProblem(ISegment source, ISegment target)
        {
            var sourceKeys = PlaceableKeys(source);
            var targetKeys = PlaceableKeys(target);
            var missing = sourceKeys.Except(targetKeys).ToList();
            var extra = targetKeys.Except(sourceKeys).ToList();
            if (missing.Count == 0 && extra.Count == 0) return null;

            var parts = new List<string>();
            if (missing.Count > 0) parts.Add("missing " + string.Join(", ", missing));
            if (extra.Count > 0) parts.Add("extra " + string.Join(", ", extra));
            return string.Join("; ", parts);
        }

        private static List<string> PlaceableKeys(ISegment segment)
        {
            var keys = new List<string>();
            Walk(segment, item =>
            {
                var syntax = SyntaxOf(item);
                if (syntax != null) keys.Add(syntax);
            });
            return keys;
        }

        /// <summary>The expansion's comment: the marker inside the segment, wrapping its content, or one around the segment.</summary>
        private static IComment FirstComment(ISegment segment)
        {
            var marker = segment.Parent as ICommentMarker;
            if (marker == null) marker = segment.Count > 0 ? segment[0] as ICommentMarker : null;
            return marker != null && marker.Comments != null && marker.Comments.Count > 0 ? marker.Comments.GetItem(0) : null;
        }

        private static string Metadata(IMetaDataContainer container, string key)
        {
            return container != null && container.MetaDataContainsKey(key) ? container.GetMetaData(key) ?? string.Empty : string.Empty;
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

        /// <summary>Visits every leaf under a container, through markers, not into locked content.</summary>
        private static void Walk(IAbstractMarkupDataContainer container, Action<IAbstractMarkupData> visit)
        {
            WalkReplacing(container, null, null, visit);
        }

        /// <summary>The same walk, with one segment's content taken from elsewhere: the editor's live copy of the active segment.</summary>
        private static void WalkReplacing(IAbstractMarkupDataContainer container, string replaceId, ISegment replacement, Action<IAbstractMarkupData> visit)
        {
            for (var i = 0; i < container.Count; i++)
            {
                var item = container[i];
                var segment = item as ISegment;
                if (segment != null && replaceId != null && !ReferenceEquals(segment, replacement)
                    && segment.Properties != null && segment.Properties.Id.Id == replaceId)
                {
                    WalkReplacing(replacement, null, null, visit);
                    continue;
                }

                visit(item);
                if (item is ILockedContent) continue;

                var nested = item as IAbstractMarkupDataContainer;
                if (nested != null) WalkReplacing(nested, replaceId, replacement, visit);
            }
        }
    }
}
