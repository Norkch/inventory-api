using System.Data;
using Dapper;
using Inventory.Application.Products;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;
using Microsoft.Data.SqlClient;

namespace Inventory.Infrastructure.Persistence.Write;

public sealed class ProductWriteRepository : IProductWriteRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private const string FindActiveCategorySql = @"
SELECT Id, Name FROM dbo.Categories WHERE Id = @CategoryId AND IsActive = 1;";

    private const string ExistsWithSameSkuSql = @"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Products WHERE Sku = @Sku
) THEN 1 ELSE 0 END AS BIT);";

    private const string InsertWhileCategoryIsActiveSql = @"
INSERT INTO dbo.Products (Id, CategoryId, Sku, Name, Description, StockQuantity, IsActive, CreatedAtUtc, UpdatedAtUtc)
SELECT @Id, @CategoryId, @Sku, @Name, @Description, @StockQuantity, @IsActive, @CreatedAtUtc, NULL
 WHERE EXISTS (SELECT 1 FROM dbo.Categories WITH (UPDLOCK, HOLDLOCK)
                WHERE Id = @CategoryId AND IsActive = 1);";

    private const string ExistsActiveByIdSql = @"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Products WHERE Id = @Id AND IsActive = 1
) THEN 1 ELSE 0 END AS BIT);";

    private const string UpdateWhileCategoryIsActiveSql = @"
UPDATE dbo.Products
   SET Name = @Name, Description = @Description, CategoryId = @CategoryId, UpdatedAtUtc = @UpdatedAtUtc
OUTPUT INSERTED.Id, INSERTED.CategoryId, INSERTED.Sku, INSERTED.Name, INSERTED.Description,
       INSERTED.StockQuantity, INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc
 WHERE Id = @Id AND IsActive = 1
   AND EXISTS (SELECT 1 FROM dbo.Categories WITH (UPDLOCK, HOLDLOCK)
                WHERE Id = @CategoryId AND IsActive = 1);";

    private const string DeactivateSql = @"
UPDATE dbo.Products SET IsActive = 0, UpdatedAtUtc = @UpdatedAtUtc
 WHERE Id = @Id AND IsActive = 1;";

    private readonly SqlConnectionFactory _connectionFactory;

    public ProductWriteRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CategoryReference?> FindActiveCategoryAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("CategoryId", categoryId, DbType.Guid);
        await using var connection = _connectionFactory.CreateConnection();

        return await connection.QuerySingleOrDefaultAsync<CategoryReference>(new CommandDefinition(FindActiveCategorySql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<bool> ExistsWithSameSkuAsync(Product product, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Sku", product.Sku, DbType.String, size: ProductRules.SkuMaxLength);
        await using var connection = _connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(ExistsWithSameSkuSql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<bool> AddAsync(Product product, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        try
        {
            var insertedRows = await connection.ExecuteAsync(new CommandDefinition(InsertWhileCategoryIsActiveSql, CreateInsertParameters(product), cancellationToken: cancellationToken));

            return insertedRows == 1;
        }
        catch (SqlException exception) when (IsUniqueViolation(exception))
        {
            throw new ConflictException(ProductMessages.SkuAlreadyExists(product));
        }
    }

    public async Task<bool> ExistsActiveAsync(Guid productId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", productId, DbType.Guid);
        await using var connection = _connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(ExistsActiveByIdSql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<Product?> UpdateAsync(ProductChanges changes, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var snapshot = await connection.QuerySingleOrDefaultAsync<ProductSnapshot>(new CommandDefinition(UpdateWhileCategoryIsActiveSql, CreateUpdateParameters(changes), cancellationToken: cancellationToken));

        return snapshot is null ? null : Product.Restore(snapshot);
    }

    public async Task<bool> DeleteAsync(ProductDeletion deletion, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", deletion.ProductId, DbType.Guid);
        parameters.Add("UpdatedAtUtc", deletion.DeletedAtUtc, DbType.DateTime2);
        await using var connection = _connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(DeactivateSql, parameters, cancellationToken: cancellationToken));

        return affectedRows == 1;
    }

    private static DynamicParameters CreateInsertParameters(Product product)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", product.Id, DbType.Guid);
        parameters.Add("CategoryId", product.CategoryId, DbType.Guid);
        parameters.Add("Sku", product.Sku, DbType.String, size: ProductRules.SkuMaxLength);
        parameters.Add("Name", product.Name, DbType.String, size: ProductRules.NameMaxLength);
        parameters.Add("Description", product.Description, DbType.String, size: ProductRules.DescriptionMaxLength);
        parameters.Add("StockQuantity", product.StockQuantity, DbType.Int32);
        parameters.Add("IsActive", product.IsActive, DbType.Boolean);
        parameters.Add("CreatedAtUtc", product.CreatedAtUtc, DbType.DateTime2);

        return parameters;
    }

    private static DynamicParameters CreateUpdateParameters(ProductChanges changes)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", changes.ProductId, DbType.Guid);
        parameters.Add("CategoryId", changes.CategoryId, DbType.Guid);
        parameters.Add("Name", changes.Name, DbType.String, size: ProductRules.NameMaxLength);
        parameters.Add("Description", changes.Description, DbType.String, size: ProductRules.DescriptionMaxLength);
        parameters.Add("UpdatedAtUtc", changes.UpdatedAtUtc, DbType.DateTime2);

        return parameters;
    }

    private static bool IsUniqueViolation(SqlException exception)
    {
        return exception.Number == UniqueIndexViolation || exception.Number == UniqueConstraintViolation;
    }
}
