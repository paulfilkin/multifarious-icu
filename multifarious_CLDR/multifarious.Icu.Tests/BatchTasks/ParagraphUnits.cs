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

    /// <summary>Every segment under a container in document order, through comment markers and other wrappers.</summary>
    public static List<ISegment> SegmentsOf(IAbstractMarkupDataContainer container)
    {
        var segments = new List<ISegment>();
        for (var i = 0; i < container.Count; i++)
        {
            if (container[i] is ISegment segment)
            {
                segments.Add(segment);
            }
            else if (container[i] is IAbstractMarkupDataContainer nested && container[i] is not ILockedContent)
            {
                segments.AddRange(SegmentsOf(nested));
            }
        }
        return segments;
    }

    /// <summary>A comment marker on a segment, inside it or around it; the expansion writes none, so this finds a translator's or a copied one.</summary>
    public static ICommentMarker? CommentOf(ISegment segment) =>
        segment.Parent as ICommentMarker ?? (segment.Count > 0 ? segment[0] as ICommentMarker : null);

    /// <summary>The texts of every comment on the unit, in order.</summary>
    public static List<string> UnitComments(IParagraphUnit unit)
    {
        var texts = new List<string>();
        var comments = unit.Properties.Comments;
        if (comments == null) return texts;
        for (var i = 0; i < comments.Count; i++) texts.Add(comments.GetItem(i).Text);
        return texts;
    }

    /// <summary>True when no segment under the paragraph carries a comment marker, inside or around it.</summary>
    public static bool NoSegmentComments(IAbstractMarkupDataContainer paragraph) =>
        SegmentsOf(paragraph).All(s => CommentOf(s) == null && !ItemsOf(s).Any(i => i is ICommentMarker));

    /// <summary>A segment's translatable content: the items inside its comment marker where it has one, else its own items.</summary>
    public static List<IAbstractMarkupData> ContentOf(ISegment segment) =>
        segment.Count == 1 && segment[0] is ICommentMarker marker ? ItemsOf(marker) : ItemsOf(segment);

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
