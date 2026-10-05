using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class ResolverQueryExecutor {
        internal static async Task<ResolverQueryAttemptResult> ExecuteAsync(
            ResolverExecutionTarget target,
            string name,
            DnsRecordType recordType,
            ResolverQueryRunOptions options,
            Func<DnsEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? builtInOverride,
            Func<DnsResolverEndpoint, string, DnsRecordType, CancellationToken, Task<ResolverQueryAttemptResult>>? explicitOverride,
            CancellationToken cancellationToken,
            Func<ClientX>? retainedClient = null) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (target.ExplicitEndpoint != null && explicitOverride != null) {
                return await explicitOverride(target.ExplicitEndpoint, name, recordType, cancellationToken).ConfigureAwait(false);
            }
            if (target.ExplicitEndpoint == null && target.BuiltInEndpoint.HasValue && builtInOverride != null) {
                return await builtInOverride(target.BuiltInEndpoint.Value, name, recordType, cancellationToken).ConfigureAwait(false);
            }

            // Start before obtaining the client so first-use setup is included in both modes.
            var stopwatch = Stopwatch.StartNew();
            ClientX client = retainedClient != null ? retainedClient() : CreateClient(target, options);
            try {
                return await ExecuteWithClientAsync(client, target.DisplayName, name, recordType,
                    options.RequestDnsSec || target.ExplicitEndpoint?.DnsSecOk == true,
                    options, stopwatch, retainedClient != null ? options.ConnectionMode : ResolverQueryConnectionMode.Cold,
                    cancellationToken).ConfigureAwait(false);
            } finally {
                if (retainedClient == null) {
                    await client.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        internal static ClientX CreateClient(ResolverExecutionTarget target, ResolverQueryRunOptions options) {
            ClientX client = ResolverExecutionClientFactory.CreateClient(target, new ResolverExecutionClientOptions {
                TimeoutMs = Math.Max(1, options.TimeoutMs),
                BootstrapResolver = options.BootstrapResolver,
                RequestNsid = options.RequestNsid,
                PortOverride = options.PortOverride,
                ForceDohWirePost = options.ForceDohWirePost
            });
            if (target.ExplicitEndpoint?.Timeout is TimeSpan timeout) {
                client.EndpointConfiguration.TimeOut = ResolverEndpointClientFactory.ToTimeoutMilliseconds(timeout);
            }
            return client;
        }

        private static async Task<ResolverQueryAttemptResult> ExecuteWithClientAsync(
            ClientX client,
            string displayName,
            string name,
            DnsRecordType recordType,
            bool requestDnsSec,
            ResolverQueryRunOptions options,
            Stopwatch stopwatch,
            ResolverQueryConnectionMode connectionMode,
            CancellationToken cancellationToken) {
            DnsRequestFormat requestFormat = client.EndpointConfiguration.RequestFormat;
            if (!DnsTransportCapabilities.Supports(requestFormat)) {
                return CreateUnsupportedAttemptResult(client, displayName, name, recordType, requestFormat, stopwatch.Elapsed, connectionMode);
            }

            try {
                DnsResponse response = await client.Resolve(
                    name, recordType, requestDnsSec, options.ValidateDnsSec,
                    retryOnTransient: false, maxRetries: options.MaxRetries,
                    retryDelayMs: options.RetryDelayMs, cancellationToken: cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                return new ResolverQueryAttemptResult {
                    Target = displayName,
                    RequestFormat = requestFormat,
                    Resolver = !string.IsNullOrWhiteSpace(response.ServerAddress) ? response.ServerAddress! : ResolverEndpointClientFactory.DescribeConfiguredResolver(client),
                    Response = response,
                    Elapsed = stopwatch.Elapsed,
                    TransportElapsed = response.RoundTripTime > TimeSpan.Zero ? response.RoundTripTime : null,
                    ConnectionMode = connectionMode
                };
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                stopwatch.Stop();
                return new ResolverQueryAttemptResult {
                    Target = displayName,
                    RequestFormat = requestFormat,
                    Resolver = ResolverEndpointClientFactory.DescribeConfiguredResolver(client),
                    Elapsed = stopwatch.Elapsed,
                    ConnectionMode = connectionMode,
                    Error = ex.Message
                };
            }
        }

        private static ResolverQueryAttemptResult CreateUnsupportedAttemptResult(
            ClientX client,
            string displayName,
            string name,
            DnsRecordType recordType,
            DnsRequestFormat requestFormat, TimeSpan elapsed, ResolverQueryConnectionMode connectionMode) {
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
                Elapsed = elapsed,
                ConnectionMode = connectionMode
            };
        }
    }
}
