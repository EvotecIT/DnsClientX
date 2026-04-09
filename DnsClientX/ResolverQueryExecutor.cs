using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class ResolverQueryExecutor {
        internal static Task<ResolverQueryAttemptResult> ExecuteAsync(
            ResolverExecutionTarget target,
            string name,
            DnsRecordType recordType,
            ResolverQueryRunOptions options,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride,
            CancellationToken cancellationToken) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            if (target.ExplicitEndpoint != null) {
                return explicitOverride != null
                    ? explicitOverride(target.ExplicitEndpoint, name, recordType, cancellationToken)
                    : ExecuteExplicitAsync(target.ExplicitEndpoint, target.DisplayName, name, recordType, options, cancellationToken);
            }

            if (!target.BuiltInEndpoint.HasValue) {
                throw new ArgumentException("Execution target must specify either a built-in or explicit endpoint.", nameof(target));
            }

            return builtInOverride != null
                ? builtInOverride(target.BuiltInEndpoint.Value, name, recordType, cancellationToken)
                : ExecuteBuiltInAsync(target.BuiltInEndpoint.Value, target.DisplayName, name, recordType, options, cancellationToken);
        }

        private static async Task<ResolverQueryAttemptResult> ExecuteBuiltInAsync(
            DnsEndpoint endpoint,
            string displayName,
            string name,
            DnsRecordType recordType,
            ResolverQueryRunOptions options,
            CancellationToken cancellationToken) {
            await using var client = ResolverExecutionClientFactory.CreateClient(
                new ResolverExecutionTarget {
                    DisplayName = displayName,
                    BuiltInEndpoint = endpoint
                },
                CreateClientOptions(options));

            return await ExecuteWithClientAsync(
                client,
                displayName,
                name,
                recordType,
                options.RequestDnsSec,
                options.ValidateDnsSec,
                options,
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<ResolverQueryAttemptResult> ExecuteExplicitAsync(
            DnsResolverEndpoint endpoint,
            string displayName,
            string name,
            DnsRecordType recordType,
            ResolverQueryRunOptions options,
            CancellationToken cancellationToken) {
            await using var client = ResolverExecutionClientFactory.CreateClient(
                new ResolverExecutionTarget {
                    DisplayName = displayName,
                    ExplicitEndpoint = endpoint
                },
                CreateClientOptions(options));
            client.EndpointConfiguration.UseTcpFallback = endpoint.AllowTcpFallback;
            if (endpoint.EdnsBufferSize.HasValue) {
                client.EndpointConfiguration.UdpBufferSize = endpoint.EdnsBufferSize.Value;
            }
            if (endpoint.Timeout.HasValue) {
                client.EndpointConfiguration.TimeOut = (int)Math.Max(1, endpoint.Timeout.Value.TotalMilliseconds);
            }
            if (endpoint.Transport != Transport.Doh && !options.PortOverride.HasValue) {
                client.EndpointConfiguration.Port = endpoint.Port;
            }

            return await ExecuteWithClientAsync(
                client,
                displayName,
                name,
                recordType,
                options.RequestDnsSec || endpoint.DnsSecOk == true,
                options.ValidateDnsSec,
                options,
                cancellationToken).ConfigureAwait(false);
        }

        private static ResolverExecutionClientOptions CreateClientOptions(ResolverQueryRunOptions options) {
            return new ResolverExecutionClientOptions {
                TimeoutMs = Math.Max(1, options.TimeoutMs),
                PortOverride = options.PortOverride,
                ForceDohWirePost = options.ForceDohWirePost
            };
        }

        private static async Task<ResolverQueryAttemptResult> ExecuteWithClientAsync(
            ClientX client,
            string displayName,
            string name,
            DnsRecordType recordType,
            bool requestDnsSec,
            bool validateDnsSec,
            ResolverQueryRunOptions options,
            CancellationToken cancellationToken) {
            DnsRequestFormat requestFormat = client.EndpointConfiguration.RequestFormat;
            if (!DnsTransportCapabilities.Supports(requestFormat)) {
                return CreateUnsupportedAttemptResult(client, displayName, name, recordType, requestFormat);
            }

            var stopwatch = Stopwatch.StartNew();
            try {
                DnsResponse response = await client.Resolve(
                    name,
                    recordType,
                    requestDnsSec,
                    validateDnsSec,
                    retryOnTransient: false,
                    maxRetries: options.MaxRetries,
                    retryDelayMs: options.RetryDelayMs,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                return new ResolverQueryAttemptResult {
                    Target = displayName,
                    RequestFormat = requestFormat,
                    Resolver = !string.IsNullOrWhiteSpace(response.ServerAddress) ? response.ServerAddress! : ResolverEndpointClientFactory.DescribeConfiguredResolver(client),
                    Response = response,
                    Elapsed = response.RoundTripTime > TimeSpan.Zero ? response.RoundTripTime : stopwatch.Elapsed
                };
            } catch (Exception ex) {
                stopwatch.Stop();
                return new ResolverQueryAttemptResult {
                    Target = displayName,
                    RequestFormat = requestFormat,
                    Resolver = ResolverEndpointClientFactory.DescribeConfiguredResolver(client),
                    Elapsed = stopwatch.Elapsed,
                    Error = ex.Message
                };
            }
        }

        private static ResolverQueryAttemptResult CreateUnsupportedAttemptResult(
            ClientX client,
            string displayName,
            string name,
            DnsRecordType recordType,
            DnsRequestFormat requestFormat) {
            var response = new DnsResponse {
                Questions = new[] {
                    new DnsQuestion {
                        Name = name,
                        Type = recordType,
                        RequestFormat = requestFormat,
                        OriginalName = name
                    }
                },
                Status = DnsResponseCode.NotImplemented,
                Error = DnsTransportCapabilities.GetUnsupportedMessage(requestFormat)
            };
            response.AddServerDetails(client.EndpointConfiguration);

            return new ResolverQueryAttemptResult {
                Target = displayName,
                RequestFormat = requestFormat,
                Resolver = ResolverEndpointClientFactory.DescribeConfiguredResolver(client),
                Response = response,
                Elapsed = TimeSpan.Zero
            };
        }
    }
}
