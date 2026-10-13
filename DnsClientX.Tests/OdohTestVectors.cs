using System;

namespace DnsClientX.Tests;

// Independent Cloudflare odoh-go fixture, source commit f39fa019b017510690599e895c20cd02ae138742 (MIT).
// Generated with the required RFC 9230 suite and Cisco go-hpke, not DnsClientX.
// Fixture generator and provenance: DnsClientX.Odoh/Qualification/GenerateFixture.go.
internal static class OdohTestVectors {
    internal const string BadPaddingResponse = "0200107474747474747474747474747474747400487c17d7556869a5277c307297bd1c20c7a85a0445e9bde093b84ebb6354befcaf75fd2118cbf2df432c64c987d2bfa84ca84db816db7bb98d543d57f979563fa3d2d46f3d694e7118";
    internal const string Config = "002c000100280020000100010020ae3bf1cd87c2d2ed25af4a1a239eed04a990f00e7403e4c8065927de010fd17a";
    internal const string EncryptedQuery = "010020e2acc3540a58c7eeac78fef62880c0f4deb0a4d64addc85b1e353d020e907006005c7bc0cfc5d0a57e8311a9db50378177c1c588355ec69381a655433ebe301a395f89fc93cfd6561bb89b0288cd2a3db5faeb74bb304cde86ad3eb4d8ba34191f26aae0cb16ce7a880c050f1c2b47a2961f881b33bd0e2023cd4f42946f";
    internal const string EncryptedResponse = "020010737373737373737373737373737373730048403782badee0cace1a3f6e285fe5727d2a02665df957b8f1892071831edb4817d5b95d37b8d6c7647c56eb4f8b8b70f976d4be2f753afc0466a11b7efffacc287c0950f844fb6a95";
    internal const string EphemeralPrivateKey = "b52268a5b0b939c9b22edb986f0996877a0bf87524a7f267fd10f13c748d67dd";
    internal const string KeyId = "e2acc3540a58c7eeac78fef62880c0f4deb0a4d64addc85b1e353d020e907006";
    internal const string Query = "123401000001000000000000076578616d706c6503636f6d0000010001";
    internal const string QueryPlaintext = "001d123401000001000000000000076578616d706c6503636f6d0000010001000b0000000000000000000000";
    internal const string Response = "123481800001000100000000076578616d706c6503636f6d0000010001c00c000100010000003c00047f000001";

    internal const string TargetPrivateKey = "9da300999027ec7ab4e1ed2c0d39ce965d9d2dec29a0c88d4ab6cb527c588b3d";

    internal static byte[] Hex(string value) {
        var bytes = new byte[value.Length / 2];
        for (int i = 0; i < bytes.Length; i++) {
            bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
