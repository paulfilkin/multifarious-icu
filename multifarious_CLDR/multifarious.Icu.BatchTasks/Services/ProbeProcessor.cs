using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// Spike-stage observer: writes what Studio's filter produced for each paragraph unit to the
    /// diagnostics log and changes nothing. What it records decides the SDLXLIFF layout: whether
    /// the JSON and Java Resources filters leave an ICU value as one plain text segment or tag
    /// its braces, which context types carry the resource key, and what an untouched unit looks
    /// like before the expansion writer has to produce one that Studio accepts as its own.
    /// </summary>
    public class ProbeProcessor : AbstractBilingualContentProcessor
    {
        public int Units { get; private set; }

        public override void ProcessParagraphUnit(IParagraphUnit paragraphUnit)
        {
            if (paragraphUnit != null && !paragraphUnit.IsStructure)
            {
                Units++;
                Diagnostics.Write(Describe(paragraphUnit));
            }

            base.ProcessParagraphUnit(paragraphUnit);
        }

        private static string Describe(IParagraphUnit unit)
        {
            var text = new StringBuilder();
            text.Append("  unit ").Append(unit.Properties.ParagraphUnitId.Id);

            var contexts = unit.Properties.Contexts != null ? unit.Properties.Contexts.Contexts : null;
            if (contexts != null)
            {
                foreach (var context in contexts)
                {
                    text.Append(" [ctx ").Append(context.ContextType)
                        .Append(" code=").Append(context.DisplayCode)
                        .Append(" desc=").Append(context.Description);
                    foreach (var pair in context.MetaData)
                    {
                        text.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
                    }
                    text.Append(']');
                }
            }

            text.Append(" segments=").Append(unit.SegmentPairs.Count());

            var items = unit.Source.AllSubItems.ToList();
            text.Append(" text=").Append(items.OfType<IText>().Count())
                .Append(" ph=").Append(items.OfType<IPlaceholderTag>().Count())
                .Append(" tagpairs=").Append(items.OfType<ITagPair>().Count())
                .Append(" locked=").Append(items.OfType<ILockedContent>().Count());

            text.Append(" source=\"");
            foreach (var item in items)
            {
                var plain = item as IText;
                if (plain != null)
                {
                    text.Append(plain.Properties.Text);
                    continue;
                }

                var placeholder = item as IPlaceholderTag;
                if (placeholder != null)
                {
                    text.Append("<ph ").Append(placeholder.Properties.TagContent).Append('>');
                }
            }
            text.Append('"');

            return text.ToString();
        }
    }
}
