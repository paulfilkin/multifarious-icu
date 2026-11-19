using System.Globalization;
using multifarious.Icu.BatchTasks.Resources;
using Sdl.ProjectAutomation.Core;

namespace multifarious.Icu.BatchTasks.Services
{
    /// <summary>
    /// Whether a project came from Trados Cloud. Studio lists the batch tasks on such a project
    /// and runs them, but its files were laid out by the cloud app and go back to the cloud's
    /// own tasks, so both tasks leave them alone and say so (Paul, 19 November 2026, after
    /// running them on a downloaded project).
    /// </summary>
    public static class CloudProjects
    {
        public static bool IsCloud(ProjectInfo info)
        {
            if (info == null) return false;
            switch (info.ProjectType)
            {
                case ProjectType.DownloadedFromLanguageCloud:
                case ProjectType.ImportedFromLanguageCloud:
                case ProjectType.InLanguageCloud:
                case ProjectType.PackageSavedToCloud:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The Task Results message for a task that was run on a cloud project.</summary>
        public static string Message(string taskName)
        {
            return string.Format(CultureInfo.CurrentCulture, UIStrings.Task_CloudProject, taskName);
        }

        /// <summary>What the diagnostics log says about the project, so a run shows which type Studio reports.</summary>
        public static string Describe(ProjectInfo info)
        {
            return info == null
                ? "<null>"
                : "type=" + info.ProjectType + " origin=" + (info.ProjectOrigin ?? "<null>") + " cloud=" + IsCloud(info);
        }
    }
}
