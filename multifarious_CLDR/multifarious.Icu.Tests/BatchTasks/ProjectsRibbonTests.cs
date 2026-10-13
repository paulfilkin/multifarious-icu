using System.Drawing;
using System.Resources;
using multifarious.Icu.BatchTasks;
using multifarious.Icu.BatchTasks.Projects;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.DefaultLocations;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// The Projects view's ribbon group and its ICU Settings action: the attributes Studio
/// discovers them by and the resource keys that name them. Silent if wrong, as with the
/// editor's group.
/// </summary>
public class ProjectsRibbonTests
{
    private static ResourceManager PluginResources() =>
        new("multifarious.Icu.BatchTasks.PluginResources", typeof(Constants).Assembly);

    [Fact]
    public void The_group_and_action_sit_on_the_projects_views_addins_tab()
    {
        var group = typeof(IcuProjectsRibbonGroup).GetCustomAttributes(typeof(RibbonGroupAttribute), false).Cast<RibbonGroupAttribute>().Single();
        var groupLayout = typeof(IcuProjectsRibbonGroup).GetCustomAttributes(typeof(RibbonGroupLayoutAttribute), false).Cast<RibbonGroupLayoutAttribute>().Single();
        var action = typeof(ShowIcuSettingsAction).GetCustomAttributes(typeof(ActionAttribute), false).Cast<ActionAttribute>().Single();
        var actionLayout = typeof(ShowIcuSettingsAction).GetCustomAttributes(typeof(ActionLayoutAttribute), false).Cast<ActionLayoutAttribute>().Single();

        Assert.Equal(Constants.ProjectsRibbonGroupId, group.Id);
        Assert.Equal(typeof(ProjectsController), group.ContextByType);
        Assert.Equal(typeof(StudioDefaultRibbonTabs.AddinsRibbonTabLocation), groupLayout.LocationByType);
        Assert.Equal(Constants.ShowSettingsActionId, action.Id);
        Assert.Equal(typeof(IcuProjectsRibbonGroup), actionLayout.LocationByType);
        Assert.NotEqual(Constants.RibbonGroupId, Constants.ProjectsRibbonGroupId);
        Assert.NotEqual(Constants.ShowFormsActionId, Constants.ShowSettingsActionId);

        var resources = PluginResources();
        Assert.False(string.IsNullOrEmpty(resources.GetString(group.Name)));
        Assert.False(string.IsNullOrEmpty(resources.GetString(action.Name)));
        Assert.False(string.IsNullOrEmpty(resources.GetString(action.Description)));
        Assert.IsType<Icon>(resources.GetObject(action.Icon));
    }
}
