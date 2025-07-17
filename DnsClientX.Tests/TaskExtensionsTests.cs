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
        /// Waits for task completion when running synchronously.
        /// </summary>
        [Fact]
        public void RunSync_Task_WaitsForCompletion() {
            bool ran = false;
            Task task = Task.Run(() => ran = true);
            task.RunSync();
            Assert.True(ran);
        }

        /// <summary>
        /// Executes a function returning a value on the thread pool and waits for completion.
        /// </summary>
        [Fact]
        public void RunSync_FuncOfT_ReturnsResult() {
            int result = ((Func<Task<int>>)(() => Task.FromResult(7))).RunSync();
            Assert.Equal(7, result);
        }
        /// <summary>
        /// Executes an async function and waits for completion.
        /// </summary>
        [Fact]
        public void RunSync_Func_WaitsForCompletion() {
            bool ran = false;
            ((Func<Task>)(() => { ran = true; return Task.CompletedTask; })).RunSync();
            Assert.True(ran);
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

        /// <summary>
        /// Throws when the provided task is cancelled.
        /// </summary>
        [Fact]
        public void RunSync_Task_Cancelled() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Task task = Task.Delay(1000, cts.Token);
            Assert.Throws<TaskCanceledException>(() => task.RunSync(cts.Token));
        }
    }
}
