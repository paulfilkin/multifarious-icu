using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.IntegrationApi;
using Sdl.ProjectAutomation.AutomaticTasks;
using Sdl.ProjectAutomation.Core;

namespace multifarious.Icu.BatchTasks.BatchTasks
{
    /// <summary>
    /// Finalises the expanded messages in a bilingual target file: prunes branches to the target
    /// language's exact CLDR category set, escapes ICU special characters the translator typed
    /// as text, checks placeholder parity and fills untranslated branches from the source with
    /// a warning.
    ///
    /// Runs after Update Main Translation Memories and before Generate Target Translations, so
    /// the translation memory stores the translator's clean text and only the generated file
    /// carries the escaping.
    /// </summary>
    [AutomaticTask(
        Id = Constants.FinaliseTaskId,
        Name = "Task_Finalise_Name",
        Description = "Task_Finalise_Description",
        GeneratedFileType = AutomaticTaskFileType.BilingualTarget,
        AllowMultiple = true)]
    [AutomaticTaskSupportedFileType(AutomaticTaskFileType.BilingualTarget)]
    public class IcuFinaliseTask : AbstractFileContentProcessingAutomaticTask
    {
        private readonly List<IcuFinaliseProcessor> _processors = new List<IcuFinaliseProcessor>();
        private FinaliseOptions _options;
        private int _filesSeen;
        private int _filesProcessed;

        protected override void OnInitializeTask()
        {
            // Settings pages arrive in a later slice; until then the design's defaults apply.
            _options = FinaliseOptions.Default;

            var info = Project != null ? Project.GetProjectInfo() : null;
            Diagnostics.Start("finalise task");
            Diagnostics.Write("project=" + (info != null ? info.Name : "<null>")
                + " files=" + (TaskFiles != null ? TaskFiles.Length : 0));
        }

        public override bool ShouldProcessFile(ProjectFile projectFile)
        {
            _filesSeen++;
            if (projectFile == null) return false;

            var wanted = Constants.DefaultFileTypeIds.Any(id =>
                string.Equals(projectFile.FileTypeId, id, StringComparison.OrdinalIgnoreCase));

            Diagnostics.Write("file: " + projectFile.Name
                + " fileTypeId=" + (projectFile.FileTypeId ?? "<null>")
                + " language=" + (projectFile.Language != null ? projectFile.Language.IsoAbbreviation : "<null>")
                + " -> " + (wanted ? "process" : "skip"));

            return wanted;
        }

        protected override void ConfigureConverter(ProjectFile projectFile, IMultiFileConverter converter)
        {
            if (projectFile == null) return;
            _filesProcessed++;

            var targetLanguage = projectFile.Language != null && projectFile.Language.CultureInfo != null
                ? projectFile.Language.CultureInfo.Name
                : null;

            var processor = new IcuFinaliseProcessor(targetLanguage, _options);
            _processors.Add(processor);

            if (BilingualFileUpdater.Update(projectFile.LocalFilePath, processor, "finalise"))
            {
                Diagnostics.Write("finalise: " + projectFile.Name + " units=" + processor.Units
                    + " pruned=" + processor.Pruned + " filled=" + processor.Filled
                    + " warnings=" + processor.Warnings.Count);
            }
        }

        public override void TaskComplete()
        {
            var units = 0;
            var pruned = 0;
            var filled = 0;
            var warnings = 0;
            foreach (var processor in _processors)
            {
                units += processor.Units;
                pruned += processor.Pruned;
                filled += processor.Filled;
                warnings += processor.Warnings.Count;
            }

            Diagnostics.Write("finalise task complete: seen=" + _filesSeen + " processed=" + _filesProcessed
                + " units=" + units + " pruned=" + pruned + " filled=" + filled + " warnings=" + warnings);

            var summary = string.Format(CultureInfo.CurrentCulture,
                "{0} ICU message(s) finalised in {1} file(s); {2} branch(es) pruned, {3} segment(s) filled from source, {4} warning(s).",
                units, _filesProcessed, pruned, filled, warnings);
            CreateReport("ICU Finalise Messages", summary, string.Empty, TaskId);
        }
    }
}
