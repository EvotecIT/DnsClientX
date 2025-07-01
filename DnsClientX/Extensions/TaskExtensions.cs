using System;
using System.Threading.Tasks;

namespace DnsClientX {
    internal static class TaskExtensions {
        public static T RunSync<T>(this Task<T> task) {
            return task.GetAwaiter().GetResult();
        }

        public static void RunSync(this Task task) {
            task.GetAwaiter().GetResult();
        }

        public static T RunSync<T>(this Func<Task<T>> func) {
            return Task.Run(func).GetAwaiter().GetResult();
        }

        public static void RunSync(this Func<Task> func) {
            Task.Run(func).GetAwaiter().GetResult();
        }
    }
}
