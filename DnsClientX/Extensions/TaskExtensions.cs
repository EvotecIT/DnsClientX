using System.Threading;
using System.Threading.Tasks;

namespace DnsClientX {
    /// <summary>
    /// Provides helper methods to execute asynchronous tasks synchronously.
    /// </summary>
    internal static class TaskExtensions {
        /// <summary>
        /// Blocks the calling thread until the <see cref="Task{TResult}"/> completes and returns its result.
        /// </summary>
        /// <typeparam name="T">Type of the task result.</typeparam>
        /// <param name="task">Task to wait for.</param>
        /// <returns>Result of the completed task.</returns>
        public static T RunSync<T>(this Task<T> task) {
            return task.GetAwaiter().GetResult();
        }

        public static T RunSync<T>(this Task<T> task, CancellationToken cancellationToken) {
            if (task.IsCompleted) {
                if (cancellationToken.IsCancellationRequested) {
                    throw new TaskCanceledException(task);
                }

                return task.GetAwaiter().GetResult();
            }

            var completed = Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken)).GetAwaiter().GetResult();

            if (completed != task) {
                throw new TaskCanceledException(task);
            }

            return task.GetAwaiter().GetResult();
        }

    }
}
