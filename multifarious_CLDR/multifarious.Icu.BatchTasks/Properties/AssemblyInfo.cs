using System.Reflection;
using System.Runtime.InteropServices;

// General information about this assembly.
[assembly: AssemblyTitle("multifarious.Icu.BatchTasks")]
[assembly: AssemblyDescription("Expands ICU MessageFormat plural forms into one segment per CLDR category, and finalises the translated messages before target generation.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("multifarious")]
[assembly: AssemblyProduct("multifariousICU Support for Trados Studio")]
[assembly: AssemblyCopyright("Copyright © 2026 multifarious.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Types in this assembly are not visible to COM components.
[assembly: ComVisible(false)]

// ID of the typelib if this project is exposed to COM.
[assembly: Guid("7c2e1f0a-4b6d-4e8f-9a31-2d5c8e7b6f14")]

// Assembly version. Kept in step with the plugin package manifest version.
//
// Not the same thing as the task ids in Constants, which stay as they are: Studio pins a
// task's registration and its settings against that id, and changing it orphans every project
// template that names the task. A version bump is not an id bump.
[assembly: AssemblyVersion("1.0.2.0")]
[assembly: AssemblyFileVersion("1.0.2.0")]
