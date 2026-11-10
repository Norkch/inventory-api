using System.Reflection;
using Inventory.Application.Categories;

namespace Inventory.UnitTests.Architecture;

public sealed class CqrsSeparationTests
{
    private const string CommandSuffix = "Command";
    private const string QuerySuffix = "Query";
    private const string ReadPortSuffix = "ReadRepository";
    private const string WritePortSuffix = "WriteRepository";

    [Fact]
    public void CommandHandlers_DoNotDependOnReadPorts()
    {
        var commandHandlers = FindHandlersOf(CommandSuffix);
        Assert.True(commandHandlers.Count >= 2);
        Assert.Empty(commandHandlers.Where(handler => DependsOnPortEndingWith(handler, ReadPortSuffix)));
    }

    [Fact]
    public void QueryHandlers_DoNotDependOnWritePorts()
    {
        var queryHandlers = FindHandlersOf(QuerySuffix);
        Assert.True(queryHandlers.Count >= 2);
        Assert.Empty(queryHandlers.Where(handler => DependsOnPortEndingWith(handler, WritePortSuffix)));
    }

    private static List<Type> FindHandlersOf(string requestSuffix)
    {
        return typeof(CreateCategoryHandler).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Handler", StringComparison.Ordinal))
            .Where(type => HandlesRequestEndingWith(type, requestSuffix))
            .ToList();
    }

    private static bool HandlesRequestEndingWith(Type handler, string requestSuffix)
    {
        var handleMethod = handler.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Instance);
        var requestType = handleMethod?.GetParameters().FirstOrDefault()?.ParameterType;

        return requestType is not null && requestType.Name.EndsWith(requestSuffix, StringComparison.Ordinal);
    }

    private static bool DependsOnPortEndingWith(Type handler, string portSuffix)
    {
        return handler.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType.IsInterface && parameter.ParameterType.Name.EndsWith(portSuffix, StringComparison.Ordinal));
    }
}
