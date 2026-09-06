using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Icu.Cldr;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.BatchTasks.Settings;
using multifarious.Icu.BatchTasks.Settings.Pages;
using multifarious.Icu.Expansion;
using Sdl.FileTypeSupport.Framework.IntegrationApi;
using Sdl.FileTypeSupport.Framework.NativeApi;
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
    [RequiresSettings(typeof(IcuFinaliseSettings), typeof(IcuFinaliseSettingsPage))]
    public class IcuFinaliseTask : AbstractFileContentProcessingAutomaticTask
    {
        private readonly List<ProcessedFile> _processed = new List<ProcessedFile>();
        private FinaliseOptions _options;
        private int _filesSeen;
        private int _filesProcessed;

        private sealed class ProcessedFile
        {
            public ProjectFile File;
            public IcuFinaliseProcessor Processor;
        }

        protected override void OnInitializeTask()
        {
            _options = GetSetting<IcuFinaliseSettings>().ToOptions();

            var info = Project != null ? Project.GetProjectInfo() : null;
            Diagnostics.Start("finalise task");
            Diagnostics.Write("project=" + (info != null ? info.Name : "<null>")
                + " files=" + (TaskFiles != null ? TaskFiles.Length : 0));
            Diagnostics.Write("settings: emptyBranch=" + _options.OnEmptyBranch
                + " placeholderMismatch=" + _options.OnPlaceholderMismatch);
        }

        /// <summary>Only files of the two proven file types; see the expand task.</summary>
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
            _processed.Add(new ProcessedFile { File = projectFile, Processor = processor });

            if (BilingualFileUpdater.Update(projectFile.LocalFilePath, processor, "finalise"))
            {
                Diagnostics.Write("finalise: " + projectFile.Name + " units=" + processor.Units
                    + " pruned=" + processor.Pruned + " filled=" + processor.Filled
                    + " warnings=" + processor.Warnings.Count);
            }

            // The warnings reach the Task Results window through Studio's own converter.
            if (processor.Warnings.Count > 0)
            {
                var messages = processor.Warnings.Select(w => w.Reason).ToList();
                converter.AddBilingualProcessor(new TaskMessageRelay(
                    TaskMessageRelay.OriginFor("Task_Finalise_Name"), projectFile.Name, messages, ErrorLevel.Warning));
            }
        }

        /// <summary>One report per language direction; see the expand task.</summary>
        public override void TaskComplete()
        {
            var units = 0;
            var pruned = 0;
            var filled = 0;
            var warnings = 0;
            foreach (var item in _processed)
            {
                units += item.Processor.Units;
                pruned += item.Processor.Pruned;
                filled += item.Processor.Filled;
                warnings += item.Processor.Warnings.Count;
            }

            Diagnostics.Write("finalise task complete: seen=" + _filesSeen + " processed=" + _filesProcessed
                + " units=" + units + " pruned=" + pruned + " filled=" + filled + " warnings=" + warnings);

            var info = Project != null ? Project.GetProjectInfo() : null;
            foreach (var group in _processed.GroupBy(item => LanguageKey(item.File)))
            {
                var direction = group.First().File.GetLanguageDirection();
                var files = group.Select(item => new FinaliseReportFile(item.File.Name, item.Processor.Outcomes)).ToList();
                var context = new ReportContext
                {
                    ProjectName = info != null ? info.Name : string.Empty,
                    SourceLanguage = direction != null && direction.SourceLanguage != null ? direction.SourceLanguage.DisplayName : string.Empty,
                    TargetLanguage = direction != null && direction.TargetLanguage != null ? direction.TargetLanguage.DisplayName : group.Key,
                    RunAt = DateTime.Now,
                    CldrVersion = CldrPlurals.Default.VersionDescription,
                    AppVersion = AppVersion,
                };

                var description = string.Format(CultureInfo.CurrentCulture, UIStrings.Report_FinaliseDescription,
                    group.Sum(item => item.Processor.Units), files.Count,
                    group.Sum(item => item.Processor.Pruned), group.Sum(item => item.Processor.Filled),
                    group.Sum(item => item.Processor.Warnings.Count));
                var xml = TaskReportWriter.Finalise(context, _options, files);

                if (direction != null)
                {
                    CreateReport(UIStrings.Report_FinaliseName, description, xml, direction);
                }
                else
                {
                    CreateReport(UIStrings.Report_FinaliseName, description, xml, TaskId);
                }
                Diagnostics.Write("report: " + group.Key + " files=" + files.Count);
            }
        }

        private static string LanguageKey(ProjectFile file)
        {
            return file.Language != null && !string.IsNullOrEmpty(file.Language.IsoAbbreviation)
                ? file.Language.IsoAbbreviation
                : string.Empty;
        }

        private static string AppVersion
        {
            get
            {
                var version = typeof(IcuFinaliseTask).Assembly.GetName().Version;
                return version == null ? "0.0.0" : version.ToString(3);
            }
        }
    }
}
