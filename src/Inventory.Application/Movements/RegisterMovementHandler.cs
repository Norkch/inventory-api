using Inventory.Application.Abstractions;
using Inventory.Application.Products;
using Inventory.Domain.Entities;
using Inventory.Domain.Exceptions;

namespace Inventory.Application.Movements;

public sealed class RegisterMovementHandler
{
    private readonly IMovementWriteRepository _movementWriteRepository;
    private readonly IClock _clock;

    public RegisterMovementHandler(IMovementWriteRepository movementWriteRepository, IClock clock)
    {
        _movementWriteRepository = movementWriteRepository;
        _clock = clock;
    }

    public async Task<MovementDto> HandleAsync(RegisterMovementCommand command, CancellationToken cancellationToken)
    {
        var movement = InventoryMovement.Create(CreateDraft(command));
        var result = await _movementWriteRepository.RegisterAsync(movement, cancellationToken);

        return MovementDto.From(EnsureRegistered(result, command));
    }

    private MovementDraft CreateDraft(RegisterMovementCommand command)
    {
        return new MovementDraft(command.ProductId, command.Type, command.Quantity, command.Reason, command.SupplierId, command.Registrant.Subject, _clock.UtcNow);
    }

    private static InventoryMovement EnsureRegistered(MovementRegistrationResult result, RegisterMovementCommand command)
    {
        return result.Status switch
        {
            MovementRegistrationStatus.Registered => result.Movement!,
            MovementRegistrationStatus.ProductNotFound => throw new NotFoundException(ProductMessages.ActiveProductNotFound(command.ProductId)),
            MovementRegistrationStatus.SupplierNotAvailable => throw new ValidationException(MovementMessages.SupplierNotAvailable),
            MovementRegistrationStatus.InsufficientStock => throw new InsufficientStockException(MovementMessages.InsufficientStock),
            MovementRegistrationStatus.StockOverflow => throw new ConflictException(MovementMessages.StockOverflow),
            _ => throw new InvalidOperationException($"Unknown movement registration status '{result.Status}'.")
        };
    }
}
