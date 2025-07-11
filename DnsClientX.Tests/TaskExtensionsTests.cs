using System;
using DnsClientX;
using System.Threading.Tasks;
using System.Threading;
using Xunit;

namespace DnsClientX.Tests {
    public class TaskExtensionsTests {
        [Fact]
        public void RunSync_TaskOfT_ReturnsResult() {
            var task = Task.FromResult(5);
            int result = task.RunSync();
            Assert.Equal(5, result);
        }

        [Fact]
        public void RunSync_Task_WaitsForCompletion() {
            bool ran = false;
            Task task = Task.Run(() => ran = true);
            task.RunSync();
            Assert.True(ran);
        }

        [Fact]
        public void RunSync_FuncOfT_ReturnsResult() {
            int result = ((Func<Task<int>>)(() => Task.FromResult(7))).RunSync();
            Assert.Equal(7, result);
        }
        [Fact]
        public void RunSync_Func_WaitsForCompletion() {
            bool ran = false;
            ((Func<Task>)(() => { ran = true; return Task.CompletedTask; })).RunSync();
            Assert.True(ran);
        }

        [Fact]
        public void RunSync_TaskOfT_Cancelled() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var task = Task.Delay(1000, cts.Token).ContinueWith(_ => 1, TaskContinuationOptions.ExecuteSynchronously);
            Assert.Throws<TaskCanceledException>(() => task.RunSync(cts.Token));
        }

        [Fact]
        public void RunSync_Task_Cancelled() {
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Task task = Task.Delay(1000, cts.Token);
            Assert.Throws<TaskCanceledException>(() => task.RunSync(cts.Token));
        }
    }
}
