using System.Data;
using System.Data.Common;
using Dapper;
using Inventory.Application.ProductSuppliers;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.Data.SqlClient;

namespace Inventory.Infrastructure.Persistence.Write;

public sealed class ProductSupplierWriteRepository : IProductSupplierWriteRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private const string InsertWhileBothAreActiveAndReadSql = @"
INSERT INTO dbo.ProductSuppliers (Id, ProductId, SupplierId, SupplierSku, CreatedAtUtc)
SELECT @Id, @ProductId, @SupplierId, @SupplierSku, @CreatedAtUtc
 WHERE EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsActive = 1)
   AND EXISTS (SELECT 1 FROM dbo.Suppliers WITH (UPDLOCK, HOLDLOCK)
                WHERE Id = @SupplierId AND IsActive = 1);
SELECT ps.ProductId, p.Name AS ProductName, ps.SupplierId, s.Name AS SupplierName, ps.SupplierSku, ps.CreatedAtUtc
  FROM dbo.ProductSuppliers ps
  JOIN dbo.Products p ON p.Id = ps.ProductId
  JOIN dbo.Suppliers s ON s.Id = ps.SupplierId
 WHERE ps.Id = @Id;";

    private const string ExistsActiveProductSql = @"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Products WHERE Id = @ProductId AND IsActive = 1
) THEN 1 ELSE 0 END AS BIT);";

    private readonly SqlConnectionFactory _connectionFactory;

    public ProductSupplierWriteRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ProductSupplierLinkResult> LinkAsync(ProductSupplier link, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        try
        {
            var linked = await connection.QuerySingleOrDefaultAsync<ProductSupplierDto>(new CommandDefinition(InsertWhileBothAreActiveAndReadSql, CreateInsertParameters(link), cancellationToken: cancellationToken));

            return linked is null
                ? ProductSupplierLinkResult.Rejected(await DiagnoseRejectionAsync(link, connection, cancellationToken))
                : ProductSupplierLinkResult.Linked(linked);
        }
        catch (SqlException exception) when (IsUniqueViolation(exception))
        {
            return ProductSupplierLinkResult.Rejected(ProductSupplierLinkStatus.AlreadyLinked);
        }
    }

    private static async Task<ProductSupplierLinkStatus> DiagnoseRejectionAsync(ProductSupplier link, DbConnection connection, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("ProductId", link.ProductId, DbType.Guid);
        var isProductActive = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(ExistsActiveProductSql, parameters, cancellationToken: cancellationToken));

        return isProductActive ? ProductSupplierLinkStatus.SupplierNotAvailable : ProductSupplierLinkStatus.ProductNotFound;
    }

    private static DynamicParameters CreateInsertParameters(ProductSupplier link)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", link.Id, DbType.Guid);
        parameters.Add("ProductId", link.ProductId, DbType.Guid);
        parameters.Add("SupplierId", link.SupplierId, DbType.Guid);
        parameters.Add("SupplierSku", link.SupplierSku, DbType.String, size: ProductSupplierRules.SupplierSkuMaxLength);
        parameters.Add("CreatedAtUtc", link.CreatedAtUtc, DbType.DateTime2);

        return parameters;
    }

    private static bool IsUniqueViolation(SqlException exception)
    {
        return exception.Number == UniqueIndexViolation || exception.Number == UniqueConstraintViolation;
    }
}
