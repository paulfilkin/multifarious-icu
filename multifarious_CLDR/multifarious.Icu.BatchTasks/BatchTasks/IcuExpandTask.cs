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
    // Binds the settings group and its page to the task: Studio shows the page under the task's
    // name in the batch task wizard and the project settings, and stores the group in the
    // project's settings bundle.
    [RequiresSettings(typeof(IcuExpandSettings), typeof(IcuExpandSettingsPage))]
    public class IcuExpandTask : AbstractFileContentProcessingAutomaticTask
    {
        private readonly List<ProcessedFile> _processed = new List<ProcessedFile>();
        private ExpansionOptions _options;
        private GrammarDictionary _dictionary;
        private string _dictionaryName;
        private string _dictionaryFailure;
        private string _sourceLanguage;
        private int _filesSeen;
        private int _filesProcessed;

        /// <summary>A file the task ran over and the processor that did it, for the report.</summary>
        private sealed class ProcessedFile
        {
            public ProjectFile File;
            public IcuExpandProcessor Processor;
        }

        protected override void OnInitializeTask()
        {
            // The settings Studio stores in the project; a project that has never seen the page
            // yields the design's defaults.
            var settings = GetSetting<IcuExpandSettings>();
            _options = settings.ToOptions();

            // The grammar dictionary: the shipped default with the project's imported file over
            // it. A file that no longer validates is reported once and the default runs.
            _dictionary = settings.ToDictionary(out _dictionaryFailure);
            _dictionaryName = settings.HasGrammarDictionaryFile && _dictionaryFailure == null
                ? settings.GrammarDictionaryName
                : null;
            if (_dictionaryFailure != null)
            {
                _dictionaryFailure = string.Format(CultureInfo.CurrentCulture, UIStrings.Expand_GrammarInvalid, _dictionaryFailure);
            }

            var info = Project != null ? Project.GetProjectInfo() : null;
            _sourceLanguage = info != null && info.SourceLanguage != null && info.SourceLanguage.CultureInfo != null
                ? info.SourceLanguage.CultureInfo.Name
                : null;

            Diagnostics.Start("expand task");
            Diagnostics.Write("project=" + (info != null ? info.Name : "<null>")
                + " source=" + (_sourceLanguage ?? "<null>")
                + " files=" + (TaskFiles != null ? TaskFiles.Length : 0));
            Diagnostics.Write("settings: cardinal=" + _options.ExpandCardinal + " ordinal=" + _options.ExpandOrdinal
                + " seed=" + _options.SourceSeedStrategy + " lock=" + _options.LockPlaceholders
                + " budget=" + _options.MaxUnitsPerMessage + " parseError=" + _options.OnParseError
                + " dictionary=" + (_dictionaryName ?? "default") + (_dictionaryFailure != null ? " (" + _dictionaryFailure + ")" : string.Empty));
        }

        /// <summary>
        /// Only files of the two proven file types. A project can hold anything, and a brace in
        /// a Word document is not ICU. The pair is fixed rather than a setting: a file type that
        /// has not been run end to end is not known to work, so new ones join by a change here
        /// once they have been (Paul, 6 September 2026).
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
        /// Puts the processor into the converter Studio hands the task. Studio parses the file
        /// through every processor the tasks of the pass added, in task order, writes the result
        /// to a temporary file, and keeps it over the project's file when
        /// <see cref="OnFileComplete"/> says so (Studio's ContentProcessingTaskImplementation,
        /// decompiled 11 October 2026). Adjacent content-processing tasks in a sequence share
        /// one pass, so Pseudo-translate placed after this task runs over the expanded forms.
        ///
        /// Until 2.0 the task rewrote the file itself and ignored this converter, on the YAML
        /// plugin's finding that the converter changed nothing on disk. That finding was the
        /// default OnFileComplete, which returns false. Alone, the rewrite held; followed by a
        /// task that returns true, Studio's output, parsed before the rewrite, won (Project 51).
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

            var processor = new IcuExpandProcessor(_sourceLanguage, targetLanguage, _options, AppVersion, _dictionary);
            _processed.Add(new ProcessedFile { File = projectFile, Processor = processor });
            converter.AddBilingualProcessor(processor);

            // The warnings reach the Task Results window through the same converter, read once
            // the processor has seen the whole document. A dictionary file that failed to load
            // is said once, with the first file. The grammar notes go in at Note level: the
            // unit was expanded, and the translator reads them on the form as well.
            var dictionaryFailure = _processed.Count == 1 && _dictionaryFailure != null
                ? new[] { _dictionaryFailure }
                : new string[0];
            var origin = TaskMessageRelay.OriginFor("Task_Expand_Name");
            converter.AddBilingualProcessor(new TaskMessageRelay(origin, projectFile.Name,
                () => dictionaryFailure.Concat(processor.Warnings.Select(w => w.Reason)), ErrorLevel.Warning));
            converter.AddBilingualProcessor(new TaskMessageRelay(origin, projectFile.Name,
                () => processor.Notes.Select(n => n.Reason).Distinct(), ErrorLevel.Note));
            Diagnostics.Write("expand: " + projectFile.Name + " processor added to the task's converter");
        }

        /// <summary>Keep the converter's output: it is the expanded file. A processor failure never gets here; Studio reports it and keeps the original.</summary>
        public override bool OnFileComplete(ProjectFile projectFile, IMultiFileConverter converter)
        {
            var processed = projectFile == null ? null : _processed.LastOrDefault(item => item.File.Id == projectFile.Id);
            if (processed != null)
            {
                Diagnostics.Write("expand: " + projectFile.Name + " units=" + processed.Processor.Units
                    + " expanded=" + processed.Processor.Expanded + " kept=" + processed.Processor.Kept
                    + " warnings=" + processed.Processor.Warnings.Count + " -> keep output");
            }
            return true;
        }

        /// <summary>
        /// One report per language direction, as Studio's own tasks produce, which also gives
        /// the file its language suffix. The report XML lists every message and what happened to
        /// it; the stylesheet embedded beside it renders the XML in Studio's Reports view.
        /// </summary>
        public override void TaskComplete()
        {
            var units = 0;
            var expanded = 0;
            var alreadyExpanded = 0;
            var warnings = 0;
            foreach (var item in _processed)
            {
                units += item.Processor.Units;
                expanded += item.Processor.Expanded;
                alreadyExpanded += item.Processor.AlreadyExpanded;
                warnings += item.Processor.Warnings.Count;
            }

            Diagnostics.Write("expand task complete: seen=" + _filesSeen + " processed=" + _filesProcessed
                + " units=" + units + " expanded=" + expanded + " alreadyExpanded=" + alreadyExpanded
                + " warnings=" + warnings);

            var info = Project != null ? Project.GetProjectInfo() : null;
            foreach (var group in _processed.GroupBy(item => LanguageKey(item.File)))
            {
                var direction = group.First().File.GetLanguageDirection();
                var files = group.Select(item => new ExpandReportFile(item.File.Name, item.Processor.Outcomes)).ToList();
                var context = new ReportContext
                {
                    ProjectName = info != null ? info.Name : string.Empty,
                    SourceLanguage = direction != null && direction.SourceLanguage != null ? direction.SourceLanguage.DisplayName : (_sourceLanguage ?? string.Empty),
                    TargetLanguage = direction != null && direction.TargetLanguage != null ? direction.TargetLanguage.DisplayName : group.Key,
                    RunAt = DateTime.Now,
                    CldrVersion = CldrPlurals.Default.VersionDescription,
                    AppVersion = AppVersion,
                    GrammarDictionary = _dictionaryName ?? UIStrings.Report_GrammarDefault,
                };

                var description = string.Format(CultureInfo.CurrentCulture, UIStrings.Report_ExpandDescription,
                    group.Sum(item => item.Processor.Expanded), files.Count, group.Sum(item => item.Processor.Warnings.Count));
                var xml = TaskReportWriter.Expand(context, _options, files);

                if (direction != null)
                {
                    CreateReport(UIStrings.Report_ExpandName, description, xml, direction);
                }
                else
                {
                    CreateReport(UIStrings.Report_ExpandName, description, xml, TaskId);
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
                var version = typeof(IcuExpandTask).Assembly.GetName().Version;
                return version == null ? "0.0.0" : version.ToString(3);
            }
        }
    }
}
