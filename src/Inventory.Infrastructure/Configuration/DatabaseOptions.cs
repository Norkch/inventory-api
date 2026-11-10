namespace Inventory.Infrastructure.Configuration;

public class DatabaseOptions
{
    public const string ConnectionStringName = "Inventory";
    public const string MissingConnectionStringMessage =
        "The ConnectionStrings__Inventory environment variable is required and cannot be empty.";

    public string ConnectionString { get; set; } = string.Empty;

    public bool HasConnectionString()
    {
        return !string.IsNullOrWhiteSpace(ConnectionString);
    }
}
