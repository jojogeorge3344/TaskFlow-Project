using Microsoft.Extensions.DependencyInjection;

namespace TaskFlow.IntegrationTests;

/// <summary>Every service and repository interface the API declares resolves from the running host, so a missing registration
/// fails here rather than on the first request that needs it.</summary>
public class DependencyInjectionTests : IClassFixture<TaskFlowApiFactory>
{
    private readonly TaskFlowApiFactory _factory;

    public DependencyInjectionTests(TaskFlowApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<Type> Contracts()
    {
        var data = new TheoryData<Type>();
        foreach (var contract in typeof(Program).Assembly.GetTypes()
                     .Where(type => type is { IsInterface: true, IsGenericTypeDefinition: false }
                                    && (type.Name.EndsWith("Service", StringComparison.Ordinal)
                                        || type.Name.EndsWith("Repository", StringComparison.Ordinal)
                                        || type.Name == "IUnitOfWork"))
                     .OrderBy(type => type.FullName))
        {
            data.Add(contract);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void EveryServiceAndRepositoryInterfaceResolves(Type contract)
    {
        using var scope = _factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService(contract));
    }
}
