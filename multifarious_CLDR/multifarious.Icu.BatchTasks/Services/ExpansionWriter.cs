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
    /// moved or deleted. The finalise task turns the tags back into locked text before the
    /// target file is generated.
    ///
    /// No comment is written for a form (Paul, 6 September 2026): a comment marker inside a
    /// source segment is copied into the target by Studio's pseudo-translation and Copy Source
    /// to Target (Project 46), and target comments are the translator's; a marker around the
    /// segment fails SDLXLIFF validation (Project 47); and a comment on the unit repeats what
    /// the ICU Forms window shows per row, live. The layout, the unit context and CLDR say
    /// everything the finalise task and the window need. Only warnings go on the unit.
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

        public void Write(IParagraphUnit unit, ExpansionPlan plan, string resourceKey)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var source = new List<IAbstractMarkupData>();
            var target = new List<IAbstractMarkupData>();
            var state = new WriteState();
            WriteNode(plan.Root, source, target, state);

            Refill(unit.Source, source);
            if (unit.Target != null) Refill(unit.Target, target);

            AddContext(unit, plan, resourceKey, state);
        }

        private sealed class WriteState
        {
            /// <summary>Segment ids are per paragraph unit in SDLXLIFF, numbered from 1.</summary>
            public int NextSegmentNumber = 1;

            /// <summary>The segments written, in document order, for the seeding record on the unit context.</summary>
            public List<KeyValuePair<int, PlannedSegment>> Written = new List<KeyValuePair<int, PlannedSegment>>();
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
            // finalise prunes only expanded selectors, because a walked select or a disabled kind
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
            foreach (var item in BuildContent(planned.Nodes))
            {
                sourceSegment.Add(item);
            }
            source.Add(sourceSegment);
            state.Written.Add(new KeyValuePair<int, PlannedSegment>(state.NextSegmentNumber - 1, planned));

            // The target segment is left empty for Copy Source to Target or pre-translation to
            // fill. It shares the pair properties with the source, which is how the framework
            // models a segment pair.
            target.Add(_itemFactory.CreateSegment(properties));
        }

        private IEnumerable<IAbstractMarkupData> BuildContent(IReadOnlyList<MessageNode> nodes)
        {
            var text = new StringBuilder();

            foreach (var node in nodes)
            {
                var literal = node as TextNode;
                if (literal != null)
                {
                    text.Append(literal.Value);
                    continue;
                }

                // The decoded value is the unescaping of design 5.8: the translator sees clean text.
                var quoted = node as QuotedTextNode;
                if (quoted != null)
                {
                    text.Append(quoted.Value);
                    continue;
                }

                if (text.Length > 0)
                {
                    yield return Text(text.ToString());
                    text.Clear();
                }

                var argument = node as ArgumentNode;
                if (argument != null)
                {
                    yield return ArgumentMarkup("{" + argument.Name + "}");
                    continue;
                }

                var typed = node as TypedArgumentNode;
                if (typed != null)
                {
                    yield return ArgumentMarkup(TypedArgumentText(typed));
                    continue;
                }

                if (node is PoundNode)
                {
                    yield return ArgumentMarkup("#");
                    continue;
                }

                throw new InvalidOperationException(
                    "A planned segment may not contain a '" + node.GetType().Name + "'.");
            }

            if (text.Length > 0)
            {
                yield return Text(text.ToString());
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
        /// machine-readable carriers, and finalise reads the branch path from the syntax itself.
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
        /// message, and the finalise task can find the units it owns. Context metadata persists
        /// in SDLXLIFF.
        /// </summary>
        private void AddContext(IParagraphUnit unit, ExpansionPlan plan, string resourceKey, WriteState state)
        {
            var context = _propertiesFactory.CreateContextInfo(Constants.IcuContextType);
            context.DisplayName = Constants.IcuContextDisplayName;
            context.DisplayCode = Constants.IcuContextDisplayCode;
            context.Purpose = ContextPurpose.Information;
            context.Description = string.IsNullOrEmpty(resourceKey) ? Constants.IcuContextDisplayName : resourceKey;

            context.SetMetaData("icu:pattern", plan.RawValue);
            context.SetMetaData("icu:hoisted", plan.HoistedText);
            if (!string.IsNullOrEmpty(resourceKey))
            {
                context.SetMetaData("icu:key", resourceKey);
            }
            context.SetMetaData("icu:cldrVersion", _cldrVersion);
            context.SetMetaData("icu:appVersion", _appVersion);
            context.SetMetaData("icu:unitCount", plan.Segments.Count.ToString(CultureInfo.InvariantCulture));

            // Which selectors were category expanded, by path. Locked content carries no
            // metadata, so finalise learns from here which selectors it may prune; a walked
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
}
