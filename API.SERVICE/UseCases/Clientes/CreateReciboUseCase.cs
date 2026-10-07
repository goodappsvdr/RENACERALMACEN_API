using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Mappings.Clientes;
using API.SERVICE.Models.Clientes;

namespace API.SERVICE.UseCases.Clientes;

public interface ICreateReciboUseCase
{
    Task<EntidadReciboDisplay> ExecuteAsync(CreateReciboDto dto, CancellationToken cancellationToken = default);
}

/// <summary>
/// Alta de recibo de cobro (Agregar_Ws de FrmRecibos) en su propia transacción, con lock por cliente.
/// La escritura en sí está en <see cref="IReciboCobroWriter"/>, que también usan las facturas cobradas en el momento.
/// </summary>
public sealed class CreateReciboUseCase : ICreateReciboUseCase
{
    private readonly IReciboCobroWriter _writer;
    private readonly IReciboCobroRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public CreateReciboUseCase(IReciboCobroWriter writer, IReciboCobroRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _writer = writer;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<EntidadReciboDisplay> ExecuteAsync(CreateReciboDto dto, CancellationToken cancellationToken = default)
    {
        var idUsuario = ReciboContexto.RequireIdUsuario(_currentUser);

        var recibo = await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Primero el lock del cliente: un segundo recibo simultáneo espera y, al entrar, ve los comprobantes ya cancelados.
            await _repository.BloquearEntidadAsync(dto.IdEntidad!.Value, ct);
            return await _writer.GrabarAsync(dto, idUsuario, ct);
        }, cancellationToken);

        return recibo.ToDisplay();
    }
}
