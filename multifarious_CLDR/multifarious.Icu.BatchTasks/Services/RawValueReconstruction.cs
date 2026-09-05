using System;
using System.Text;
using Sdl.FileTypeSupport.Framework.BilingualApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>What a paragraph's content amounts to as raw native text.</summary>
    public sealed class ReconstructedValue
    {
        public ReconstructedValue(string rawValue, bool usedFilterTags)
        {
            RawValue = rawValue;
            UsedFilterTags = usedFilterTags;
        }

        /// <summary>The native value: text and tag content concatenated in document order.</summary>
        public string RawValue { get; }

        /// <summary>
        /// True where the filter had fragmented the value into its own tags. When such a value is
        /// expanded those tags are discarded, because the expansion's layout supersedes them.
        /// </summary>
        public bool UsedFilterTags { get; }
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
    /// writer will emit, which is what the finalise checks parse.
    /// </summary>
    public static class RawValueReconstruction
    {
        public static ReconstructedValue Reconstruct(IAbstractMarkupDataContainer paragraph)
        {
            if (paragraph == null) throw new ArgumentNullException(nameof(paragraph));

            var builder = new StringBuilder();
            var usedTags = false;
            Append(paragraph, builder, ref usedTags);
            return new ReconstructedValue(builder.ToString(), usedTags);
        }

        private static void Append(IAbstractMarkupDataContainer container, StringBuilder builder, ref bool usedTags)
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
                    Append(pair, builder, ref usedTags);
                    builder.Append(pair.EndTagProperties.TagContent);
                    continue;
                }

                var locked = item as ILockedContent;
                if (locked != null)
                {
                    Append(locked.Content, builder, ref usedTags);
                    continue;
                }

                var nested = item as IAbstractMarkupDataContainer;
                if (nested != null)
                {
                    Append(nested, builder, ref usedTags);
                }
            }
        }
    }
}
