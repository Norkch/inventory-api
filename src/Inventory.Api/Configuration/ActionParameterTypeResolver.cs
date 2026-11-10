using Microsoft.AspNetCore.Mvc.Abstractions;

namespace Inventory.Api.Configuration;

public static class ActionParameterTypeResolver
{
    private static readonly Type[] SimpleTypes = { typeof(string), typeof(Guid), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan) };

    public static Type? Resolve(ActionDescriptor actionDescriptor, string modelStateKey)
    {
        foreach (var parameter in actionDescriptor.Parameters)
        {
            var type = ResolveInParameter(parameter, modelStateKey);
            if (type is not null)
            {
                return UnwrapNullable(type);
            }
        }

        return null;
    }

    private static Type? ResolveInParameter(ParameterDescriptor parameter, string modelStateKey)
    {
        if (string.Equals(parameter.Name, modelStateKey, StringComparison.OrdinalIgnoreCase))
        {
            return parameter.ParameterType;
        }
        if (IsSimpleType(parameter.ParameterType))
        {
            return null;
        }

        return FindPropertyType(parameter.ParameterType, RemoveParameterPrefix(parameter, modelStateKey));
    }

    private static Type? FindPropertyType(Type parameterType, string propertyName)
    {
        var property = parameterType.GetProperties()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        return property?.PropertyType;
    }

    private static string RemoveParameterPrefix(ParameterDescriptor parameter, string modelStateKey)
    {
        var prefix = parameter.Name + ".";

        return modelStateKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? modelStateKey.Substring(prefix.Length) : modelStateKey;
    }

    private static bool IsSimpleType(Type type)
    {
        var underlyingType = UnwrapNullable(type);

        return underlyingType.IsPrimitive || underlyingType.IsEnum || SimpleTypes.Contains(underlyingType);
    }

    private static Type UnwrapNullable(Type type)
    {
        return Nullable.GetUnderlyingType(type) ?? type;
    }
}
