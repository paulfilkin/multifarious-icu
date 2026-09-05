using System.Resources;
using multifarious.Icu.BatchTasks;
using multifarious.Icu.BatchTasks.BatchTasks;
using Sdl.ProjectAutomation.AutomaticTasks;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// What Studio needs in order to offer the batch tasks at all.
///
/// The failure these guard is silent: the code compiles, the task looks right, and Studio simply
/// never lists it. The attribute is what puts it in plugin.xml, the supported-file-type attribute
/// is what makes Studio offer it for any file, and the resource keys are what name it in the list.
/// </summary>
public class BatchTaskContractTests
{
    public static IEnumerable<object[]> Tasks =>
    [
        [typeof(IcuExpandTask), Constants.ExpandTaskId],
        [typeof(IcuFinaliseTask), Constants.FinaliseTaskId],
    ];

    private static AutomaticTaskAttribute AttributeOf(Type task) =>
        task.GetCustomAttributes(typeof(AutomaticTaskAttribute), false)
            .Cast<AutomaticTaskAttribute>()
            .Single();

    [Theory]
    [MemberData(nameof(Tasks))]
    public void The_task_carries_the_attribute_that_puts_it_in_plugin_xml(Type task, string id)
    {
        var attribute = AttributeOf(task);

        Assert.Equal(id, attribute.Id);
        Assert.Equal(AutomaticTaskFileType.BilingualTarget, attribute.GeneratedFileType);
        Assert.True(attribute.AllowMultiple,
            "a project has one bilingual file per target language and the task has to run over all of them");
    }

    [Theory]
    [MemberData(nameof(Tasks))]
    public void The_task_declares_it_runs_on_bilingual_target_files(Type task, string id)
    {
        _ = id;
        var supported = task.GetCustomAttributes(typeof(AutomaticTaskSupportedFileTypeAttribute), false)
            .Cast<AutomaticTaskSupportedFileTypeAttribute>()
            .ToList();

        Assert.NotEmpty(supported);
        Assert.Contains(supported, s => s.FileType == AutomaticTaskFileType.BilingualTarget);
    }

    /// <summary>
    /// Studio resolves the name and description from the plugin resources. A missing entry shows
    /// the raw key in the task list.
    /// </summary>
    [Theory]
    [MemberData(nameof(Tasks))]
    public void The_task_name_and_description_exist_in_the_plugin_resources(Type task, string id)
    {
        _ = id;
        var manager = new ResourceManager("multifarious.Icu.BatchTasks.PluginResources", typeof(Constants).Assembly);
        var attribute = AttributeOf(task);

        Assert.False(string.IsNullOrEmpty(manager.GetString(attribute.Name)),
            $"PluginResources has no entry '{attribute.Name}'");
        Assert.False(string.IsNullOrEmpty(manager.GetString(attribute.Description)),
            $"PluginResources has no entry '{attribute.Description}'");
    }

    [Fact]
    public void The_two_task_ids_differ()
    {
        Assert.NotEqual(Constants.ExpandTaskId, Constants.FinaliseTaskId);
    }
}
