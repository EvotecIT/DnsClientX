namespace DnsClientX {
    /// <summary>
    /// Settings for the DnsClientX library.
    /// Provides interface for setting logging levels and number of threads to use
    /// </summary>
    public class Settings {
        protected static InternalLogger _logger = new InternalLogger();

        public bool Error {
            get => _logger.IsError;
            set => _logger.IsError = value;
        }

        public bool Verbose {
            get => _logger.IsVerbose;
            set => _logger.IsVerbose = value;
        }

        public bool Warning {
            get => _logger.IsWarning;
            set => _logger.IsWarning = value;
        }

        public bool Progress {
            get => _logger.IsProgress;
            set => _logger.IsProgress = value;
        }

        public bool Debug {
            get => _logger.IsDebug;
            set => _logger.IsDebug = value;
        }

        protected readonly object _LockObject = new object();
    }
}
