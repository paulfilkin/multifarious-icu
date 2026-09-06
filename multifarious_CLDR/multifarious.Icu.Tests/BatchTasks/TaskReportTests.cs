using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.Expansion;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The report XML the tasks write and the stylesheet Studio renders it through. The rendering
/// runs the embedded stylesheet through the same XSLT engine Studio uses, with a stand-in for
/// the extension object Studio's viewer supplies, so a stylesheet that does not compile or a
/// label the XML does not carry fails here rather than in the Reports view.
/// </summary>
public class TaskReportTests
{
    /// <summary>What Studio's report viewer provides under urn:XmlReporting.</summary>
    public sealed class FakeXmlReporting
    {
        public string GetDefaultCssLinkTag() => "<link rel=\"stylesheet\" href=\"studio.css\" />";
        public string GetImagesUrl() => "images";
        public string GetResourceString(string name) => name;
    }

    private static ReportContext Context() => new()
    {
        ProjectName = "Project <44>",
        SourceLanguage = "English (United Kingdom)",
        TargetLanguage = "Arabic (Saudi Arabia)",
        RunAt = new DateTime(2026, 9, 6, 7, 18, 56),
        CldrVersion = "CLDR 47 (Unicode 16.0)",
        AppVersion = "1.0.0",
    };

    private static List<ExpandReportFile> ExpandFiles() =>
    [
        new("messages.json",
        [
            new ExpandUnitOutcome("u1", "inbox.unreadCount", ExpandOutcome.Expanded, 6, null),
            new ExpandUnitOutcome("u2", "app.greeting", ExpandOutcome.Protected, 1, null),
            new ExpandUnitOutcome("u3", "sync.status", ExpandOutcome.Walked, 4, "Expansion is over the branch budget (36 segments needed, 24 allowed)."),
            new ExpandUnitOutcome("u4", "broken & \"odd\" <key>", ExpandOutcome.PassedThrough, 0, "The value does not parse as ICU: expected '}'."),
        ]),
        new("messages.properties",
        [
            new ExpandUnitOutcome("u5", "queue.position", ExpandOutcome.Skipped, 1, null),
        ]),
    ];

    private static List<FinaliseReportFile> FinaliseFiles() =>
    [
        new("messages.json",
        [
            new FinaliseUnitOutcome("u1", "inbox.unreadCount", 6, 0, 2, ["Segment 3: The segment was untranslated; the source text was used.", "Segment 4: The segment was untranslated; the source text was used."]),
            new FinaliseUnitOutcome("u2", "party.guests", 3, 3, 0, []),
        ]),
    ];

    private static string Render(string xml)
    {
        var transform = new XslCompiledTransform();
        using (var stylesheet = XmlReader.Create(new StringReader(TaskReportWriter.Stylesheet())))
        {
            transform.Load(stylesheet);
        }

        var arguments = new XsltArgumentList();
        arguments.AddExtensionObject("urn:XmlReporting", new FakeXmlReporting());

        var output = new StringWriter();
        using (var input = XmlReader.Create(new StringReader(xml)))
        {
            transform.Transform(input, arguments, output);
        }

        return output.ToString();
    }

    [Fact]
    public void The_expand_report_carries_every_message_and_the_totals()
    {
        var xml = TaskReportWriter.Expand(Context(), ExpansionOptions.Default, ExpandFiles());
        var document = XDocument.Parse(xml);
        var task = document.Root!;

        Assert.Equal("expand", task.Attribute("name")!.Value);
        Assert.Equal("Project <44>", task.Element("taskInfo")!.Attribute("project")!.Value);
        Assert.Equal("2", task.Element("taskInfo")!.Attribute("files")!.Value);

        var totals = task.Element("totals")!;
        Assert.Equal("5", totals.Attribute("units")!.Value);
        Assert.Equal("1", totals.Attribute("expanded")!.Value);
        Assert.Equal("1", totals.Attribute("protected")!.Value);
        Assert.Equal("1", totals.Attribute("walked")!.Value);
        Assert.Equal("1", totals.Attribute("passedThrough")!.Value);
        Assert.Equal("1", totals.Attribute("skipped")!.Value);
        Assert.Equal("12", totals.Attribute("segments")!.Value);
        Assert.Equal("2", totals.Attribute("warnings")!.Value);

        var messages = task.Elements("file").SelectMany(f => f.Elements("message")).ToList();
        Assert.Equal(5, messages.Count);
        Assert.Equal("broken & \"odd\" <key>", messages[3].Attribute("key")!.Value);
        Assert.Equal("Walked", messages[2].Attribute("outcome")!.Value);

        Assert.Equal(5, task.Element("settings")!.Elements("setting").Count());
        Assert.NotEmpty(task.Element("labels")!.Elements("label"));
        Assert.All(task.Element("labels")!.Elements("label"), label => Assert.False(string.IsNullOrWhiteSpace(label.Value)));
    }

    [Fact]
    public void The_finalise_report_carries_the_warnings_per_message()
    {
        var xml = TaskReportWriter.Finalise(Context(), FinaliseOptions.Default, FinaliseFiles());
        var task = XDocument.Parse(xml).Root!;

        Assert.Equal("finalise", task.Attribute("name")!.Value);
        var totals = task.Element("totals")!;
        Assert.Equal("2", totals.Attribute("units")!.Value);
        Assert.Equal("9", totals.Attribute("segments")!.Value);
        Assert.Equal("3", totals.Attribute("pruned")!.Value);
        Assert.Equal("2", totals.Attribute("filled")!.Value);
        Assert.Equal("2", totals.Attribute("warnings")!.Value);

        var warnings = task.Descendants("warning").Select(w => w.Value).ToList();
        Assert.Equal(2, warnings.Count);
        Assert.StartsWith("Segment 3:", warnings[0]);
    }

    [Fact]
    public void The_stylesheet_renders_the_expand_report_as_studio_will()
    {
        var html = Render(TaskReportWriter.Expand(Context(), ExpansionOptions.Default, ExpandFiles()));

        Assert.Contains("studio.css", html);
        Assert.Contains("images/TradosStudio_Logo.svg", html);
        Assert.Contains("ICU Expand Plural Forms", html);
        Assert.Contains("Project &lt;44&gt;", html);
        Assert.Contains("messages.json", html);
        Assert.Contains("inbox.unreadCount", html);
        Assert.Contains("Over budget", html);
        Assert.Contains("36 segments needed", html);
        Assert.Contains("broken &amp; \"odd\" &lt;key&gt;", html);
        Assert.Contains("Already expanded", html);
        Assert.Contains("CLDR 47", html);
    }

    [Fact]
    public void The_stylesheet_renders_the_finalise_report_with_its_warnings()
    {
        var html = Render(TaskReportWriter.Finalise(Context(), FinaliseOptions.Default, FinaliseFiles()));

        Assert.Contains("ICU Finalise Messages", html);
        Assert.Contains("Forms removed", html);
        Assert.Contains("Segment 3: The segment was untranslated", html);
        Assert.DoesNotContain("No warnings.", html);

        var quiet = Render(TaskReportWriter.Finalise(Context(), FinaliseOptions.Default,
            [new FinaliseReportFile("simple.json", [new FinaliseUnitOutcome("u1", "rabbitWarning1", 6, 0, 0, [])])]));
        Assert.Contains("No warnings.", quiet);
    }

    [Fact]
    public void The_assembly_embeds_exactly_one_stylesheet()
    {
        // Studio takes the first .xsl resource of the task assembly; a second would be a coin toss.
        var names = typeof(TaskReportWriter).Assembly.GetManifestResourceNames().Where(n => n.EndsWith(".xsl")).ToList();

        Assert.Single(names);
    }
}
