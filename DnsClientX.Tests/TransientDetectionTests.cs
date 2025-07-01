using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using Xunit;

namespace DnsClientX.Tests {
    public class TransientDetectionTests {
        private static bool InvokeIsTransient(Exception ex) {
            MethodInfo method = typeof(ClientX).GetMethod("IsTransient", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (bool)method.Invoke(null, new object[] { ex })!;
        }

        private static bool InvokeIsTransientResponse(DnsResponse response) {
            MethodInfo method = typeof(ClientX).GetMethod("IsTransientResponse", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (bool)method.Invoke(null, new object[] { response })!;
        }

        [Fact]
        public void IsTransient_DnsClientExceptionWithServerFailure_ShouldBeTrue() {
            var response = new DnsResponse { Status = DnsResponseCode.ServerFailure };
            var ex = new DnsClientException("error", response);

            Assert.True(InvokeIsTransient(ex));
        }

        [Fact]
        public void IsTransient_HttpRequestException_ShouldBeTrue() {
            var ex = new HttpRequestException("network error");

            Assert.True(InvokeIsTransient(ex));
        }

        [Fact]
        public void IsTransient_TimeoutException_ShouldBeTrue() {
            var ex = new TimeoutException();

            Assert.True(InvokeIsTransient(ex));
        }

        [Fact]
        public void IsTransientResponse_ServerFailureWithError_ShouldBeTrue() {
            var response = new DnsResponse {
                Status = DnsResponseCode.ServerFailure,
                Error = "network error"
            };

            Assert.True(InvokeIsTransientResponse(response));
        }

        [Fact]
        public void IsTransientResponse_ServerFailureWithAnswers_ShouldBeFalse() {
            var answer = new DnsAnswer { Name = "a", Type = DnsRecordType.A, TTL = 60, DataRaw = "1.1.1.1" };
            var response = new DnsResponse {
                Status = DnsResponseCode.ServerFailure,
                Answers = new[] { answer }
            };

            Assert.False(InvokeIsTransientResponse(response));
        }

        [Theory]
        [InlineData(DnsResponseCode.Refused)]
        [InlineData(DnsResponseCode.NotImplemented)]
        public void IsTransientResponse_TransientCodes_ShouldBeTrue(DnsResponseCode code) {
            var response = new DnsResponse { Status = code };

            Assert.True(InvokeIsTransientResponse(response));
        }

        [Fact]
        public void IsTransientResponse_NxDomain_ShouldBeFalse() {
            var response = new DnsResponse { Status = DnsResponseCode.NXDomain };

            Assert.False(InvokeIsTransientResponse(response));
        }

        [Theory]
        [InlineData(SocketError.ConnectionReset)]
        [InlineData(SocketError.NetworkUnreachable)]
        public void IsTransient_SocketExceptionSpecificErrors_ShouldBeTrue(SocketError error) {
            var ex = new SocketException((int)error);

            Assert.True(InvokeIsTransient(ex));
        }
    }
}
