using System;
using System.Linq;
using multifarious.Icu.BatchTasks.Services;
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
        private int _filesSeen;
        private int _filesProcessed;

        protected override void OnInitializeTask()
        {
            Diagnostics.Start("finalise task");
            Diagnostics.Write("project=" + (Project != null ? Project.GetProjectInfo().Name : "<null>")
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

        /// <summary>Spike stage: observes and changes nothing, as for the expand task.</summary>
        protected override void ConfigureConverter(ProjectFile projectFile, IMultiFileConverter converter)
        {
            if (projectFile == null) return;
            _filesProcessed++;

            Diagnostics.Write("configure: " + projectFile.Name);
        }

        public override void TaskComplete()
        {
            Diagnostics.Write("finalise task complete: seen=" + _filesSeen + " processed=" + _filesProcessed);
        }
    }
}
