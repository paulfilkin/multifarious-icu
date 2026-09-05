using System.Reflection;
using System.Runtime.CompilerServices;

namespace multifarious.Icu.Tests.BatchTasks;

/// <summary>
/// Studio assemblies are referenced for compilation, but some are only reached at run time by
/// name: DefaultDocumentItemFactory.CreateInstance() loads
/// Sdl.FileTypeSupport.Framework.Implementation reflectively, for instance. Inside Studio they are
/// all simply present. Here they are not, so tests that build real paragraph units need the
/// installation directory on the probing path. Without this the failure is a
/// TypeInitializationException wrapping a FileNotFoundException, which reads like a broken test
/// rather than a missing runtime dependency.
/// </summary>
internal static class StudioAssemblyResolver
{
    private static readonly string StudioFolder = Path.Combine(
        Environment.GetEnvironmentVariable("ProgramW6432") ?? @"C:\Program Files",
        @"Trados\Trados Studio\Studio19");

    [ModuleInitializer]
    internal static void Register()
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }

    private static Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        if (name is null || !Directory.Exists(StudioFolder))
        {
            return null;
        }

        var candidate = Path.Combine(StudioFolder, name + ".dll");
        return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
    }
}
