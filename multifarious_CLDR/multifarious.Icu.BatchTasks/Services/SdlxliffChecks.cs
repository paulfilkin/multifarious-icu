using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// Self-checks run on a bilingual file the task has written, before it replaces the project's
    /// copy. A file that fails is left on disk for inspection and the original is kept, on the
    /// same reasoning as the cloud design's invariants before upload: a broken document must not
    /// reach the next task. The first check exists because the first Studio run produced context
    /// references without definitions, which Studio's reader turns into null contexts and its TM
    /// lookup then crashes on.
    /// </summary>
    public static class SdlxliffChecks
    {
        private const string SdlNamespace = "http://sdl.com/FileTypes/SdlXliff/1.0";

        /// <summary>
        /// Every <c>sdl:cxt</c> reference in the body names a <c>cxt-def</c> in a header. Returns
        /// the ids that do not, empty when the file is sound.
        /// </summary>
        public static IReadOnlyList<string> UndefinedContextReferences(string path)
        {
            var defined = new HashSet<string>();
            var referenced = new List<string>();

            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreWhitespace = true };
            using (var stream = File.OpenRead(path))
            using (var reader = XmlReader.Create(stream, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element || reader.NamespaceURI != SdlNamespace) continue;

                    if (reader.LocalName == "cxt-def")
                    {
                        var id = reader.GetAttribute("id");
                        if (id != null) defined.Add(id);
                    }
                    else if (reader.LocalName == "cxt")
                    {
                        var id = reader.GetAttribute("id");
                        if (id != null) referenced.Add(id);
                    }
                }
            }

            var missing = new List<string>();
            foreach (var id in referenced)
            {
                if (!defined.Contains(id) && !missing.Contains(id)) missing.Add(id);
            }
            return missing;
        }
    }
}
