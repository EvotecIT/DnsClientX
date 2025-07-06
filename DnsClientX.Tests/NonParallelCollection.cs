using Xunit;

namespace DnsClientX.Tests {
    [CollectionDefinition("NonParallel", DisableParallelization = true)]
    public class NonParallelCollection : ICollectionFixture<object> { }
}
