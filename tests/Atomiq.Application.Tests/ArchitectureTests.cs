using Atomiq.Domain;

namespace Atomiq.Application.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainAssemblyIsAvailableToApplicationTests()
    {
        Assert.Equal("Atomiq.Domain", typeof(DomainAssemblyMarker).Assembly.GetName().Name);
    }
}
