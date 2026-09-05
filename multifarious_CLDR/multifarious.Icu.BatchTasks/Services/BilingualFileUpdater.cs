using System;
using System.IO;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.Core.Utilities.IntegrationApi;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// Rewrites a project's bilingual file through a processor of ours.
    ///
    /// The converter Studio hands a content-processing task runs its processors over the file and
    /// changes nothing on disk (established in the YAML plugin). What works is a converter of our
    /// own from the bilingual file to a temporary one with the processor in between, then the
    /// result put in place of the original, once it has passed its self-checks.
    /// </summary>
    public static class BilingualFileUpdater
    {
        /// <summary>Returns true when the file was replaced, false when there was nothing to do.</summary>
        public static bool Update(string input, AbstractBilingualContentProcessor processor, string label)
        {
            if (processor == null) throw new ArgumentNullException(nameof(processor));

            if (string.IsNullOrEmpty(input) || !File.Exists(input))
            {
                Diagnostics.Write(label + ": no bilingual file at " + (input ?? "<null>"));
                return false;
            }

            // The extension matters: the file type manager picks the filter by it, and a temp file
            // called .tmp is not a bilingual document as far as Studio is concerned.
            var output = Path.GetTempFileName();
            var outputSdlxliff = output + ".sdlxliff";
            File.Move(output, outputSdlxliff);

            try
            {
                var manager = DefaultFileTypeManager.CreateInstance(true);
                var converter = manager.GetConverterToDefaultBilingual(input, outputSdlxliff, null);

                converter.AddBilingualProcessor(processor);
                converter.SynchronizeDocumentProperties();
                converter.Parse();

                // Only once the conversion has produced something. Replacing a project's bilingual
                // file with a half-written one would lose the translator's work outright.
                if (new FileInfo(outputSdlxliff).Length == 0)
                {
                    Diagnostics.Write(label + ": conversion produced nothing, leaving " + input + " alone");
                    return false;
                }

                // And only a file the next task can read. A failed check keeps the original, keeps
                // the rejected output beside it for inspection, and fails the task loudly rather
                // than handing Studio a document that crashes Analyse.
                var undefined = SdlxliffChecks.UndefinedContextReferences(outputSdlxliff);
                if (undefined.Count > 0)
                {
                    var rejected = input + ".rejected.sdlxliff";
                    File.Copy(outputSdlxliff, rejected, true);
                    Diagnostics.Write(label + ": self-check failed for " + input + ": context references without "
                        + "definitions: " + string.Join(",", undefined) + "; output kept at " + rejected);
                    throw new InvalidOperationException(
                        "The bilingual file failed its self-check (context references "
                        + string.Join(", ", undefined) + " have no definition); the original was kept and the "
                        + "rejected output saved as " + Path.GetFileName(rejected) + ".");
                }

                File.Delete(input);
                File.Move(outputSdlxliff, input);
                Diagnostics.Write(label + ": updated " + input);
                return true;
            }
            catch (Exception ex)
            {
                Diagnostics.Write(label + " FAILED for " + input + ": " + ex.GetType().Name + ": " + ex.Message);
                throw;
            }
            finally
            {
                if (File.Exists(outputSdlxliff)) File.Delete(outputSdlxliff);
            }
        }
    }
}
