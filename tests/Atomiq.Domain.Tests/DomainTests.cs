using Atomiq.Domain;

namespace Atomiq.Domain.Tests;

public sealed class DomainTests
{
    [Fact]
    public void DomainMarkerBelongsToDomainAssembly()
    {
        Assert.Equal("Atomiq.Domain", typeof(DomainAssemblyMarker).Assembly.GetName().Name);
    }
}
