using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Test collection used to disable parallel execution for tests that are not thread safe.
    /// </summary>
    [CollectionDefinition("NoParallel", DisableParallelization = true)]
    public class NoParallelCollection : ICollectionFixture<object> { }
}
