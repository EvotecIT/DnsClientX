using System;
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

        /// <summary>
        /// Blocks the calling thread until the given <see cref="Task"/> completes.
        /// </summary>
        /// <param name="task">Task to wait for.</param>
        public static void RunSync(this Task task) {
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

        /// <summary>
        /// Executes the provided asynchronous delegate synchronously.
        /// </summary>
        /// <param name="func">Asynchronous delegate to invoke.</param>
        public static void RunSync(this Func<Task> func) {
            Task.Run(func).GetAwaiter().GetResult();
        }
    }
}
