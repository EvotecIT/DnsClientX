using Xunit;

namespace DnsClientX.Tests {
    /// <summary>
    /// Collection definition to ensure disposal tests don't run in parallel.
    /// This prevents shared static state interference between tests.
    /// </summary>
    [CollectionDefinition("DisposalTests", DisableParallelization = true)]
    public class DisposalTestCollection {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }
}
