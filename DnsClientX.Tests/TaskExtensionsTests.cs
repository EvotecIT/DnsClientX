using System;
using DnsClientX;
using System.Threading.Tasks;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Unit tests for the <see cref="TaskExtensions"/> helper methods.
    /// </summary>
    public class TaskExtensionsTests {
        /// <summary>
        /// Ensures <see cref="TaskExtensions.RunSync{T}(Task{T})"/> returns the task result.
        /// </summary>
        [Fact]
        public void RunSync_TaskOfT_ReturnsResult() {
            var task = Task.FromResult(5);
            int result = task.RunSync();
            Assert.Equal(5, result);
        }

        /// <summary>
        /// Throws when the provided task of T is cancelled.
        /// </summary>
        [Fact]
        public void RunSync_TaskOfT_Cancelled() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var task = Task.Delay(1000, cts.Token).ContinueWith(_ => 1, TaskContinuationOptions.ExecuteSynchronously);
            Assert.Throws<TaskCanceledException>(() => task.RunSync(cts.Token));
        }

    }
}
