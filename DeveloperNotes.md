# Developer Notes

## Updating Root Trust Anchors

Run the `RootAnchorHelper` to download the latest trust anchors from IANA and update `RootTrustAnchors.cs`.

```
var records = await RootAnchorHelper.FetchLatestAsync();
```

Replace the `DsRecords` array in `DnsClientX/Security/RootTrustAnchors.cs` with the values returned by `FetchLatestAsync`.
