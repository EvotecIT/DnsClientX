namespace DnsClientX {
    /// <summary>
    /// Settings for the DnsClientX library.
    /// Provides interface for setting logging levels and number of threads to use.
    /// </summary>
    public class Settings {
        /// <summary>
        /// The logger instance.
        /// </summary>
        protected static InternalLogger _logger = new InternalLogger();

        /// <summary>
        /// Gets the internal logger instance.
        /// </summary>
        public static InternalLogger Logger => _logger;

        /// <summary>
        /// Gets or sets a value indicating whether error logging is enabled.
        /// </summary>
        public bool Error {
            get => _logger.IsError;
            set => _logger.IsError = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether verbose logging is enabled.
        /// </summary>
        public bool Verbose {
            get => _logger.IsVerbose;
            set => _logger.IsVerbose = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether warning logging is enabled.
        /// </summary>
        public bool Warning {
            get => _logger.IsWarning;
            set => _logger.IsWarning = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether progress logging is enabled.
        /// </summary>
        public bool Progress {
            get => _logger.IsProgress;
            set => _logger.IsProgress = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether debug logging is enabled.
        /// </summary>
        public bool Debug {
            get => _logger.IsDebug;
            set => _logger.IsDebug = value;
        }

        /// <summary>
        /// The lock object
        /// </summary>
        protected readonly object _LockObject = new object();
    }
}
