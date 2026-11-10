using System.Data;
using System.Data.Common;
using Dapper;
using Inventory.Application.Movements;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.Data.SqlClient;

namespace Inventory.Infrastructure.Persistence.Write;

public sealed class MovementWriteRepository : IMovementWriteRepository
{
    private const int ArithmeticOverflow = 8115;

    private const string ApplyStockDeltaSql = @"
UPDATE dbo.Products
   SET StockQuantity = StockQuantity + @Delta
OUTPUT INSERTED.StockQuantity
 WHERE Id = @ProductId AND IsActive = 1
   AND StockQuantity >= -@Delta;";

    private const string ExistsActiveProductSql = @"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsActive = 1
) THEN 1 ELSE 0 END AS BIT);";

    private const string InsertMovementSql = @"
INSERT INTO dbo.InventoryMovements (Id, ProductId, SupplierId, MovementType, Quantity, StockAfter, Reason, OccurredAtUtc, RegisteredBy)
VALUES (@Id, @ProductId, NULL, @MovementType, @Quantity, @StockAfter, @Reason, @OccurredAtUtc, @RegisteredBy);";

    private readonly SqlConnectionFactory _connectionFactory;

    public MovementWriteRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<MovementRegistrationResult> RegisterAsync(InventoryMovement movement, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await ApplyStockDeltaAndInsertMovementAsync(movement, transaction, cancellationToken);
            await CommitIfRegisteredAsync(result, transaction, cancellationToken);

            return result;
        }
        catch (SqlException exception) when (exception.Number == ArithmeticOverflow)
        {
            return MovementRegistrationResult.Rejected(MovementRegistrationStatus.StockOverflow);
        }
    }

    private static async Task<MovementRegistrationResult> ApplyStockDeltaAndInsertMovementAsync(InventoryMovement movement, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var connection = transaction.Connection!;
        var stockAfter = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(ApplyStockDeltaSql, CreateStockParameters(movement), transaction, cancellationToken: cancellationToken));
        if (stockAfter is null)
        {
            return MovementRegistrationResult.Rejected(await DiagnoseRejectionAsync(movement, transaction, cancellationToken));
        }
        var registered = movement.WithStockAfter(stockAfter.Value);
        await InsertMovementAsync(registered, transaction, cancellationToken);

        return MovementRegistrationResult.Registered(registered);
    }

    private static Task InsertMovementAsync(InventoryMovement movement, DbTransaction transaction, CancellationToken cancellationToken)
    {
        return transaction.Connection!.ExecuteAsync(new CommandDefinition(InsertMovementSql, CreateInsertParameters(movement), transaction, cancellationToken: cancellationToken));
    }

    private static async Task<MovementRegistrationStatus> DiagnoseRejectionAsync(InventoryMovement movement, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("ProductId", movement.ProductId, DbType.Guid);
        var isActive = await transaction.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition(ExistsActiveProductSql, parameters, transaction, cancellationToken: cancellationToken));

        return isActive ? MovementRegistrationStatus.InsufficientStock : MovementRegistrationStatus.ProductNotFound;
    }

    private static async Task CommitIfRegisteredAsync(MovementRegistrationResult result, DbTransaction transaction, CancellationToken cancellationToken)
    {
        if (result.Status == MovementRegistrationStatus.Registered)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static DynamicParameters CreateStockParameters(InventoryMovement movement)
    {
        var parameters = new DynamicParameters();
        parameters.Add("ProductId", movement.ProductId, DbType.Guid);
        parameters.Add("Delta", movement.StockDelta, DbType.Int32);

        return parameters;
    }

    private static DynamicParameters CreateInsertParameters(InventoryMovement movement)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", movement.Id, DbType.Guid);
        parameters.Add("ProductId", movement.ProductId, DbType.Guid);
        parameters.Add("MovementType", (byte)movement.Type, DbType.Byte);
        parameters.Add("Quantity", movement.Quantity, DbType.Int32);
        parameters.Add("StockAfter", movement.StockAfter, DbType.Int32);
        parameters.Add("Reason", movement.Reason, DbType.String, size: MovementRules.ReasonMaxLength);
        parameters.Add("OccurredAtUtc", movement.OccurredAtUtc, DbType.DateTime2);
        parameters.Add("RegisteredBy", movement.RegisteredBy, DbType.String, size: MovementRules.RegisteredByMaxLength);

        return parameters;
    }
}
