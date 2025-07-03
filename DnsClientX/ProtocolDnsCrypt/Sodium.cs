using System;
using System.Runtime.InteropServices;

namespace DnsClientX {
    internal static class Sodium {
        private const string LIB = "libsodium";

        static Sodium() {
            if (sodium_init() < 0) {
                throw new Exception("libsodium initialization failed");
            }
        }

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sodium_init();

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void crypto_box_curve25519xchacha20poly1305_keypair(byte[] pk, byte[] sk);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int crypto_kx_client_session_keys(byte[] rx, byte[] tx, byte[] client_pk, byte[] client_sk, byte[] server_pk);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int crypto_aead_xchacha20poly1305_ietf_encrypt(byte[] c, out ulong clen_p, byte[] m, ulong mlen, IntPtr ad, ulong adlen, IntPtr nsec, byte[] npub, byte[] k);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int crypto_aead_xchacha20poly1305_ietf_decrypt(byte[] m, out ulong mlen_p, IntPtr nsec, byte[] c, ulong clen, IntPtr ad, ulong adlen, byte[] npub, byte[] k);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int crypto_sign_ed25519_verify_detached(byte[] sig, byte[] m, ulong mlen, byte[] pk);
    }
}
