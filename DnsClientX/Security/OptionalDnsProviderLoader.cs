using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DnsClientX;

internal static class OptionalDnsProviderLoader {
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Provider assembly discovery requires preserved provider types. Register a provider instance directly in trimmed applications.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Loading external managed provider assemblies is unavailable to Native AOT. Register a statically referenced provider instance directly.")]
#endif
    internal static T Load<T>(string assemblyPath, string description) where T : class {
        if (string.IsNullOrWhiteSpace(assemblyPath)) { throw new ArgumentException("A " + description + " provider assembly path is required.", nameof(assemblyPath)); }
        Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(assemblyPath));
        Type[] providers = assembly.GetExportedTypes().Where(type => type.IsClass && !type.IsAbstract &&
            !type.ContainsGenericParameters && typeof(T).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null).ToArray();
        if (providers.Length != 1) {
            throw new InvalidOperationException("The selected assembly must contain exactly one public, parameterless " + typeof(T).Name + " implementation.");
        }
        return (T)Activator.CreateInstance(providers[0])!;
    }
}
