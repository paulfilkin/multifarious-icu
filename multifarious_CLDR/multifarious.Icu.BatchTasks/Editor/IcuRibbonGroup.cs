using Sdl.Desktop.IntegrationApi;
using Sdl.Desktop.IntegrationApi.DefaultLocations;
using Sdl.Desktop.IntegrationApi.Extensions;
using Sdl.TranslationStudioAutomation.IntegrationApi;

namespace multifarious.Icu.BatchTasks.Editor
{
    /// <summary>The plugin's group on the editor's Add-ins tab, holding the button that shows the ICU Forms window.</summary>
    [RibbonGroup(Constants.RibbonGroupId, "RibbonGroup_Icu_Name", typeof(EditorController))]
    [RibbonGroupLayout(LocationByType = typeof(StudioDefaultRibbonTabs.AddinsRibbonTabLocation))]
    public class IcuRibbonGroup : AbstractRibbonGroup
    {
    }

    /// <summary>Shows the ICU Forms window, if a document is open in the editor.</summary>
    [Action(
        Constants.ShowFormsActionId,
        Name = "Action_ShowForms_Name",
        Description = "Action_ShowForms_Description",
        Icon = "IcuForms_Icon")]
    [ActionLayout(typeof(IcuRibbonGroup), 10, DisplayType.Large)]
    public class ShowIcuFormsAction : AbstractAction
    {
        protected override void Execute()
        {
            var editor = SdlTradosStudio.Application.GetController<EditorController>();
            if (editor == null || editor.ActiveDocument == null) return;

            var viewPart = SdlTradosStudio.Application.GetController<IcuFormsViewPartController>();
            if (viewPart != null) viewPart.Show();
        }
    }
}
