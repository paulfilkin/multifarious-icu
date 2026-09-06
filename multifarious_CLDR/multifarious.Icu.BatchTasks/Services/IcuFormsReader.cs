using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

        /// <summary>The counts that select this form, as the comment records them.</summary>
        public IReadOnlyList<string> Counts { get; set; }

        public bool FractionalOnly { get; set; }

        /// <summary>The count substituted for '#' in the rendered text; empty where there is none.</summary>
        public string SampleCount { get; set; }

        public string SeededFrom { get; set; }

        public bool SyntheticSource { get; set; }

        /// <summary>The comment text the expansion wrote, for a tooltip.</summary>
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
    /// Reads an expanded paragraph unit back into rows for the ICU Forms view: the unit
    /// context for the message, each source segment's comment metadata for the form, and the
    /// segment content for the text, with '#' and the arguments replaced by sample values so
    /// the translator sees a sentence rather than syntax. Reads the editor's document model,
    /// never the file on disk, and reads the layout the way Finalise does.
    /// </summary>
    public sealed class IcuFormsReader
    {
        private readonly PreviewForms _forms;

        public IcuFormsReader(PreviewForms forms = null)
        {
            _forms = forms ?? new PreviewForms();
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

            var samples = new Dictionary<string, string>(StringComparer.Ordinal);
            var nameOrdinal = 0;
            var inPluralContext = ContainsPluralKindSyntax(unit.Target);

            var sourceSegments = SegmentsOf(unit.Source);
            var targetSegments = SegmentsOf(unit.Target);
            var rows = new List<IcuFormRow>();
            for (var index = 0; index < sourceSegments.Count; index++)
            {
                var source = sourceSegments[index];
                var target = index < targetSegments.Count ? targetSegments[index] : null;
                if (target != null && activeId != null && target.Properties.Id.Id == activeId) target = activeTarget;
                rows.Add(Row(source, target, samples, ref nameOrdinal, inPluralContext));
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

        private static IcuFormRow Row(ISegment source, ISegment target, Dictionary<string, string> samples, ref int nameOrdinal, bool inPluralContext)
        {
            var row = new IcuFormRow
            {
                SegmentId = source.Properties.Id.Id,
                Path = string.Empty,
                Selector = "none",
                Category = string.Empty,
                Counts = new List<string>(),
                SampleCount = string.Empty,
                SeededFrom = string.Empty,
                Comment = string.Empty,
            };

            var comment = FirstComment(source);
            if (comment != null)
            {
                row.Path = Metadata(comment, "icu:path");
                row.Selector = Metadata(comment, "icu:selector");
                row.Category = Metadata(comment, "icu:category");
                row.SeededFrom = Metadata(comment, "icu:seededFrom");
                row.SyntheticSource = Metadata(comment, "icu:syntheticSource") == "true";
                row.Comment = comment.Text ?? string.Empty;

                var integers = Split(Metadata(comment, "icu:exampleIntegers"));
                var decimals = Split(Metadata(comment, "icu:exampleDecimals"));
                row.FractionalOnly = integers.Count == 0 && decimals.Count > 0;
                row.Counts = row.FractionalOnly ? decimals : integers;
                row.SampleCount = row.Counts.FirstOrDefault() ?? string.Empty;
            }

            // The comment's examples are the values '#' shows: the count after the selector's
            // offset (design 5.5), which is what the CLDR rule was evaluated on.
            var pound = row.SampleCount.Length == 0 ? "#" : row.SampleCount;

            row.SourceRendered = Rendered(source, pound, samples, ref nameOrdinal, inPluralContext);
            row.TargetEmpty = target == null || IsEmpty(target);
            row.TargetRendered = target == null ? string.Empty : Rendered(target, pound, samples, ref nameOrdinal, inPluralContext);

            // An empty target has no placeholders by definition; "not translated" is the whole
            // story and parity is only checked once there is a translation to check.
            if (target != null && !row.TargetEmpty)
            {
                row.PlaceholderWarning = ParityProblem(source, target);
                row.TypedPound = inPluralContext && PlaceableKeys(source).Contains("#") && TypedText(target).Contains("#");
            }

            return row;
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
        /// The segment as a sentence: text runs as the translator sees them (typed escapes
        /// undone), '#' as the sample count, arguments as sample values that stay the same
        /// across the rows of one message.
        /// </summary>
        private static string Rendered(ISegment segment, string pound, Dictionary<string, string> samples, ref int nameOrdinal, bool inPluralContext)
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

                var locked = item as ILockedContent;
                var tag = item as IPlaceholderTag;
                var syntax = locked != null
                    ? RawValueReconstruction.Reconstruct(locked.Content).RawValue
                    : tag != null ? tag.Properties.TagContent : null;
                if (syntax == null) return;

                if (syntax == "#")
                {
                    builder.Append(pound);
                    return;
                }

                builder.Append(SampleFor(syntax, samples, ref ordinal));
            });
            nameOrdinal = ordinal;
            return builder.ToString();
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

                var locked = item as ILockedContent;
                if (locked != null)
                {
                    builder.Append(RawValueReconstruction.Reconstruct(locked.Content).RawValue);
                    return;
                }

                var tag = item as IPlaceholderTag;
                if (tag != null) builder.Append(tag.Properties.TagContent);
            });
            return builder.ToString();
        }

        private static bool ContainsPluralKindSyntax(IParagraph paragraph)
        {
            var found = false;
            Walk(paragraph, item =>
            {
                var locked = item as ILockedContent;
                var syntax = locked != null ? RawValueReconstruction.Reconstruct(locked.Content).RawValue
                    : item is IPlaceholderTag ? ((IPlaceholderTag)item).Properties.TagContent : null;
                if (syntax != null && (syntax.Contains(", plural,") || syntax.Contains(", selectordinal,"))) found = true;
            });
            return found;
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
                var locked = item as ILockedContent;
                if (locked != null) keys.Add(RawValueReconstruction.Reconstruct(locked.Content).RawValue);
                var tag = item as IPlaceholderTag;
                if (tag != null) keys.Add(tag.Properties.TagContent);
            });
            return keys;
        }

        private static IComment FirstComment(ISegment segment)
        {
            var marker = segment.Count > 0 ? segment[0] as ICommentMarker : null;
            return marker != null && marker.Comments != null && marker.Comments.Count > 0 ? marker.Comments.GetItem(0) : null;
        }

        private static string Metadata(IMetaDataContainer container, string key)
        {
            return container != null && container.MetaDataContainsKey(key) ? container.GetMetaData(key) ?? string.Empty : string.Empty;
        }

        private static List<string> Split(string value)
        {
            return value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList();
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
