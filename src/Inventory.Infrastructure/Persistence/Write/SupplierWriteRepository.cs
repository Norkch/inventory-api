using System.Data;
using Dapper;
using Inventory.Application.Suppliers;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;
using Microsoft.Data.SqlClient;

namespace Inventory.Infrastructure.Persistence.Write;

public sealed class SupplierWriteRepository : ISupplierWriteRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private const string ExistsWithSameNitSql = @"
SELECT CAST(CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Suppliers WHERE Nit = @Nit
) THEN 1 ELSE 0 END AS BIT);";

    private const string InsertSql = @"
INSERT INTO dbo.Suppliers (Id, Name, Nit, Email, Phone, IsActive, CreatedAtUtc, UpdatedAtUtc)
VALUES (@Id, @Name, @Nit, @Email, @Phone, @IsActive, @CreatedAtUtc, NULL);";

    private const string UpdateActiveSql = @"
UPDATE dbo.Suppliers
   SET Name = @Name, Email = @Email, Phone = @Phone, UpdatedAtUtc = @UpdatedAtUtc
OUTPUT INSERTED.Id, INSERTED.Name, INSERTED.Nit, INSERTED.Email, INSERTED.Phone,
       INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc
 WHERE Id = @Id AND IsActive = 1;";

    private const string DeactivateSql = @"
UPDATE dbo.Suppliers SET IsActive = 0, UpdatedAtUtc = @UpdatedAtUtc
 WHERE Id = @Id AND IsActive = 1;";

    private readonly SqlConnectionFactory _connectionFactory;

    public SupplierWriteRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> ExistsWithSameNitAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Nit", supplier.Nit, DbType.String, size: SupplierRules.NitMaxLength);
        await using var connection = _connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(ExistsWithSameNitSql, parameters, cancellationToken: cancellationToken));
    }

    public async Task CreateAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(InsertSql, CreateInsertParameters(supplier), cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (IsUniqueViolation(exception))
        {
            throw new ConflictException(SupplierMessages.NitAlreadyExists(supplier));
        }
    }

    public async Task<Supplier?> UpdateAsync(SupplierChanges changes, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        var snapshot = await connection.QuerySingleOrDefaultAsync<SupplierSnapshot>(new CommandDefinition(UpdateActiveSql, CreateUpdateParameters(changes), cancellationToken: cancellationToken));

        return snapshot is null ? null : Supplier.Restore(snapshot);
    }

    public async Task<bool> DeactivateAsync(SupplierDeactivation deactivation, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", deactivation.SupplierId, DbType.Guid);
        parameters.Add("UpdatedAtUtc", deactivation.DeactivatedAtUtc, DbType.DateTime2);
        await using var connection = _connectionFactory.CreateConnection();
        var affectedRows = await connection.ExecuteAsync(new CommandDefinition(DeactivateSql, parameters, cancellationToken: cancellationToken));

        return affectedRows == 1;
    }

    private static DynamicParameters CreateInsertParameters(Supplier supplier)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", supplier.Id, DbType.Guid);
        parameters.Add("Name", supplier.Name, DbType.String, size: SupplierRules.NameMaxLength);
        parameters.Add("Nit", supplier.Nit, DbType.String, size: SupplierRules.NitMaxLength);
        parameters.Add("Email", supplier.Email, DbType.String, size: SupplierRules.EmailMaxLength);
        parameters.Add("Phone", supplier.Phone, DbType.String, size: SupplierRules.PhoneMaxLength);
        parameters.Add("IsActive", supplier.IsActive, DbType.Boolean);
        parameters.Add("CreatedAtUtc", supplier.CreatedAtUtc, DbType.DateTime2);

        return parameters;
    }

    private static DynamicParameters CreateUpdateParameters(SupplierChanges changes)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Id", changes.SupplierId, DbType.Guid);
        parameters.Add("Name", changes.Name, DbType.String, size: SupplierRules.NameMaxLength);
        parameters.Add("Email", changes.Email, DbType.String, size: SupplierRules.EmailMaxLength);
        parameters.Add("Phone", changes.Phone, DbType.String, size: SupplierRules.PhoneMaxLength);
        parameters.Add("UpdatedAtUtc", changes.UpdatedAtUtc, DbType.DateTime2);

        return parameters;
    }

    private static bool IsUniqueViolation(SqlException exception)
    {
        return exception.Number == UniqueIndexViolation || exception.Number == UniqueConstraintViolation;
    }
}
