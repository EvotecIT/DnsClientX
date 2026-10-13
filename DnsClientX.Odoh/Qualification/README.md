# ODoH fixture provenance

`GenerateFixture.go` generates the deterministic fixture in `DnsClientX.Tests/OdohTestVectors.cs` with Cloudflare's independent [odoh-go implementation](https://github.com/cloudflare/odoh-go), at commit `f39fa019b017510690599e895c20cd02ae138742` (MIT). Its HPKE implementation is Cisco go-hpke `v0.0.0-20230407100446-246075f83609`. The fixed recipient and sender seeds are synthetic test material.

To reproduce it, check out that producer outside the product repository, copy `GenerateFixture.go` into its root as `qualification_test.go`, and run `go test -run TestDnsClientXFixture -v`. It writes `../odoh-fixture.json`, containing the complete configuration, key ID, encrypted query, response, and authenticated invalid-padding response. Normal product builds and tests do not require Go. The generator is excluded from the NuGet package.

The HPKE primitive test uses the base-mode X25519/HKDF-SHA256/AES-128-GCM vector from the [CFRG reference vectors](https://github.com/cfrg/draft-irtf-cfrg-hpke/blob/master/test-vectors.json), including the first ciphertext and three exporter contexts. RFC 9230 uses a fresh, single-use sender context for each query.
