using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DnsClientX;

/// <summary>Loads an explicitly selected optional DNSSEC provider without adding it to core dependencies.</summary>
public static class DnsSecSignatureVerifierLoader {
    /// <summary>Creates the assembly's single public, parameterless DNSSEC signature verifier.</summary>
    /// <param name="assemblyPath">Local provider assembly path. Its dependencies must be available beside it.</param>
    /// <returns>A thread-safe verifier implemented by the selected assembly.</returns>
    /// <remarks>Loading an assembly executes local code. Select only a provider trusted by the application.</remarks>
    public static IDnsSecSignatureVerifier Load(string assemblyPath) {
        if (string.IsNullOrWhiteSpace(assemblyPath)) throw new ArgumentException("A DNSSEC provider assembly path is required.", nameof(assemblyPath));
        string path = Path.GetFullPath(assemblyPath);
        Assembly assembly = Assembly.LoadFrom(path);
        Type[] providers = assembly.GetExportedTypes().Where(type => type.IsClass && !type.IsAbstract
            && !type.ContainsGenericParameters && typeof(IDnsSecSignatureVerifier).IsAssignableFrom(type)
            && type.GetConstructor(Type.EmptyTypes) != null).ToArray();
        if (providers.Length != 1) throw new InvalidOperationException(
            "The selected assembly must contain exactly one public, parameterless IDnsSecSignatureVerifier implementation.");
        return (IDnsSecSignatureVerifier)Activator.CreateInstance(providers[0])!;
    }
}
