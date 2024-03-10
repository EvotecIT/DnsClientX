# DnsClientX - DnsClient over HTTPS

DnsClientX is available as NuGet from the gallery and its preferred way of using it.

[![nuget downloads](https://img.shields.io/nuget/dt/DnsClientX?label=nuget%20downloads)](https://www.nuget.org/packages/DnsClientX)
[![nuget version](https://img.shields.io/nuget/v/DnsClientX)](https://www.nuget.org/packages/DnsClientX)
[![license](https://img.shields.io/github/license/EvotecIT/DnsClientX.svg)](#)

If you would like to contact me you can do so via Twitter or LinkedIn.

[![twitter](https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social)](https://twitter.com/PrzemyslawKlys)
[![blog](https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg)](https://evotec.xyz/hub)
[![linked](https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn)](https://www.linkedin.com/in/pklys)

## What it's all about

<img width="256" height="256" align=right src="https://raw.githubusercontent.com/EvotecIT/DnsClientX/master/Assets/Icons/DnsClientX3.png">

**DnsClientX** is an async C# library for DNS over HTTPS, and maybe in future DNS over TLS.

It provides querying multiple DNS Providers.
- [Cloudflare](https://developers.cloudflare.com/1.1.1.1/encryption/dns-over-https/)
- Google
- [Quad9](https://www.quad9.net/news/blog/doh-with-quad9-dns-servers/)
- OpenDNS
- etc.

There are two ways **DNS over HTTPS** can be utilized:
- **JSON Format** - In this format, DNS queries and responses are encoded in JavaScript Object Notation (JSON). This format is more human-readable and can be easier to work with in web and application development environments where JSON is commonly used for data interchange.
- **Wireformat** - The DNS wire format is the traditional binary format used by DNS over UDP and TCP

As not all provides use same format. Some provide both, some only one of them. Additionaly not all providers respond in the same way, with subtle differences in the response.
DnsClientX tries to unify the responses as much as possible, so that responses from Cloudflare, Google, Quad9 via JSON or Wireformat are an exact match.

Additionally Wireformat can be queried using:
- `GET` - by default
- `POST` - currently not working

For now only `GET` requests are supported for Wireformat, as `POST` requests are not working as expected.

> [!WARNING]
> We try to unify the responses as much as possible for common use cases by translating on the fly. If you find disprepencies please **open an issue** or better **pull request**.

## Credits

This project general idea is based on [DnsOverHttps](https://github.com/actually-akac/DnsOverHttps) by [@akac](https://github.com/actually-akac) which was a starting point for **DnsClientX**. I've decided to take it a step further and make it more generic and more flexible. I've also added more providers and more options to the library, improving . I've also added tests to make sure everything works as expected.

## TO DO

> [!IMPORTANT]
> This library is still in development and there are things that need to be done, tested and fixed.
> If you would like to help, please do so by opening an issue or a pull request.
> Things may and will change, as I'm not quite sure what I am doing :-)

- [ ] Add DNS over TLS
- [ ] [Add more providers](https://dnscrypt.info/public-servers/)
- [ ] Fix POST requests for Wireformat
- [ ] Add more tests
- [ ] Go thru all additional parameters and make sure they have proper responses

## Supported .NET Versions

This library supports multiple NET versions:
- .NET 6
  - No dependencies
- .NET 7
  - No dependencies
- .NET 8
  - No dependencies
- .NET Standard 2.0
  - System.Text.Json
- .NET 4.7.2
  - System.Text.Json

## Build Status

| Platform | Status                                                                                                                                                                                                              | Test Report                                                                                                                                                                                                                                    | Code Coverage                                                                                                                                                                                                                                             | .NET                                                                 |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| Windows  | <a href="https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=30"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/30/master?compact_message&style=flat&label=Tests%20Windows"></a> | <a href="https://dev.azure.com/evotecpl/DnsClientX/_test/analytics?definitionId=30&contextType=build"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/30/master?compact_message&style=flat&label=Test Analytics"></a> | <a href="https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=30&view=ms.vss-pipelineanalytics-web.new-build-definition-pipeline-analytics-view-cardmetrics"><img src="https://img.shields.io/azure-devops/coverage/evotecpl/DnsClientX/30"></a> | .NET 4.7.2, NET 4.8, .NET 6.0, .NET 7.0, .NET 8.0, .NET Standard 2.0 |
| Linux    | <a href="https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=31"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/31/master?compact_message&style=flat&label=Tests%20Linux"></a>   | <a href="https://dev.azure.com/evotecpl/DnsClientX/_test/analytics?definitionId=31&contextType=build"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/31/master?compact_message&style=flat&label=Test Analytics"></a> |                                                                                                                                                                                                                                                           | .NET 6.0, .NET 7.0, .NET Standard 2.0, .NET 8.0                      |
| MacOs    | <a href="https://dev.azure.com/evotecpl/DnsClientX/_build?definitionId=32"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/32/master?compact_message&style=flat&label=Tests%20MacOs"></a>   | <a href="https://dev.azure.com/evotecpl/DnsClientX/_test/analytics?definitionId=32&contextType=build"><img src="https://img.shields.io/azure-devops/tests/evotecpl/DnsClientX/32/master?compact_message&style=flat&label=Test Analytics"></a> |                                                                                                                                                                                                                                                           | .NET 6.0, .NET 7.0, .NET Standard 2.0, .NET 8.0                      |


## Features

- Supports multiple DNS Providers (Cloudflare, Google, Quad9, OpenDNS, etc.)
- Supports both JSON and Wireformat
- Supports DNSSEC
- Supports multiple DNS record types
- Supports parallel queries
- No external dependencies on .NET 6, .NET 7 and .NET 8
- Minimal dependencies on .NET Standard 2.0 and .NET 4.7.2

## Other libraries

- [DnsClient.NET](https://github.com/MichaCo/DnsClient.NET) - DnsClient is a simple yet very powerful and high performant open source library for the .NET Framework to do DNS lookups. If you need standard DNS support - this one is for you.
- [DnsOverHttps](https://github.com/actually-akac/DnsOverHttps) - DnsOverHttps is a simple yet very powerful and high performant open source library for the .NET Framework to do DNS lookups over HTTPS using Cloudflare. If you only need Cloudflare support and target newer .NET versions - this one is for you.


## Please share with the community

Please consider sharing a post about DnsClientX and the value it provides. It really does help!

[![Share on reddit](https://img.shields.io/badge/share%20on-reddit-red?logo=reddit)](https://reddit.com/submit?url=https://github.com/EvotecIT/DnsClientX&title=DnsClientX)
[![Share on hacker news](https://img.shields.io/badge/share%20on-hacker%20news-orange?logo=ycombinator)](https://news.ycombinator.com/submitlink?u=https://github.com/EvotecIT/DnsClientX)
[![Share on twitter](https://img.shields.io/badge/share%20on-twitter-03A9F4?logo=twitter)](https://twitter.com/share?url=https://github.com/EvotecIT/DnsClientX&t=DnsClientX)
[![Share on facebook](https://img.shields.io/badge/share%20on-facebook-1976D2?logo=facebook)](https://www.facebook.com/sharer/sharer.php?u=https://github.com/EvotecIT/DnsClientX)
[![Share on linkedin](https://img.shields.io/badge/share%20on-linkedin-3949AB?logo=linkedin)](https://www.linkedin.com/shareArticle?url=https://github.com/EvotecIT/DnsClientX&title=DnsClientX)


## Usage

```csharp
using DnsClientX;
```