using System;

namespace DnsClientX;

/// <summary>
/// Internal logger that allows writing diagnostic information to various sinks.
/// </summary>
/// <remarks>
/// The logger is used internally by <see cref="ClientX"/> and can be subscribed to for troubleshooting output.
/// </remarks>
public class InternalLogger {
    private readonly object _lock = new object();

    private static void WriteConsoleLine(string message) => Console.WriteLine(DnsTerminalText.Escape(message));

    /// <summary>
    /// Occurs when a verbose message is logged.
    /// </summary>
    public event EventHandler<LogEventArgs>? OnVerboseMessage;

    /// <summary>
    /// Occurs when a warning message is logged.
    /// </summary>
    public event EventHandler<LogEventArgs>? OnWarningMessage;

    /// <summary>
    /// Occurs when an error message is logged.
    /// </summary>
    public event EventHandler<LogEventArgs>? OnErrorMessage;

    /// <summary>
    /// Occurs when a debug message is logged.
    /// </summary>
    public event EventHandler<LogEventArgs>? OnDebugMessage;

    /// <summary>
    /// Occurs when a progress message is logged.
    /// </summary>
    public event EventHandler<LogEventArgs>? OnProgressMessage;

    /// <summary>
    /// Occurs when an information message is logged.
    /// </summary>
    public event EventHandler<LogEventArgs>? OnInformationMessage;

    /// <summary>
    /// Gets or sets a value indicating whether verbose messages should be logged.
    /// </summary>
    public bool IsVerbose { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether error messages should be logged.
    /// </summary>
    public bool IsError { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether warning messages should be logged.
    /// </summary>
    public bool IsWarning { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether debug messages should be logged.
    /// </summary>
    public bool IsDebug { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether information messages should be logged.
    /// </summary>
    public bool IsInformation { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether progress messages should be logged.
    /// </summary>
    public bool IsProgress { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalLogger"/> class.
    /// </summary>
    /// <param name="isVerbose">If set to <c>true</c>, verbose messages will be logged.</param>
    public InternalLogger(bool isVerbose = false) {
        IsVerbose = isVerbose;
    }

    /// <summary>
    /// Writes a progress message to the console and invokes the OnProgressMessage event. The
    /// <see cref="LogEventArgs.ProgressPercentage"/> property of the event is set to
    /// <paramref name="percentCompleted"/>.
    /// </summary>
    /// <param name="activity">The activity being logged.</param>
    /// <param name="currentOperation">The current operation being logged.</param>
    /// <param name="percentCompleted">The percentage of the operation that is completed.</param>
    /// <param name="currentSteps">The current step of the operation (optional).</param>
    /// <param name="totalSteps">The total steps of the operation (optional).</param>
    public void WriteProgress(string activity, string currentOperation, int percentCompleted, int? currentSteps = null, int? totalSteps = null) {
        lock (_lock) {
            OnProgressMessage?.Invoke(this, new LogEventArgs(activity, currentOperation, currentSteps, totalSteps, percentCompleted));
            if (IsProgress) {
                if (currentSteps.HasValue && totalSteps.HasValue) {
                    WriteConsoleLine($"[progress] activity: {activity} / operation: {currentOperation} / percent completed: {percentCompleted}% ({currentSteps} out of {totalSteps})");
                } else {
                    WriteConsoleLine($"[progress] activity: {activity} / operation: {currentOperation} / percent completed: {percentCompleted}%");
                }
            }
        }
    }

    /// <summary>
    /// Writes an error message to the console and invokes the OnErrorMessage event.
    /// </summary>
    /// <param name="message">The error message to be logged.</param>
    public void WriteError(string message) {
        lock (_lock) {
            OnErrorMessage?.Invoke(this, new LogEventArgs(message));
            if (IsError) {
                WriteConsoleLine("[error] " + message);
            }
        }
    }

    /// <summary>
    /// Writes a formatted error message to the console and invokes the OnErrorMessage event.
    /// </summary>
    /// <param name="message">The error message to be logged, with format items.</param>
    /// <param name="args">An array of objects to write using format.</param>
    public void WriteError(string message, params object[] args) {
        lock (_lock) {
            var entry = new LogEventArgs(message, args);
            OnErrorMessage?.Invoke(this, new LogEventArgs(entry.FullMessage));
            if (IsError) {
                WriteConsoleLine("[error] " + entry.FullMessage);
            }
        }
    }

    /// <summary>
    /// Writes a warning message to the console and invokes the OnWarningMessage event.
    /// </summary>
    /// <param name="message">The warning message to be logged.</param>
    public void WriteWarning(string message) {
        lock (_lock) {
            OnWarningMessage?.Invoke(this, new LogEventArgs(message));
            if (IsWarning) {
                WriteConsoleLine("[warning] " + message);
            }
        }
    }

    /// <summary>
    /// Writes a formatted warning message to the console and invokes the OnWarningMessage event.
    /// </summary>
    /// <param name="message">The warning message to be logged, with format items.</param>
    /// <param name="args">An array of objects to write using format.</param>
    public void WriteWarning(string message, params object[] args) {
        lock (_lock) {
            var entry = new LogEventArgs(message, args);
            OnWarningMessage?.Invoke(this, new LogEventArgs(entry.FullMessage));
            if (IsWarning) {
                WriteConsoleLine("[warning] " + entry.FullMessage);
            }
        }
    }

    /// <summary>
    /// Writes a verbose message to the console and invokes the OnVerboseMessage event.
    /// </summary>
    /// <param name="message">The verbose message to be logged.</param>
    public void WriteVerbose(string message) {
        lock (_lock) {
            OnVerboseMessage?.Invoke(this, new LogEventArgs(message));
            if (IsVerbose) {
                WriteConsoleLine(message);
            }
        }
    }

    /// <summary>
    /// Writes a formatted verbose message to the console and invokes the OnVerboseMessage event.
    /// </summary>
    /// <param name="message">The verbose message to be logged, with format items.</param>
    /// <param name="args">An array of objects to write using format.</param>
    public void WriteVerbose(string message, params object[] args) {
        lock (_lock) {
            var entry = new LogEventArgs(message, args);
            OnVerboseMessage?.Invoke(this, entry);
            if (IsVerbose) {
                WriteConsoleLine(entry.FullMessage);
            }
        }
    }

    /// <summary>
    /// Writes a debug message to the console and invokes the OnDebugMessage event.
    /// </summary>
    /// <param name="message">The debug message to be logged.</param>
    /// <param name="args">An array of objects to write using format.</param>
    public void WriteDebug(string message, params object[] args) {
        lock (_lock) {
            var entry = new LogEventArgs(message, args);
            OnDebugMessage?.Invoke(this, entry);
            if (IsDebug) {
                WriteConsoleLine("[debug] " + entry.FullMessage);
            }
        }
    }

    /// <summary>
    /// Writes an information message to the console and invokes the OnInformationMessage event.
    /// </summary>
    /// <param name="message">The information message to be logged.</param>
    /// <param name="args">An array of objects to write using format.</param>
    public void WriteInformation(string message, params object[] args) {
        lock (_lock) {
            var entry = new LogEventArgs(message, args);
            OnInformationMessage?.Invoke(this, entry);
            if (IsInformation) {
                WriteConsoleLine("[information] " + entry.FullMessage);
            }
        }
    }
}

/// <summary>
/// Represents the arguments for a log event.
/// </summary>
/// <seealso cref="System.EventArgs" />
public class LogEventArgs : EventArgs {
    /// <summary>
    /// Progress percentage
    /// </summary>
    public int? ProgressPercentage { get; set; }

    /// <summary>
    /// Progress total steps
    /// </summary>
    public int? ProgressTotalSteps { get; set; }

    /// <summary>
    /// Progress current steps
    /// </summary>
    public int? ProgressCurrentSteps { get; set; }

    /// <summary>
    /// Progress current operation
    /// </summary>
    public string ProgressCurrentOperation { get; set; } = string.Empty;

    /// <summary>
    /// Progress activity
    /// </summary>
    public string ProgressActivity { get; set; } = string.Empty;

    /// <summary>
    /// Message to be written including arguments substitution
    /// </summary>
    public string FullMessage { get; set; } = string.Empty;

    /// <summary>
    /// Message to be written
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments.
    /// </summary>
    /// <value>
    /// The arguments.
    /// </value>
    public object[] Args { get; set; } = Array.Empty<object>();

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEventArgs"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="args">The arguments.</param>
    public LogEventArgs(string message, object[] args) {
        Message = message;
        Args = args;
        FullMessage = args.Length == 0 ? message : string.Format(message, args);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEventArgs"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public LogEventArgs(string message) {
        Message = message;
        FullMessage = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogEventArgs"/> class.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="currentOperation">The current operation.</param>
    /// <param name="currentSteps">The current steps.</param>
    /// <param name="totalSteps">The total steps.</param>
    /// <param name="percentage">The progress percentage.</param>
    public LogEventArgs(string activity, string currentOperation, int? currentSteps, int? totalSteps, int? percentage) {
        ProgressActivity = activity;
        ProgressCurrentOperation = currentOperation;
        ProgressCurrentSteps = currentSteps;
        ProgressTotalSteps = totalSteps;
        ProgressPercentage = percentage;
    }
}
