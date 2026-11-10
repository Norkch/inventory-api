using System.Data;
using Dapper;
using Inventory.Application.Categories;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;
using Microsoft.Data.SqlClient;

namespace Inventory.Infrastructure.Persistence.Write;

public sealed class CategoryWriteRepository : ICategoryWriteRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private const string ExistsActiveWithSameNameSql = @"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Categories WHERE Name = @Name AND IsActive = 1
) THEN 1 ELSE 0 END AS BIT);";

    private const string InsertSql = @"
INSERT INTO dbo.Categories (Id, Name, Description, IsActive, CreatedAtUtc)
VALUES (@Id, @Name, @Description, @IsActive, @CreatedAtUtc);";

    private const string DeactivateSql = @"
UPDATE dbo.Categories SET IsActive = 0
 WHERE Id = @Id AND IsActive = 1
   AND NOT EXISTS (SELECT 1 FROM dbo.Products WHERE CategoryId = @Id AND IsActive = 1);";

    private const string ExistsActiveByIdSql = @"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Categories WHERE Id = @Id AND IsActive = 1
) THEN 1 ELSE 0 END AS BIT);";

    private readonly SqlConnectionFactory _connectionFactory;

    public CategoryWriteRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ExistsActiveWithSameNameAsync(Category category, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Name", category.Name, DbType.String, size: Category.NameMaxLength);
        await using var connection = _connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(ExistsActiveWithSameNameSql, parameters, cancellationToken: cancellationToken));
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(InsertSql, CreateInsertParameters(category), cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (IsUniqueViolation(exception))
        {
            throw new ConflictException(CategoryMessages.NameAlreadyExists(category));
        }
    }

    public async Task<CategoryDeactivationResult> DeactivateAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var parameters = CreateIdParameters(categoryId);
        await using var connection = _connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(DeactivateSql, parameters, cancellationToken: cancellationToken));
        if (affectedRows == 1)
        {
            return CategoryDeactivationResult.Deactivated;
        }
        var isStillActive = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(ExistsActiveByIdSql, parameters, cancellationToken: cancellationToken));

        return isStillActive ? CategoryDeactivationResult.HasActiveProducts : CategoryDeactivationResult.NotFound;
    }

    private static DynamicParameters CreateInsertParameters(Category category)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", category.Id, DbType.Guid);
        parameters.Add("Name", category.Name, DbType.String, size: Category.NameMaxLength);
        parameters.Add("Description", category.Description, DbType.String, size: Category.DescriptionMaxLength);
        parameters.Add("IsActive", category.IsActive, DbType.Boolean);
        parameters.Add("CreatedAtUtc", category.CreatedAtUtc, DbType.DateTime2);

        return parameters;
    }

    private static DynamicParameters CreateIdParameters(Guid categoryId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", categoryId, DbType.Guid);

        return parameters;
    }

    private static bool IsUniqueViolation(SqlException exception)
    {
        return exception.Number == UniqueIndexViolation || exception.Number == UniqueConstraintViolation;
    }
}
