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
    /// syntax as locked text between the segments; arguments and '#' inside a segment are locked
    /// text too, carrying the exact source text. Locked content is what both of Studio's writers
    /// emit verbatim: the JSON writer drops placeholder tags wherever they sit, shown first by
    /// the cloud project's T4 and again by the first Studio generation here (Project 39, 5 Sep
    /// 2026), where the target paragraph carried every tag and the generated file none.
    /// Placeholder tags remain the configuration variant; under it the tags also carry the
    /// branch path and role as metadata.
    ///
    /// Locked content carries no metadata, so each segment's comment is the machine-readable
    /// carrier: it holds the design's rendered text and, as metadata, the branch path, category,
    /// locale and examples. Comment metadata persists in SDLXLIFF and survived Analyse,
    /// Pre-translate and an editor save in the same run.
    /// </summary>
    public sealed class ExpansionWriter
    {
        private readonly IDocumentItemFactory _itemFactory;
        private readonly IPropertiesFactory _propertiesFactory;
        private readonly TagConstruct _tagConstruct;
        private readonly string _cldrVersion;
        private readonly string _appVersion;

        public ExpansionWriter(IDocumentItemFactory itemFactory, IPropertiesFactory propertiesFactory,
            TagConstruct tagConstruct, string cldrVersion, string appVersion)
        {
            if (itemFactory == null) throw new ArgumentNullException(nameof(itemFactory));
            if (propertiesFactory == null) throw new ArgumentNullException(nameof(propertiesFactory));

            _itemFactory = itemFactory;
            _propertiesFactory = propertiesFactory;
            _tagConstruct = tagConstruct;
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

            AddContext(unit, plan, resourceKey);
        }

        private sealed class WriteState
        {
            /// <summary>Segment ids are per paragraph unit in SDLXLIFF, numbered from 1.</summary>
            public int NextSegmentNumber = 1;
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

            // The open tag records whether this selector's branches were category expanded:
            // finalise prunes only expanded selectors, because a walked select or a disabled kind
            // carries the developer's branches.
            AddSyntaxPair(openText.ToString(), "selectorOpen", selector.Path, selector.IsExpanded, source, target);

            foreach (var branch in selector.Branches)
            {
                AddSyntaxPair(" " + branch.KeyText + " {", "branchOpen", branch.Path, null, source, target,
                    branch.Content as PlannedSegment);
                WriteNode(branch.Content, source, target, state);
                AddSyntaxPair("}", "branchClose", branch.Path, null, source, target);
            }

            AddSyntaxPair("}", "selectorClose", selector.Path, null, source, target);
        }

        private void WriteSegment(PlannedSegment planned, List<IAbstractMarkupData> source,
            List<IAbstractMarkupData> target, WriteState state)
        {
            var properties = _itemFactory.CreateSegmentPairProperties();
            properties.Id = new SegmentId(state.NextSegmentNumber.ToString(CultureInfo.InvariantCulture));
            state.NextSegmentNumber++;

            var sourceSegment = _itemFactory.CreateSegment(properties);
            var comment = _itemFactory.CreateCommentMarker(CommentProperties(planned));
            foreach (var item in BuildContent(planned.Nodes))
            {
                comment.Add(item);
            }
            sourceSegment.Add(comment);

            // The target segment is left empty for Copy Source to Target or pre-translation to
            // fill. It shares the pair properties with the source, which is how the framework
            // models a segment pair.
            var targetSegment = _itemFactory.CreateSegment(properties);

            source.Add(sourceSegment);
            target.Add(targetSegment);
        }

        private ICommentProperties CommentProperties(PlannedSegment planned)
        {
            var comment = _propertiesFactory.CreateComment(planned.Comment, Constants.CommentAuthor, Severity.Low);
            comment.Date = DateTime.Now;
            comment.DateSpecified = true;
            foreach (var pair in planned.Metadata)
            {
                comment.SetMetaData(pair.Key, pair.Value);
            }

            var properties = _propertiesFactory.CreateCommentProperties();
            properties.Add(comment);
            return properties;
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

        /// <summary>An argument or '#' inside a segment, in the configured construct.</summary>
        private IAbstractMarkupData ArgumentMarkup(string content)
        {
            if (_tagConstruct == TagConstruct.LockedContent)
            {
                return Locked(content);
            }

            var properties = _propertiesFactory.CreatePlaceholderTagProperties(content);
            properties.DisplayText = content;
            properties.SegmentationHint = SegmentationHint.Include;
            return _itemFactory.CreatePlaceholderTag(properties);
        }

        /// <summary>
        /// One piece of selector syntax between segments, added to both paragraphs. Each side gets
        /// its own properties object: the two paragraphs are separate documents to the writer.
        /// Where the syntax opens a branch that is a leaf, the segment's metadata rides on the tag
        /// as well as on the comment, so the branch path and category can be read from content.
        /// </summary>
        private void AddSyntaxPair(string content, string role, string path, bool? expanded,
            List<IAbstractMarkupData> source, List<IAbstractMarkupData> target, PlannedSegment leaf = null)
        {
            source.Add(SyntaxMarkup(content, role, path, expanded, leaf));
            target.Add(SyntaxMarkup(content, role, path, expanded, leaf));
        }

        private IAbstractMarkupData SyntaxMarkup(string content, string role, string path, bool? expanded,
            PlannedSegment leaf)
        {
            if (_tagConstruct == TagConstruct.LockedContent)
            {
                // Locked content carries no metadata; the segment's comment is the machine-readable
                // carrier and finalise reads the branch path from it, by position within the unit.
                return Locked(content);
            }

            var properties = _propertiesFactory.CreatePlaceholderTagProperties(content);
            properties.DisplayText = content.Trim();
            properties.SegmentationHint = SegmentationHint.Exclude;
            properties.SetMetaData("icu:role", role);
            properties.SetMetaData("icu:path", path);
            if (expanded.HasValue)
            {
                properties.SetMetaData("icu:expanded", expanded.Value ? "true" : "false");
            }

            if (leaf != null)
            {
                foreach (var pair in leaf.Metadata)
                {
                    if (pair.Key == "icu:path") continue;
                    properties.SetMetaData(pair.Key, pair.Value);
                }
            }

            return _itemFactory.CreatePlaceholderTag(properties);
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
        private void AddContext(IParagraphUnit unit, ExpansionPlan plan, string resourceKey)
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
