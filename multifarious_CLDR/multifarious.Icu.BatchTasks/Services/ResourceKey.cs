using System.Text.RegularExpressions;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// The native resource key of a paragraph unit, read from the filter's own context metadata.
    /// Studio's JSON filter writes <c>JsonPath</c> on its paragraph context, the Java Resources
    /// filter writes <c>SDL:SpiceId</c> on its key-value context and also names the key in the
    /// context description; other filters at least describe the key in prose. Confirmed on the
    /// Phase 0 log for both filters.
    /// </summary>
    public static class ResourceKey
    {
        private static readonly string[] MetadataKeys = { "JsonPath", "SDL:SpiceId" };

        private static readonly Regex KeyNamePattern =
            new Regex("Key name=\"([^\"]+)\"", RegexOptions.CultureInvariant);

        public static string Of(IParagraphUnit unit)
        {
            if (unit == null || unit.Properties == null || unit.Properties.Contexts == null) return null;

            var contexts = unit.Properties.Contexts.Contexts;
            if (contexts == null) return null;

            foreach (var context in contexts)
            {
                if (context == null) continue;

                foreach (var key in MetadataKeys)
                {
                    var value = context.MetaDataContainsKey(key) ? context.GetMetaData(key) : null;
                    if (!string.IsNullOrEmpty(value)) return value;
                }

                if (!string.IsNullOrEmpty(context.Description))
                {
                    var match = KeyNamePattern.Match(context.Description);
                    if (match.Success) return match.Groups[1].Value;
                }
            }

            return null;
        }

        /// <summary>
        /// The key a structure unit announces for the sub-content that follows it. With an
        /// embedded content processor on the JSON filter, each value becomes its own
        /// sub-content file whose units carry no JsonPath; the enclosing file holds a structure
        /// unit with a <c>StructureParagraphUnit</c> context whose <c>StructureParagraphPath</c>
        /// is the key (Project 72, 14 October 2026). Null for any other unit.
        /// </summary>
        public static string StructurePathOf(IParagraphUnit unit)
        {
            if (unit == null || !unit.IsStructure || unit.Properties == null || unit.Properties.Contexts == null) return null;

            var contexts = unit.Properties.Contexts.Contexts;
            if (contexts == null) return null;

            foreach (var context in contexts)
            {
                if (context == null || context.ContextType != "StructureParagraphUnit") continue;
                var path = context.MetaDataContainsKey("StructureParagraphPath") ? context.GetMetaData("StructureParagraphPath") : null;
                if (!string.IsNullOrEmpty(path)) return path;
            }

            return null;
        }

        private static readonly Regex CloudSelectorOpen =
            new Regex(@"^\{[^,{}\s]+,\s*(plural|selectordinal|select),", RegexOptions.CultureInvariant);

        /// <summary>
        /// Whether the unit was laid out by the Trados Cloud app and downloaded for offline work
        /// (19 November 2026, project 6a995f7f): no context of this plugin's, the same three
        /// locked shapes between the segments, and the cloud's per-segment metadata, which the
        /// SDLXLIFF export keeps on each segment's translation origin (<c>icu:path</c> and the
        /// rest) and Studio copies onto the new origin when a segment is edited. The ICU Forms
        /// window reads such a unit; the tasks leave it alone.
        /// </summary>
        public static bool IsCloudExpanded(IParagraphUnit unit)
        {
            if (unit == null || unit.Source == null || unit.IsStructure || IcuContextOf(unit) != null) return false;

            var opens = false;
            for (var i = 0; i < unit.Source.Count; i++)
            {
                var item = unit.Source[i];
                var text = item as IText;
                if (text != null && string.IsNullOrWhiteSpace(text.Properties.Text)) continue;

                var locked = item as ILockedContent;
                if (locked == null) return false;
                opens = CloudSelectorOpen.IsMatch(RawValueReconstruction.Reconstruct(locked.Content).RawValue);
                break;
            }
            if (!opens) return false;

            foreach (var pair in unit.SegmentPairs)
            {
                var origin = pair != null && pair.Properties != null ? pair.Properties.TranslationOrigin : null;
                if (origin != null && origin.MetaDataContainsKey("icu:path")) return true;
            }

            return false;
        }

        /// <summary>The plugin's own context on the unit, or null where the unit has never been expanded.</summary>
        public static IContextInfo IcuContextOf(IParagraphUnit unit)
        {
            if (unit == null || unit.Properties == null || unit.Properties.Contexts == null) return null;

            var contexts = unit.Properties.Contexts.Contexts;
            if (contexts == null) return null;

            foreach (var context in contexts)
            {
                if (context != null && context.ContextType == Constants.IcuContextType) return context;
            }

            return null;
        }

        /// <summary>
        /// Whether the unit is laid out one segment per form: it carries this plugin's context
        /// and the state is not restored. This is the shape the restore task, the verifier and
        /// the ICU Forms window read; a restored unit is one plain segment and none of them
        /// applies to it.
        /// </summary>
        public static bool IsExpanded(IParagraphUnit unit)
        {
            var context = IcuContextOf(unit);
            return context != null && !IsRestoredState(context);
        }

        /// <summary>Whether the restore task has put the unit back together: the context is there and says so.</summary>
        public static bool IsRestored(IParagraphUnit unit)
        {
            var context = IcuContextOf(unit);
            return context != null && IsRestoredState(context);
        }

        private static bool IsRestoredState(IContextInfo context)
        {
            return context.MetaDataContainsKey(Constants.StateKey)
                && context.GetMetaData(Constants.StateKey) == Constants.StateRestored;
        }
    }
}
