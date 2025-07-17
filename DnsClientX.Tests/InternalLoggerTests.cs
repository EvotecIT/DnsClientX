using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Tests for the <see cref="InternalLogger"/> helper.
    /// </summary>
    public class InternalLoggerTests {
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
