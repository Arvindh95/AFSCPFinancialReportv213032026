using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

// net48 has no built-in ModuleInitializerAttribute, but Roslyn (C# 9) recognizes the
// attribute by its full name as long as the type exists. Define it ourselves so we can
// register the assembly resolver before any test touches a PX.* type.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute { }
}

namespace FinancialReport.Tests
{
    /// <summary>
    /// The FinancialReport assembly references PX.* assemblies that live in the Acumatica
    /// runtime Bin folder and are NOT copied into the test output (Private=False). When the
    /// CLR fails to probe-load one of them, this resolver loads it from the Acumatica Bin
    /// directory, where all its transitive dependencies also live.
    ///
    /// Runs via [ModuleInitializer] at assembly load — before any test method executes —
    /// so the handler is registered before the first PX type is JIT-resolved.
    /// </summary>
    internal static class PxAssemblyResolver
    {
        // In-place Acumatica install on this machine. Matches the HintPath base used by
        // the main project's PX references (..\..\..\..\Bin from the project folder).
        private static readonly string AcumaticaBin =
            @"C:\Program Files\Acumatica ERP\2025R2\Bin";

        [ModuleInitializer]
        internal static void Init()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string simpleName = new AssemblyName(args.Name).Name;

            // Only handle Acumatica assemblies; let the CLR resolve everything else.
            if (string.IsNullOrEmpty(simpleName) || !simpleName.StartsWith("PX.", StringComparison.OrdinalIgnoreCase))
                return null;

            string candidate = Path.Combine(AcumaticaBin, simpleName + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        }
    }
}
