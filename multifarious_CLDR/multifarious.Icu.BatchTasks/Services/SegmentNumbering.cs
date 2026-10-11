using System;
using System.Collections.Generic;
using System.Globalization;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// Numbers segments across the whole file, as Studio's own filters do. A segment id is per
    /// paragraph unit in SDLXLIFF, but the editor shows it as the segment number, so a file
    /// whose units each start again at 1 reads as 1, 1, 1, 2, 3, 4, 1, 2 down the left-hand
    /// column (Project 52, 11 October 2026). Both processors keep a running count and renumber
    /// every translatable unit they pass, touched or not, so the numbers run on through the
    /// file whatever each unit's own history.
    /// </summary>
    public static class SegmentNumbering
    {
        /// <summary>
        /// Gives the unit's segments the numbers from <paramref name="next"/> on, in document
        /// order, source and target alike, and advances <paramref name="next"/> past them. A
        /// target segment takes the number of the source segment at the same position; a target
        /// with no source counterpart keeps counting.
        /// </summary>
        public static void Renumber(IParagraphUnit unit, ref int next)
        {
            if (unit == null || unit.IsStructure || unit.Source == null) return;

            var sources = SegmentsOf(unit.Source);
            var targets = unit.Target == null ? new List<ISegment>() : SegmentsOf(unit.Target);
            var count = Math.Max(sources.Count, targets.Count);
            for (var i = 0; i < count; i++)
            {
                var id = new SegmentId(next.ToString(CultureInfo.InvariantCulture));
                if (i < sources.Count) sources[i].Properties.Id = id;
                if (i < targets.Count) targets[i].Properties.Id = id;
                next++;
            }
        }

        private static List<ISegment> SegmentsOf(IAbstractMarkupDataContainer container)
        {
            var segments = new List<ISegment>();
            for (var i = 0; i < container.Count; i++)
            {
                var segment = container[i] as ISegment;
                if (segment != null)
                {
                    segments.Add(segment);
                    continue;
                }

                if (container[i] is ILockedContent) continue;
                var nested = container[i] as IAbstractMarkupDataContainer;
                if (nested != null) segments.AddRange(SegmentsOf(nested));
            }
            return segments;
        }
    }
}
