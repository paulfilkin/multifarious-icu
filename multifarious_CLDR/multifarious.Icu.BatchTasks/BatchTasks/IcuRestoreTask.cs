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
    /// Restores the expanded messages in a bilingual target file: prunes branches to the target
    /// language's exact CLDR category set, escapes ICU special characters the translator typed
    /// as text, checks placeholder parity, fills untranslated forms from the source with a
    /// warning, and puts each message back together as one plain segment per side, the shape
    /// Studio's writers emit as it stands.
    ///
    /// Runs whenever the translator wants the whole messages back: before Generate Target
    /// Translations, or to switch out of the expanded layout. ICU Expand on a restored file
    /// lays the forms out again with their translations and states.
    /// </summary>
    [AutomaticTask(
        Id = Constants.RestoreTaskId,
        Name = "Task_Restore_Name",
        Description = "Task_Restore_Description",
        GeneratedFileType = AutomaticTaskFileType.BilingualTarget,
        AllowMultiple = true)]
    [AutomaticTaskSupportedFileType(AutomaticTaskFileType.BilingualTarget)]
    [RequiresSettings(typeof(IcuRestoreSettings), typeof(IcuRestoreSettingsPage))]
    public class IcuRestoreTask : AbstractFileContentProcessingAutomaticTask
    {
        private readonly List<ProcessedFile> _processed = new List<ProcessedFile>();
        private RestoreOptions _options;
        private int _filesSeen;
        private int _filesProcessed;

        private sealed class ProcessedFile
        {
            public ProjectFile File;
            public IcuRestoreProcessor Processor;
        }

        protected override void OnInitializeTask()
        {
            _options = GetSetting<IcuRestoreSettings>().ToOptions();

            var info = Project != null ? Project.GetProjectInfo() : null;
            Diagnostics.Start("restore task");
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

        /// <summary>The processor goes into Studio's converter and the output is kept; see the expand task.</summary>
        protected override void ConfigureConverter(ProjectFile projectFile, IMultiFileConverter converter)
        {
            if (projectFile == null) return;
            _filesProcessed++;

            var targetLanguage = projectFile.Language != null && projectFile.Language.CultureInfo != null
                ? projectFile.Language.CultureInfo.Name
                : null;

            var processor = new IcuRestoreProcessor(targetLanguage, _options);
            _processed.Add(new ProcessedFile { File = projectFile, Processor = processor });
            converter.AddBilingualProcessor(processor);

            converter.AddBilingualProcessor(new TaskMessageRelay(
                TaskMessageRelay.OriginFor("Task_Restore_Name"), projectFile.Name,
                () => processor.Warnings.Select(w => w.Reason), ErrorLevel.Warning));
            Diagnostics.Write("restore: " + projectFile.Name + " processor added to the task's converter");
        }

        public override bool OnFileComplete(ProjectFile projectFile, IMultiFileConverter converter)
        {
            var processed = projectFile == null ? null : _processed.LastOrDefault(item => item.File.Id == projectFile.Id);
            if (processed != null)
            {
                Diagnostics.Write("restore: " + projectFile.Name + " units=" + processed.Processor.Units
                    + " restored=" + processed.Processor.Restored + " pruned=" + processed.Processor.Pruned
                    + " filled=" + processed.Processor.Filled + " warnings=" + processed.Processor.Warnings.Count
                    + " -> keep output");
            }
            return true;
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

            Diagnostics.Write("restore task complete: seen=" + _filesSeen + " processed=" + _filesProcessed
                + " units=" + units + " pruned=" + pruned + " filled=" + filled + " warnings=" + warnings);

            var info = Project != null ? Project.GetProjectInfo() : null;
            foreach (var group in _processed.GroupBy(item => LanguageKey(item.File)))
            {
                var direction = group.First().File.GetLanguageDirection();
                var files = group.Select(item => new RestoreReportFile(item.File.Name, item.Processor.Outcomes)).ToList();
                var context = new ReportContext
                {
                    ProjectName = info != null ? info.Name : string.Empty,
                    SourceLanguage = direction != null && direction.SourceLanguage != null ? direction.SourceLanguage.DisplayName : string.Empty,
                    TargetLanguage = direction != null && direction.TargetLanguage != null ? direction.TargetLanguage.DisplayName : group.Key,
                    RunAt = DateTime.Now,
                    CldrVersion = CldrPlurals.Default.VersionDescription,
                    AppVersion = AppVersion,
                };

                var description = string.Format(CultureInfo.CurrentCulture, UIStrings.Report_RestoreDescription,
                    group.Sum(item => item.Processor.Units), files.Count,
                    group.Sum(item => item.Processor.Pruned), group.Sum(item => item.Processor.Filled),
                    group.Sum(item => item.Processor.Warnings.Count));
                var xml = TaskReportWriter.Restore(context, _options, files);

                if (direction != null)
                {
                    CreateReport(UIStrings.Report_RestoreName, description, xml, direction);
                }
                else
                {
                    CreateReport(UIStrings.Report_RestoreName, description, xml, TaskId);
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
                var version = typeof(IcuRestoreTask).Assembly.GetName().Version;
                return version == null ? "0.0.0" : version.ToString(3);
            }
        }
    }
}
