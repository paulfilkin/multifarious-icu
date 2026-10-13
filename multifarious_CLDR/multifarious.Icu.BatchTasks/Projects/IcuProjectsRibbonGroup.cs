using System;
using System.Linq;
using System.Windows;
using multifarious.Icu.BatchTasks.Resources;
using multifarious.Icu.BatchTasks.Services;
using multifarious.Icu.BatchTasks.Settings.Views;
using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.DefaultLocations;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace multifarious.Icu.BatchTasks.Projects
{
    /// <summary>The plugin's group on the Projects view's Add-ins tab, holding the ICU Settings button.</summary>
    [RibbonGroup(Constants.ProjectsRibbonGroupId, "RibbonGroup_IcuProjects_Name", typeof(ProjectsController))]
    [RibbonGroupLayout(LocationByType = typeof(StudioDefaultRibbonTabs.AddinsRibbonTabLocation))]
    public class IcuProjectsRibbonGroup : AbstractRibbonGroup
    {
    }

    /// <summary>
    /// Opens the Expand ICU and Restore ICU settings of the selected project in a dialog and
    /// writes them back on OK. Studio adds a plugin batch task's settings page to the Batch
    /// Processing tree only when a wizard hands it a task template (the Batch Processing
    /// settings category, decompiled 13 October 2026), so the Project Settings dialog and the
    /// template editor never show the pages; this action is how a project's settings are set up
    /// or changed outside the wizards. It edits the project-level bundle, what the wizards call
    /// All Language Pairs; a language pair given its own settings in a wizard keeps them.
    /// </summary>
    [Action(
        Constants.ShowSettingsActionId,
        Name = "Action_ShowSettings_Name",
        Description = "Action_ShowSettings_Description",
        Icon = "IcuForms_Icon")]
    [ActionLayout(typeof(IcuProjectsRibbonGroup), 10, DisplayType.Large)]
    public class ShowIcuSettingsAction : AbstractAction
    {
        protected override void Execute()
        {
            try
            {
                var projects = SdlTradosStudio.Application.GetController<ProjectsController>();
                var project = projects == null ? null : projects.CurrentProject ?? projects.SelectedProjects?.FirstOrDefault();
                if (project == null)
                {
                    MessageBox.Show(UIStrings.Settings_NoProject, UIStrings.Settings_ExpandTab, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var name = project.GetProjectInfo().Name ?? string.Empty;
                var bundle = project.GetSettings();
                Diagnostics.Write("settings dialog: project=" + name);

                var window = new IcuSettingsWindow(name, bundle);
                if (window.ShowDialog() != true)
                {
                    Diagnostics.Write("settings dialog: cancelled");
                    return;
                }

                project.UpdateSettings(bundle);
                project.Save();
                projects.RefreshProjects();
                Diagnostics.Write("settings dialog: saved");
            }
            catch (Exception exception)
            {
                Diagnostics.Write("settings dialog: failed: " + exception);
                MessageBox.Show(string.Format(System.Globalization.CultureInfo.CurrentCulture, UIStrings.Settings_SaveFailed, exception.Message),
                    UIStrings.Settings_ExpandTab, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
