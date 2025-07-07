using Xunit;

namespace DnsClientX.Tests {
    [CollectionDefinition("NoParallel", DisableParallelization = true)]
    public class NoParallelCollection : ICollectionFixture<object> { }
}
