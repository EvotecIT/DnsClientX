using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DnsClientX;

internal static class OptionalDnsProviderLoader {
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
