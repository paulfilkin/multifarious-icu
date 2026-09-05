using System;
using System.Linq;
using multifarious.Icu.BatchTasks.Services;
using Sdl.FileTypeSupport.Framework.IntegrationApi;
using Sdl.ProjectAutomation.AutomaticTasks;
using Sdl.ProjectAutomation.Core;

namespace multifarious.Icu.BatchTasks.BatchTasks
{
    /// <summary>
    /// Expands every ICU plural and selectordinal message in a bilingual target file into one
    /// translatable segment per CLDR category the file's target language needs.
    ///
    /// Runs on the bilingual target files, after Copy to Target Languages, so each language gets
    /// exactly its own category set: Russian four segments, Japanese one. Placed before Analyse
    /// Files and Pre-translate in a task sequence, the word counts and matches see whole
    /// sentences rather than brace syntax.
    /// </summary>
    [AutomaticTask(
        Id = Constants.ExpandTaskId,
        Name = "Task_Expand_Name",
        Description = "Task_Expand_Description",
        GeneratedFileType = AutomaticTaskFileType.BilingualTarget,
        AllowMultiple = true)]
    // Says what the task can be run on. Without it the task is declared in plugin.xml, loads
    // without complaint, and never appears in Studio's list - there is nothing telling Studio which
    // files it applies to, so it applies to none.
    [AutomaticTaskSupportedFileType(AutomaticTaskFileType.BilingualTarget)]
    public class IcuExpandTask : AbstractFileContentProcessingAutomaticTask
    {
        private int _filesSeen;
        private int _filesProcessed;

        protected override void OnInitializeTask()
        {
            Diagnostics.Start("expand task");
            Diagnostics.Write("project=" + (Project != null ? Project.GetProjectInfo().Name : "<null>")
                + " files=" + (TaskFiles != null ? TaskFiles.Length : 0));
        }

        /// <summary>
        /// Only files of the allowlisted file types. A project can hold anything, and a brace in
        /// a Word document is not ICU.
        /// </summary>
        public override bool ShouldProcessFile(ProjectFile projectFile)
        {
            _filesSeen++;
            if (projectFile == null) return false;

            var wanted = Constants.DefaultFileTypeIds.Any(id =>
                string.Equals(projectFile.FileTypeId, id, StringComparison.OrdinalIgnoreCase));

            Diagnostics.Write("file: " + projectFile.Name
                + " fileTypeId=" + (projectFile.FileTypeId ?? "<null>")
                + " language=" + (projectFile.Language != null ? projectFile.Language.IsoAbbreviation : "<null>")
                + " path=" + (projectFile.LocalFilePath ?? "<null>")
                + " -> " + (wanted ? "process" : "skip"));

            return wanted;
        }

        /// <summary>
        /// Spike stage: observes what Studio hands the task and changes nothing. The converter
        /// Studio passes in runs its processors over the bilingual file but does not write it
        /// back (established in the YAML plugin), which makes it exactly right for a read-only
        /// probe. The expansion processor and the write-back arrive once the tasks are confirmed
        /// to register and run in Studio and the probe has shown what the filters produce.
        /// </summary>
        protected override void ConfigureConverter(ProjectFile projectFile, IMultiFileConverter converter)
        {
            if (projectFile == null) return;
            _filesProcessed++;

            Diagnostics.Write("configure: " + projectFile.Name
                + " converter=" + (converter != null ? converter.GetType().Name : "<null>"));

            if (converter != null)
            {
                converter.AddBilingualProcessor(new ProbeProcessor());
            }
        }

        public override void TaskComplete()
        {
            Diagnostics.Write("expand task complete: seen=" + _filesSeen + " processed=" + _filesProcessed);
        }
    }
}
