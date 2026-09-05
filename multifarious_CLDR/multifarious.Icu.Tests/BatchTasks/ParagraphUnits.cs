using Sdl.FileTypeSupport.Framework.BilingualApi;
using Sdl.FileTypeSupport.Framework.Core.Utilities.BilingualApi;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// Builds paragraph units through the framework's own factories, shaped as Studio's JSON and
/// Java Resources filters shape them (Phase 0 log): a source paragraph of plain text segments, an
/// empty target mirror sharing the segment ids, and the filter's context carrying the key.
/// </summary>
internal static class ParagraphUnits
{
    public static IDocumentItemFactory ItemFactory { get; } = DefaultDocumentItemFactory.CreateInstance();

    public static IPropertiesFactory PropertiesFactory => ItemFactory.PropertiesFactory;

    /// <summary>One unit whose source is the given text runs, each its own segment, as Studio's segmentation would leave a value cut at sentence ends.</summary>
    public static IParagraphUnit WithSegments(params string[] segmentTexts)
    {
        var unit = ItemFactory.CreateParagraphUnit(LockTypeFlags.Unlocked);
        var number = 1;
        foreach (var text in segmentTexts)
        {
            var properties = ItemFactory.CreateSegmentPairProperties();
            properties.Id = new SegmentId(number.ToString());
            number++;

            var source = ItemFactory.CreateSegment(properties);
            source.Add(ItemFactory.CreateText(PropertiesFactory.CreateTextProperties(text)));
            unit.Source.Add(source);

            unit.Target.Add(ItemFactory.CreateSegment(properties));
        }

        return unit;
    }

    /// <summary>A JSON filter unit: one segment, an sdl:paragraph context carrying JsonPath.</summary>
    public static IParagraphUnit Json(string value, string jsonPath)
    {
        var unit = WithSegments(value);
        var context = PropertiesFactory.CreateContextInfo("sdl:paragraph");
        context.DisplayCode = "P";
        context.Description = "A paragraph of text";
        context.SetMetaData("JsonPath", jsonPath);
        AddContext(unit, context);
        return unit;
    }

    /// <summary>A Java Resources filter unit: a KeyValue context carrying SDL:SpiceId and naming the key in its description.</summary>
    public static IParagraphUnit Properties(string value, string key)
    {
        var unit = WithSegments(value);
        var context = PropertiesFactory.CreateContextInfo("KeyValue");
        context.DisplayCode = "V";
        context.Description = "Key name=\"" + key + "\"\nTextual value of a key.";
        context.SetMetaData("SDL:SpiceId", key);
        AddContext(unit, context);
        return unit;
    }

    public static void AddContext(IParagraphUnit unit, IContextInfo context)
    {
        unit.Properties.Contexts ??= PropertiesFactory.CreateContextProperties();
        unit.Properties.Contexts.Contexts.Add(context);
    }

    /// <summary>The top-level items of a paragraph, indexed rather than enumerated (see RawValueReconstruction).</summary>
    public static List<IAbstractMarkupData> ItemsOf(IAbstractMarkupDataContainer container)
    {
        var items = new List<IAbstractMarkupData>();
        for (var i = 0; i < container.Count; i++)
        {
            items.Add(container[i]);
        }
        return items;
    }
}
