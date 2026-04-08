using System;
using System.Collections.Generic;
using System.Linq;

namespace DnsClientX {
    /// <summary>
    /// Builds normalized resolver execution targets for CLI, PowerShell, and other adapters.
    /// </summary>
    public static class ResolverExecutionPlanBuilder {
        /// <summary>
        /// Builds the execution target represented by a saved resolver selection.
        /// </summary>
        /// <param name="selection">The resolver selection to normalize.</param>
        /// <returns>A single execution target.</returns>
        public static ResolverExecutionTarget BuildSelectionTarget(ResolverSelectionResult selection) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            if (selection.Kind == ResolverSelectionKind.BuiltInEndpoint && selection.BuiltInEndpoint.HasValue) {
                DnsEndpoint endpoint = selection.BuiltInEndpoint.Value;
                return new ResolverExecutionTarget {
                    DisplayName = endpoint.ToString(),
                    BuiltInEndpoint = endpoint
                };
            }

            if (selection.Kind == ResolverSelectionKind.ExplicitEndpoint && selection.ExplicitEndpoint != null) {
                return BuildExplicitTarget(selection.ExplicitEndpoint);
            }

            throw new ArgumentException($"Resolver selection '{selection.Target}' could not be applied.", nameof(selection));
        }

        /// <summary>
        /// Builds normalized execution targets for the supplied explicit resolver endpoints.
        /// </summary>
        /// <param name="endpoints">The explicit resolver endpoints to normalize.</param>
        /// <returns>A de-duplicated array of execution targets.</returns>
        public static ResolverExecutionTarget[] BuildExplicitTargets(IEnumerable<DnsResolverEndpoint> endpoints) {
            if (endpoints == null) {
                throw new ArgumentNullException(nameof(endpoints));
            }

            return endpoints
                .GroupBy(DescribeEndpoint, StringComparer.OrdinalIgnoreCase)
                .Select(group => BuildExplicitTarget(group.First()))
                .ToArray();
        }

        /// <summary>
        /// Builds normalized execution targets for the supplied built-in endpoints.
        /// </summary>
        /// <param name="endpoints">The built-in endpoints to normalize.</param>
        /// <returns>A distinct array of execution targets.</returns>
        public static ResolverExecutionTarget[] BuildBuiltInTargets(IEnumerable<DnsEndpoint> endpoints) {
            if (endpoints == null) {
                throw new ArgumentNullException(nameof(endpoints));
            }

            return endpoints
                .Distinct()
                .Select(endpoint => new ResolverExecutionTarget {
                    DisplayName = endpoint.ToString(),
                    BuiltInEndpoint = endpoint
                })
                .ToArray();
        }

        /// <summary>
        /// Builds the effective probe targets for a built-in resolver profile.
        /// </summary>
        /// <param name="endpoint">The built-in resolver profile to expand.</param>
        /// <returns>The probe targets that should be tested together.</returns>
        public static ResolverExecutionTarget[] BuildProbeTargets(DnsEndpoint endpoint) {
            return BuildBuiltInTargets(ProbePlanBuilder.BuildPlan(endpoint));
        }

        /// <summary>
        /// Describes an explicit resolver endpoint using its effective transport and endpoint string.
        /// </summary>
        /// <param name="endpoint">The endpoint to describe.</param>
        /// <returns>A stable human-readable endpoint description.</returns>
        public static string DescribeEndpoint(DnsResolverEndpoint endpoint) {
            if (endpoint == null) {
                throw new ArgumentNullException(nameof(endpoint));
            }

            DnsRequestFormat requestFormat = endpoint.RequestFormat ?? DnsRequestFormatMapper.FromTransport(endpoint.Transport);
            string prefix = requestFormat switch {
                DnsRequestFormat.DnsOverHttp3 => "doh3",
                DnsRequestFormat.DnsOverHttp2 => "doh2",
                DnsRequestFormat.DnsOverQuic => "doq",
                _ => DnsRequestFormatMapper.ToTransport(requestFormat).ToString().ToLowerInvariant()
            };
            return $"{prefix}@{endpoint}";
        }

        private static ResolverExecutionTarget BuildExplicitTarget(DnsResolverEndpoint endpoint) {
            return new ResolverExecutionTarget {
                DisplayName = DescribeEndpoint(endpoint),
                ExplicitEndpoint = endpoint
            };
        }
    }
}
