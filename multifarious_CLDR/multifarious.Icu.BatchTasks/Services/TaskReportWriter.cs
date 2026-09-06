using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.Expansion;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>What every report says about the run it describes.</summary>
    public sealed class ReportContext
    {
        public string ProjectName { get; set; }
        public string SourceLanguage { get; set; }
        public string TargetLanguage { get; set; }
        public DateTime RunAt { get; set; }
        public string CldrVersion { get; set; }
        public string AppVersion { get; set; }
    }

    /// <summary>One processed file and what happened in it.</summary>
    public sealed class ExpandReportFile
    {
        public ExpandReportFile(string name, IReadOnlyList<ExpandUnitOutcome> outcomes)
        {
            Name = name ?? string.Empty;
            Outcomes = outcomes ?? new List<ExpandUnitOutcome>();
        }

        public string Name { get; }
        public IReadOnlyList<ExpandUnitOutcome> Outcomes { get; }
    }

    public sealed class FinaliseReportFile
    {
        public FinaliseReportFile(string name, IReadOnlyList<FinaliseUnitOutcome> outcomes)
        {
            Name = name ?? string.Empty;
            Outcomes = outcomes ?? new List<FinaliseUnitOutcome>();
        }

        public string Name { get; }
        public IReadOnlyList<FinaliseUnitOutcome> Outcomes { get; }
    }

    /// <summary>
    /// Writes the XML behind a task report. Studio renders it through the one stylesheet
    /// embedded in this assembly (Resources\TaskReport.xsl): its report viewer takes the first
    /// <c>.xsl</c> resource of the task's assembly, and the task attribute plugins use cannot
    /// name one per task, so the stylesheet branches on the task name. Every label the
    /// stylesheet shows travels in the XML, resolved from UIStrings in the culture the task
    /// ran under, so the stylesheet itself carries no text and the report is localised with
    /// the rest of the plugin.
    /// </summary>
    public static class TaskReportWriter
    {
        public const string ExpandTaskName = "expand";
        public const string FinaliseTaskName = "finalise";

        public static string Expand(ReportContext context, ExpansionOptions options, IReadOnlyList<ExpandReportFile> files)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (files == null) throw new ArgumentNullException(nameof(files));

            return Write(writer =>
            {
                writer.WriteStartElement("task");
                writer.WriteAttributeString("name", ExpandTaskName);
                WriteTaskInfo(writer, context, files.Count);

                writer.WriteStartElement("settings");
                WriteSetting(writer, UIStrings.Expand_Cardinal, OnOff(options.ExpandCardinal));
                WriteSetting(writer, UIStrings.Expand_Ordinal, OnOff(options.ExpandOrdinal));
                WriteSetting(writer, UIStrings.Expand_SeedHeader,
                    options.SourceSeedStrategy == SourceSeedStrategy.AlwaysOther ? UIStrings.Expand_SeedAlwaysOther : UIStrings.Expand_SeedMatching);
                WriteSetting(writer, UIStrings.Expand_Hints, OnOff(options.IncludeHints));
                WriteSetting(writer, UIStrings.Expand_BudgetHeader, options.MaxUnitsPerMessage.ToString(CultureInfo.InvariantCulture));
                WriteSetting(writer, UIStrings.Expand_ParseHeader,
                    options.OnParseError == ParseErrorBehaviour.FailTask ? UIStrings.Expand_ParseFail : UIStrings.Expand_ParsePassThrough);
                writer.WriteEndElement();

                WriteLabels(writer, CommonLabels().Concat(new[]
                {
                    Label("title", UIStrings.Report_ExpandName),
                    Label("outcome_Expanded", UIStrings.Report_Expanded),
                    Label("outcome_Protected", UIStrings.Report_Protected),
                    Label("outcome_Walked", UIStrings.Report_Walked),
                    Label("outcome_PassedThrough", UIStrings.Report_PassedThrough),
                    Label("outcome_Skipped", UIStrings.Report_Skipped),
                    Label("outcome", UIStrings.Report_Outcome),
                    Label("note", UIStrings.Report_Note),
                }));

                var all = files.SelectMany(file => file.Outcomes).ToList();
                writer.WriteStartElement("totals");
                WriteExpandCounts(writer, all);
                writer.WriteEndElement();

                foreach (var file in files)
                {
                    writer.WriteStartElement("file");
                    writer.WriteAttributeString("name", file.Name);
                    WriteExpandCounts(writer, file.Outcomes);
                    foreach (var outcome in file.Outcomes)
                    {
                        writer.WriteStartElement("message");
                        writer.WriteAttributeString("key", outcome.Key);
                        writer.WriteAttributeString("outcome", outcome.Outcome.ToString());
                        writer.WriteAttributeString("segments", outcome.Segments.ToString(CultureInfo.InvariantCulture));
                        if (outcome.Detail.Length > 0) writer.WriteAttributeString("detail", outcome.Detail);
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            });
        }

        public static string Finalise(ReportContext context, FinaliseOptions options, IReadOnlyList<FinaliseReportFile> files)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (files == null) throw new ArgumentNullException(nameof(files));

            return Write(writer =>
            {
                writer.WriteStartElement("task");
                writer.WriteAttributeString("name", FinaliseTaskName);
                WriteTaskInfo(writer, context, files.Count);

                writer.WriteStartElement("settings");
                WriteSetting(writer, UIStrings.Finalise_EmptyHeader,
                    options.OnEmptyBranch == EmptyBranchBehaviour.FailTask ? UIStrings.Finalise_EmptyFail : UIStrings.Finalise_EmptyUseSource);
                WriteSetting(writer, UIStrings.Finalise_MismatchHeader,
                    options.OnPlaceholderMismatch == PlaceholderMismatchBehaviour.FailTask ? UIStrings.Finalise_MismatchFail : UIStrings.Finalise_MismatchWarn);
                writer.WriteEndElement();

                WriteLabels(writer, CommonLabels().Concat(new[]
                {
                    Label("title", UIStrings.Report_FinaliseName),
                    Label("pruned", UIStrings.Report_Pruned),
                    Label("filled", UIStrings.Report_Filled),
                    Label("noWarnings", UIStrings.Report_NoWarnings),
                }));

                var all = files.SelectMany(file => file.Outcomes).ToList();
                writer.WriteStartElement("totals");
                WriteFinaliseCounts(writer, all);
                writer.WriteEndElement();

                foreach (var file in files)
                {
                    writer.WriteStartElement("file");
                    writer.WriteAttributeString("name", file.Name);
                    WriteFinaliseCounts(writer, file.Outcomes);
                    foreach (var outcome in file.Outcomes)
                    {
                        writer.WriteStartElement("message");
                        writer.WriteAttributeString("key", outcome.Key);
                        writer.WriteAttributeString("segments", outcome.Segments.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("pruned", outcome.Pruned.ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("filled", outcome.Filled.ToString(CultureInfo.InvariantCulture));
                        foreach (var warning in outcome.Warnings)
                        {
                            writer.WriteElementString("warning", warning);
                        }
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            });
        }

        private static string Write(Action<XmlWriter> body)
        {
            // No declaration: the string goes to Studio, which stores it in whatever encoding it
            // chooses, and a writer over a string would declare utf-16 regardless (Project 45).
            // Studio's own reports carry none either.
            var builder = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = true,
            };

            using (var writer = XmlWriter.Create(builder, settings))
            {
                body(writer);
            }

            return builder.ToString();
        }

        private static void WriteTaskInfo(XmlWriter writer, ReportContext context, int fileCount)
        {
            writer.WriteStartElement("taskInfo");
            writer.WriteAttributeString("project", context.ProjectName ?? string.Empty);
            writer.WriteAttributeString("sourceLanguage", context.SourceLanguage ?? string.Empty);
            writer.WriteAttributeString("targetLanguage", context.TargetLanguage ?? string.Empty);
            // A fixed, unambiguous form: the batch task thread's culture is not the user's (it
            // printed 9/6/2026 on a British machine in Project 45), so no culture's short date
            // pattern can be trusted here.
            writer.WriteAttributeString("runAt", context.RunAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("files", fileCount.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("cldr", context.CldrVersion ?? string.Empty);
            writer.WriteAttributeString("version", context.AppVersion ?? string.Empty);
            writer.WriteEndElement();
        }

        private static void WriteSetting(XmlWriter writer, string label, string value)
        {
            writer.WriteStartElement("setting");
            writer.WriteAttributeString("label", label);
            writer.WriteAttributeString("value", value);
            writer.WriteEndElement();
        }

        private static KeyValuePair<string, string> Label(string id, string text)
        {
            return new KeyValuePair<string, string>(id, text);
        }

        private static IEnumerable<KeyValuePair<string, string>> CommonLabels()
        {
            yield return Label("summary", UIStrings.Report_Summary);
            yield return Label("project", UIStrings.Report_Project);
            yield return Label("languages", UIStrings.Report_Languages);
            yield return Label("files", UIStrings.Report_Files);
            yield return Label("createdAt", UIStrings.Report_CreatedAt);
            yield return Label("cldr", UIStrings.Report_Cldr);
            yield return Label("settings", UIStrings.Report_Settings);
            yield return Label("totals", UIStrings.Report_Totals);
            yield return Label("file", UIStrings.Report_File);
            yield return Label("messages", UIStrings.Report_Messages);
            yield return Label("segments", UIStrings.Report_Segments);
            yield return Label("warnings", UIStrings.Report_Warnings);
            yield return Label("details", UIStrings.Report_Details);
            yield return Label("key", UIStrings.Report_Key);
            yield return Label("total", UIStrings.Report_Total);
        }

        private static void WriteLabels(XmlWriter writer, IEnumerable<KeyValuePair<string, string>> labels)
        {
            writer.WriteStartElement("labels");
            foreach (var label in labels)
            {
                writer.WriteStartElement("label");
                writer.WriteAttributeString("id", label.Key);
                writer.WriteString(label.Value ?? string.Empty);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        private static void WriteExpandCounts(XmlWriter writer, IReadOnlyList<ExpandUnitOutcome> outcomes)
        {
            Count(writer, "units", outcomes.Count);
            Count(writer, "expanded", outcomes.Count(o => o.Outcome == ExpandOutcome.Expanded));
            Count(writer, "protected", outcomes.Count(o => o.Outcome == ExpandOutcome.Protected));
            Count(writer, "walked", outcomes.Count(o => o.Outcome == ExpandOutcome.Walked));
            Count(writer, "passedThrough", outcomes.Count(o => o.Outcome == ExpandOutcome.PassedThrough));
            Count(writer, "skipped", outcomes.Count(o => o.Outcome == ExpandOutcome.Skipped));
            Count(writer, "segments", outcomes.Sum(o => o.Segments));
            Count(writer, "warnings", outcomes.Count(o => o.Detail.Length > 0));
        }

        private static void WriteFinaliseCounts(XmlWriter writer, IReadOnlyList<FinaliseUnitOutcome> outcomes)
        {
            Count(writer, "units", outcomes.Count);
            Count(writer, "segments", outcomes.Sum(o => o.Segments));
            Count(writer, "pruned", outcomes.Sum(o => o.Pruned));
            Count(writer, "filled", outcomes.Sum(o => o.Filled));
            Count(writer, "warnings", outcomes.Sum(o => o.Warnings.Count));
        }

        private static void Count(XmlWriter writer, string name, int value)
        {
            writer.WriteAttributeString(name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static string OnOff(bool value)
        {
            return value ? UIStrings.Report_On : UIStrings.Report_Off;
        }

        /// <summary>The stylesheet as embedded, for the tests and for anyone rendering outside Studio.</summary>
        public static string Stylesheet()
        {
            var assembly = typeof(TaskReportWriter).Assembly;
            var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".TaskReport.xsl", StringComparison.Ordinal));
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
