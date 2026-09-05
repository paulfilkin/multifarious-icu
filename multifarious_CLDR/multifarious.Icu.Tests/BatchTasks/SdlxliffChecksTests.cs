using multifarious.Icu.BatchTasks.Services;

namespace multifarious.Icu.Tests.BatchTasks;

public class SdlxliffChecksTests
{
    private const string Head =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><xliff xmlns:sdl=\"http://sdl.com/FileTypes/SdlXliff/1.0\" " +
        "xmlns=\"urn:oasis:names:tc:xliff:document:1.2\" version=\"1.2\"><file original=\"x\" datatype=\"x\" " +
        "source-language=\"en-GB\" target-language=\"ru-RU\"><header>" +
        "<cxt-defs xmlns=\"http://sdl.com/FileTypes/SdlXliff/1.0\"><cxt-def id=\"1\" type=\"sdl:paragraph\"/>" +
        "<cxt-def id=\"2\" type=\"multifarious:icu\"/></cxt-defs></header><body>";

    private const string Tail = "</body></file></xliff>";

    private static string Write(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".sdlxliff");
        File.WriteAllText(path, Head + body + Tail);
        return path;
    }

    [Fact]
    public void A_file_whose_references_are_all_defined_passes()
    {
        var path = Write("<group><sdl:cxts><sdl:cxt id=\"1\"/><sdl:cxt id=\"2\"/></sdl:cxts></group>");
        try
        {
            Assert.Empty(SdlxliffChecks.UndefinedContextReferences(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_reference_without_a_definition_is_reported_once()
    {
        // The shape the first Studio run produced for the Java Resources file: ids 14 to 18
        // referenced from groups, defined nowhere.
        var path = Write(
            "<group><sdl:cxts><sdl:cxt id=\"1\"/><sdl:cxt id=\"14\"/></sdl:cxts></group>" +
            "<group><sdl:cxts><sdl:cxt id=\"2\"/><sdl:cxt id=\"14\"/><sdl:cxt id=\"15\"/></sdl:cxts></group>");
        try
        {
            Assert.Equal(["14", "15"], SdlxliffChecks.UndefinedContextReferences(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
