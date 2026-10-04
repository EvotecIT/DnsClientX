using System;
using System.IO;
using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="InternalLogger"/> helper.
    /// </summary>
    [Collection("NoParallel")]
    public class InternalLoggerTests {
        /// <summary>Console logging escapes controls without changing event data or treating an unformatted brace as a placeholder.</summary>
        [Fact]
        public void ConsoleOutputEscapesControlsButEventsKeepOriginalData() {
            var logger = new InternalLogger { IsDebug = true, IsProgress = true, IsWarning = true };
            LogEventArgs? debug = null;
            LogEventArgs? progress = null;
            LogEventArgs? warning = null;
            logger.OnDebugMessage += (_, args) => debug = args;
            logger.OnProgressMessage += (_, args) => progress = args;
            logger.OnWarningMessage += (_, args) => warning = args;

            using var output = new StringWriter();
            TextWriter originalOut = Console.Out;
            try {
                Console.SetOut(output);
                logger.WriteDebug("answer {literal}\u001b[31m");
                logger.WriteProgress("zone\u001b[2J", "query", 50);
                logger.WriteWarning("Resolver {0}", "bad\u001b[1m");
            } finally {
                Console.SetOut(originalOut);
            }

            Assert.Equal("answer {literal}\u001b[31m", debug?.FullMessage);
            Assert.Equal("zone\u001b[2J", progress?.ProgressActivity);
            Assert.Equal("Resolver bad\u001b[1m", warning?.FullMessage);
            string text = output.ToString();
            Assert.Equal(-1, text.IndexOf('\u001b'));
            Assert.Contains("answer {literal}\\u001B[31m", text, StringComparison.Ordinal);
            Assert.Contains("zone\\u001B[2J", text, StringComparison.Ordinal);
            Assert.Contains("Resolver bad\\u001B[1m", text, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures progress events set the expected percentage.
        /// </summary>
        [Fact]
        public void WriteProgress_SetsPercentage() {
            var logger = new InternalLogger();
            LogEventArgs? args = null;
            logger.OnProgressMessage += (_, e) => args = e;

            logger.WriteProgress("activity", "operation", 42);

            Assert.NotNull(args);
            Assert.Equal(42, args!.ProgressPercentage);
            Assert.Null(args.ProgressCurrentSteps);
            Assert.Null(args.ProgressTotalSteps);
        }

        /// <summary>
        /// Ensures step information is included with progress updates.
        /// </summary>
        [Fact]
        public void WriteProgress_WithSteps_SetsPercentage() {
            var logger = new InternalLogger();
            LogEventArgs? args = null;
            logger.OnProgressMessage += (_, e) => args = e;

            logger.WriteProgress("activity", "operation", 75, 1, 4);

            Assert.NotNull(args);
            Assert.Equal(75, args!.ProgressPercentage);
            Assert.Equal(1, args.ProgressCurrentSteps);
            Assert.Equal(4, args.ProgressTotalSteps);
        }
    }
}
