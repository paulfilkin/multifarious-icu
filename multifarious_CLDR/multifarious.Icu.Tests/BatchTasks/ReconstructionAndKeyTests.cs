using multifarious.Icu.BatchTasks.Services;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace multifarious.Icu.Tests.BatchTasks;

public class ReconstructionAndKeyTests
{
    [Fact]
    public void A_value_split_across_segments_by_segmentation_reconstructs_whole()
    {
        // The Phase 0 log: Studio cuts this value into four segments at the sentence ends
        // inside the branches. Only the whole paragraph is the ICU message.
        var unit = ParagraphUnits.WithSegments(
            "{rabbitCount, plural, =0 {There are no rabbits.",
            " We appear to be safe.} =1 {That's no ordinary rabbit!} other {Run away!",
            " There are # vicious rabbits!}}");

        var reconstructed = RawValueReconstruction.Reconstruct(unit.Source);

        Assert.Equal(
            "{rabbitCount, plural, =0 {There are no rabbits. We appear to be safe.} =1 {That's no ordinary rabbit!} other {Run away! There are # vicious rabbits!}}",
            reconstructed.RawValue);
        Assert.False(reconstructed.UsedFilterTags);
    }

    [Fact]
    public void Filter_tags_fold_their_content_back_into_the_value()
    {
        var unit = ParagraphUnits.WithSegments("You have ");
        var segment = (Sdl.FileTypeSupport.Framework.BilingualApi.ISegment)unit.Source[0];
        segment.Add(ParagraphUnits.ItemFactory.CreatePlaceholderTag(
            ParagraphUnits.PropertiesFactory.CreatePlaceholderTagProperties("{count}")));
        segment.Add(ParagraphUnits.ItemFactory.CreateText(
            ParagraphUnits.PropertiesFactory.CreateTextProperties(" messages.")));

        var reconstructed = RawValueReconstruction.Reconstruct(unit.Source);

        Assert.Equal("You have {count} messages.", reconstructed.RawValue);
        Assert.True(reconstructed.UsedFilterTags);
    }

    [Fact]
    public void The_key_is_read_from_the_json_filters_context()
    {
        var unit = ParagraphUnits.Json("Message Centre", "['app.title']");

        Assert.Equal("['app.title']", ResourceKey.Of(unit));
    }

    [Fact]
    public void The_key_is_read_from_the_properties_filters_context()
    {
        var unit = ParagraphUnits.Properties("Message Centre", "app.title");

        Assert.Equal("app.title", ResourceKey.Of(unit));
    }

    [Fact]
    public void The_key_falls_back_to_the_description_when_no_metadata_names_it()
    {
        var unit = ParagraphUnits.WithSegments("Message Centre");
        var context = ParagraphUnits.PropertiesFactory.CreateContextInfo("Other");
        context.Description = "Key name=\"fallback.key\"";
        ParagraphUnits.AddContext(unit, context);

        Assert.Equal("fallback.key", ResourceKey.Of(unit));
    }

    [Fact]
    public void A_unit_without_contexts_has_no_key_and_is_not_expanded()
    {
        var unit = ParagraphUnits.WithSegments("Message Centre");

        Assert.Null(ResourceKey.Of(unit));
        Assert.False(ResourceKey.IsExpanded(unit));
    }
}
