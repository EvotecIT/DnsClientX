using System;

namespace DnsClientX {
    public partial class ClientX {
        private static void ValidateTimeout(int timeOutMilliseconds) {
            if (timeOutMilliseconds < 1) {
                throw new ArgumentOutOfRangeException(nameof(timeOutMilliseconds), "Timeout must be greater than zero.");
            }
        }
    }
}
