using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DnsClientX.DnsCrypt;

/// <summary>Narrow native boundary for the DNSCrypt NaCl box constructions and certificate signatures.</summary>
internal static partial class DnsCryptSodium {
    private const string Library = "libsodium";
    private static readonly Lazy<string> Availability = new Lazy<string>(Initialize, true);

    internal static string Version => Availability.Value;

    internal static void KeyPair(byte[] publicKey, byte[] secretKey) {
        _ = Version;
        if (crypto_box_keypair(publicKey, secretKey) != 0) throw new CryptographicException("DNSCrypt key generation failed.");
    }

    internal static byte[] SharedKey(ushort system, byte[] publicKey, byte[] secretKey) {
        _ = Version;
        var shared = new byte[32];
        int result = system == 2 ? crypto_box_curve25519xchacha20poly1305_beforenm(shared, publicKey, secretKey)
            : crypto_box_beforenm(shared, publicKey, secretKey);
        if (result == 0) return shared;
        Zero(shared);
        throw new CryptographicException("The resolver certificate contains an invalid X25519 public key.");
    }

    internal static byte[] Encrypt(ushort system, byte[] plaintext, byte[] nonce, byte[] shared) {
        _ = Version;
        var encrypted = new byte[plaintext.Length + 16];
        int result = system == 2 ? crypto_box_curve25519xchacha20poly1305_easy_afternm(encrypted, plaintext, (ulong)plaintext.Length, nonce, shared)
            : crypto_box_easy_afternm(encrypted, plaintext, (ulong)plaintext.Length, nonce, shared);
        if (result != 0) throw new CryptographicException("DNSCrypt query encryption failed.");
        return encrypted;
    }

    internal static byte[] Decrypt(ushort system, byte[] encrypted, byte[] nonce, byte[] shared) {
        _ = Version;
        if (encrypted.Length < 16) throw new CryptographicException("The DNSCrypt authentication tag is truncated.");
        var plaintext = new byte[encrypted.Length - 16];
        int result = system == 2 ? crypto_box_curve25519xchacha20poly1305_open_easy_afternm(plaintext, encrypted, (ulong)encrypted.Length, nonce, shared)
            : crypto_box_open_easy_afternm(plaintext, encrypted, (ulong)encrypted.Length, nonce, shared);
        if (result == 0) return plaintext;
        Zero(plaintext);
        throw new CryptographicException("DNSCrypt response authentication failed.");
    }

    internal static bool Verify(byte[] signature, byte[] signedData, byte[] providerKey) {
        _ = Version;
        return crypto_sign_verify_detached(signature, signedData, (ulong)signedData.Length, providerKey) == 0;
    }

    internal static byte[] Random(int count) {
        _ = Version;
        var bytes = new byte[count];
        randombytes_buf(bytes, new UIntPtr((uint)count));
        return bytes;
    }

    internal static void Zero(byte[] secret) {
        if (secret.Length != 0) sodium_memzero(secret, new UIntPtr((uint)secret.Length));
    }

    private static string Initialize() {
        try {
            // Framework hosts lack the runtime asset resolver used by modern .NET. Preload the
            // exact bundled architecture without modifying the process DLL search path.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.Ordinal)) {
                LoadFrameworkNativeLibrary();
            }
            if (sodium_init() < 0) throw new CryptographicException("libsodium initialization failed.");
            string version = Marshal.PtrToStringAnsi(sodium_version_string()) ?? "unknown";
            if (!System.Version.TryParse(version, out var parsed) || parsed < new System.Version(1, 0, 18)) throw new PlatformNotSupportedException("DNSCrypt requires libsodium 1.0.18 or later; loaded " + version + ".");
            // Require the exact NaCl XChaCha box API. A generic IETF AEAD is incompatible.
            if (crypto_box_curve25519xchacha20poly1305_beforenmbytes().ToUInt64() != 32 || crypto_box_curve25519xchacha20poly1305_noncebytes().ToUInt64() != 24) {
                throw new PlatformNotSupportedException("The native DNSCrypt NaCl XChaCha API has incompatible sizes.");
            }
            return version;
        } catch (DllNotFoundException exception) {
            throw new PlatformNotSupportedException("DNSCrypt requires the libsodium native asset for the current desktop operating system and process architecture.", exception);
        } catch (EntryPointNotFoundException exception) {
            throw new PlatformNotSupportedException("The loaded libsodium library does not export the DNSCrypt NaCl box APIs.", exception);
        } catch (BadImageFormatException exception) {
            throw new PlatformNotSupportedException("The libsodium native asset does not match this process architecture.", exception);
        }
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int sodium_init();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sodium_version_string();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void sodium_memzero([In, Out] byte[] bytes, UIntPtr size);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void randombytes_buf([Out] byte[] bytes, UIntPtr size);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int crypto_box_keypair([Out] byte[] publicKey, [Out] byte[] secretKey);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int crypto_box_beforenm([Out] byte[] sharedKey, byte[] publicKey, byte[] secretKey);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int crypto_box_easy_afternm([Out] byte[] encrypted, byte[] plaintext, ulong length, byte[] nonce, byte[] sharedKey);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int crypto_box_open_easy_afternm([Out] byte[] plaintext, byte[] encrypted, ulong length, byte[] nonce, byte[] sharedKey);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern UIntPtr crypto_box_curve25519xchacha20poly1305_beforenmbytes();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern UIntPtr crypto_box_curve25519xchacha20poly1305_noncebytes();
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int crypto_box_curve25519xchacha20poly1305_beforenm([Out] byte[] sharedKey, byte[] publicKey, byte[] secretKey);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int crypto_box_curve25519xchacha20poly1305_easy_afternm([Out] byte[] encrypted, byte[] plaintext, ulong length, byte[] nonce, byte[] sharedKey);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int crypto_box_curve25519xchacha20poly1305_open_easy_afternm([Out] byte[] plaintext, byte[] encrypted, ulong length, byte[] nonce, byte[] sharedKey);
    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int crypto_sign_verify_detached(byte[] signature, byte[] message, ulong length, byte[] publicKey);
}
