using JobProcessor.Worker.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace JobProcessor.Worker.Tests.Helpers;

public static class ScopeFactoryHelper
{
    public static Mock<IServiceScopeFactory> Create(Mock<IJobRepository> repositoryMock)
    {
        var providerMock = new Mock<IServiceProvider>();
        providerMock
            .Setup(p => p.GetService(typeof(IJobRepository)))
            .Returns(repositoryMock.Object);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);

        var asyncScope = new AsyncServiceScope(scopeMock.Object);

        var factoryMock = new Mock<IServiceScopeFactory>();
        factoryMock
            .Setup(f => f.CreateScope())
            .Returns(scopeMock.Object);

        return factoryMock;
    }
}
