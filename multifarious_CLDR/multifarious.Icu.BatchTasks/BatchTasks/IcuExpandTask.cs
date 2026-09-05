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
        private readonly List<IcuExpandProcessor> _processors = new List<IcuExpandProcessor>();
        private ExpansionOptions _options;
        private string _sourceLanguage;
        private int _filesSeen;
        private int _filesProcessed;

        protected override void OnInitializeTask()
        {
            // Settings pages arrive in a later slice; until then the design's defaults apply.
            _options = ExpansionOptions.Default;

            var info = Project != null ? Project.GetProjectInfo() : null;
            _sourceLanguage = info != null && info.SourceLanguage != null && info.SourceLanguage.CultureInfo != null
                ? info.SourceLanguage.CultureInfo.Name
                : null;

            Diagnostics.Start("expand task");
            Diagnostics.Write("project=" + (info != null ? info.Name : "<null>")
                + " source=" + (_sourceLanguage ?? "<null>")
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
                + " -> " + (wanted ? "process" : "skip"));

            return wanted;
        }

        /// <summary>
        /// Updates the project's bilingual file, and deliberately ignores the converter handed in.
        ///
        /// A processor added to that converter runs over every unit and changes nothing on disk;
        /// the YAML plugin established this. What works is a converter of our own from the
        /// bilingual file to a new one with the processor in between, then the result put in
        /// place of the original.
        /// </summary>
        protected override void ConfigureConverter(ProjectFile projectFile, IMultiFileConverter converter)
        {
            if (projectFile == null) return;
            _filesProcessed++;

            // The language comes from the project file rather than the conversion properties. This
            // file exists because the project has that target language, whereas the conversion
            // properties have been observed to arrive empty.
            var targetLanguage = projectFile.Language != null && projectFile.Language.CultureInfo != null
                ? projectFile.Language.CultureInfo.Name
                : null;

            var processor = new IcuExpandProcessor(_sourceLanguage, targetLanguage, _options, AppVersion);
            _processors.Add(processor);

            if (BilingualFileUpdater.Update(projectFile.LocalFilePath, processor, "expand"))
            {
                Diagnostics.Write("expand: " + projectFile.Name + " units=" + processor.Units
                    + " expanded=" + processor.Expanded + " warnings=" + processor.Warnings.Count);
            }
        }

        public override void TaskComplete()
        {
            var units = 0;
            var expanded = 0;
            var alreadyExpanded = 0;
            var warnings = new List<ExpansionWarning>();
            foreach (var processor in _processors)
            {
                units += processor.Units;
                expanded += processor.Expanded;
                alreadyExpanded += processor.AlreadyExpanded;
                warnings.AddRange(processor.Warnings);
            }

            Diagnostics.Write("expand task complete: seen=" + _filesSeen + " processed=" + _filesProcessed
                + " units=" + units + " expanded=" + expanded + " alreadyExpanded=" + alreadyExpanded
                + " warnings=" + warnings.Count);

            var summary = string.Format(CultureInfo.CurrentCulture,
                "{0} ICU message(s) expanded in {1} file(s); {2} passed through with a warning.",
                expanded, _filesProcessed, warnings.Count);
            CreateReport("ICU Expand Plural Forms", summary, string.Empty, TaskId);
        }

        private static string AppVersion
        {
            get
            {
                var version = typeof(IcuExpandTask).Assembly.GetName().Version;
                return version == null ? "0.0.0" : version.ToString(3);
            }
        }
    }
}
