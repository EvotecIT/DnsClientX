using System;
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

        /// <summary>
        /// Blocks the calling thread until the given <see cref="Task"/> completes.
        /// </summary>
        /// <param name="task">Task to wait for.</param>
        public static void RunSync(this Task task) {
            task.GetAwaiter().GetResult();
        }

        public static void RunSync(this Task task, CancellationToken cancellationToken) {
            if (task.IsCompleted) {
                if (cancellationToken.IsCancellationRequested) {
                    throw new TaskCanceledException(task);
                }

                task.GetAwaiter().GetResult();
                return;
            }

            var completed = Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken)).GetAwaiter().GetResult();

            if (completed != task) {
                throw new TaskCanceledException(task);
            }

            task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Executes the provided asynchronous delegate synchronously and returns its result.
        /// </summary>
        /// <typeparam name="T">Type of the task result.</typeparam>
        /// <param name="func">Asynchronous delegate to invoke.</param>
        /// <returns>Result returned by the delegate.</returns>
        public static T RunSync<T>(this Func<Task<T>> func) {
            return Task.Run(func).GetAwaiter().GetResult();
        }

        public static T RunSync<T>(this Func<Task<T>> func, CancellationToken cancellationToken) {
            var task = Task.Run(func, cancellationToken);
            return task.RunSync(cancellationToken);
        }

        /// <summary>
        /// Executes the provided asynchronous delegate synchronously.
        /// </summary>
        /// <param name="func">Asynchronous delegate to invoke.</param>
        /// <returns>A task representing the completion of <paramref name="func"/>.</returns>
        public static void RunSync(this Func<Task> func) {
            Task.Run(func).GetAwaiter().GetResult();
        }

        public static void RunSync(this Func<Task> func, CancellationToken cancellationToken) {
            var task = Task.Run(func, cancellationToken);
            task.RunSync(cancellationToken);
        }
    }
}
