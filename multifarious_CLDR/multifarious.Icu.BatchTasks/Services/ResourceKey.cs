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
