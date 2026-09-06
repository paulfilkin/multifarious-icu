using multifarious.Icu.BatchTasks.Services;
using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The relay that puts a task's warnings into Studio's Task Results: it reports each message
/// through the reporter the converter gives it, as soon as the converter starts on the file.
/// </summary>
public class TaskMessageRelayTests
{
    private sealed class Recording : IBilingualContentMessageReporter
    {
        public List<(ErrorLevel Level, string Origin, string Message, string Where)> Messages { get; } = [];

        public void ReportMessage(object source, string origin, ErrorLevel level, string message, TextLocation fromLocation, TextLocation uptoLocation) =>
            Messages.Add((level, origin, message, ""));

        public void ReportMessage(object source, string origin, ErrorLevel level, string message, string locationDescription) =>
            Messages.Add((level, origin, message, locationDescription));
    }

    [Fact]
    public void Each_warning_is_reported_with_the_tasks_name_and_the_file_when_the_converter_starts()
    {
        var reporter = new Recording();
        var relay = new TaskMessageRelay("ICU Finalise Messages", "messages.json.sdlxliff",
            ["Segment 2: Placeholder mismatch: 1 placeholder(s) in the target with no source counterpart", "The message is left with its placeholders as tags"],
            ErrorLevel.Warning)
        {
            MessageReporter = reporter,
        };

        relay.Initialize(ParagraphUnits.ItemFactory.CreateDocumentProperties());

        Assert.Equal(2, reporter.Messages.Count);
        Assert.All(reporter.Messages, m => Assert.Equal(ErrorLevel.Warning, m.Level));
        Assert.All(reporter.Messages, m => Assert.Equal("ICU Finalise Messages", m.Origin));
        Assert.All(reporter.Messages, m => Assert.Equal("messages.json.sdlxliff", m.Where));
        Assert.StartsWith("Segment 2: Placeholder mismatch", reporter.Messages[0].Message);
    }

    [Fact]
    public void The_origin_is_the_tasks_name_from_the_plugin_resources()
    {
        Assert.Equal("ICU Finalise Messages", TaskMessageRelay.OriginFor("Task_Finalise_Name"));
        Assert.Equal("ICU Expand Plural Forms", TaskMessageRelay.OriginFor("Task_Expand_Name"));
        Assert.Equal("Task_Unknown", TaskMessageRelay.OriginFor("Task_Unknown"));
    }
}
