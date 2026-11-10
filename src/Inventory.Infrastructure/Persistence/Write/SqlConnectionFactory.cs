using System.Data.Common;
using Inventory.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Inventory.Infrastructure.Persistence.Write;

public sealed class SqlConnectionFactory
{
    private readonly DatabaseOptions _databaseOptions;

    public SqlConnectionFactory(IOptions<DatabaseOptions> databaseOptions)
    {
        _databaseOptions = databaseOptions.Value;
    }

    public DbConnection CreateConnection()
    {
        return new SqlConnection(_databaseOptions.ConnectionString);
    }
}
