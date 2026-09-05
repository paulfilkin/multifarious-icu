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

        /// <summary>Whether the unit already carries this plugin's own context, meaning it has been expanded.</summary>
        public static bool IsExpanded(IParagraphUnit unit)
        {
            if (unit == null || unit.Properties == null || unit.Properties.Contexts == null) return false;

            var contexts = unit.Properties.Contexts.Contexts;
            if (contexts == null) return false;

            foreach (var context in contexts)
            {
                if (context != null && context.ContextType == Constants.IcuContextType) return true;
            }

            return false;
        }
    }
}
