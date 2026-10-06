using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Domain.Ventas;
using API.SERVICE.Interfaces;
using API.SERVICE.Interfaces.Clientes;
using API.SERVICE.Interfaces.Sistema;
using API.SERVICE.Interfaces.Ventas;

namespace API.SERVICE.UseCases.Ventas;

public interface IAnularVentaInternaUseCase
{
    Task ExecuteAsync(int idDocumentoCliente, CancellationToken cancellationToken = default);
}

/// <summary>
/// Anulación de comprobante interno (VEN) en una transacción (Editar_Ws de FrmFacturas).
/// Valida tipo y estado, toma el lock del cliente y delega la reversión en <see cref="IVentaAnulador"/>.
/// </summary>
public sealed class AnularVentaInternaUseCase : IAnularVentaInternaUseCase
{
    private readonly IVentaAnulador _anulador;
    private readonly IVentaRepository _ventas;
    private readonly IReciboCobroRepository _comprobantes;
    private readonly IReferenciasRepository _referencias;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServerClock _clock;
    private readonly ICurrentUser _currentUser;

    public AnularVentaInternaUseCase(
        IVentaAnulador anulador,
        IVentaRepository ventas,
        IReciboCobroRepository comprobantes,
        IReferenciasRepository referencias,
        IUnitOfWork unitOfWork,
        IServerClock clock,
        ICurrentUser currentUser)
    {
        _anulador = anulador;
        _ventas = ventas;
        _comprobantes = comprobantes;
        _referencias = referencias;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _currentUser = currentUser;
    }

    public async Task ExecuteAsync(int idDocumentoCliente, CancellationToken cancellationToken = default)
    {
        var idUsuario = VentaContexto.RequireIdUsuario(_currentUser);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var ven = await _referencias.GetParametroEnteroAsync("COMPROBANTE", "VEN", ct);
            var doc = await _ventas.GetDocumentoAsync(idDocumentoCliente, ct)
                ?? throw new NotFoundException($"Comprobante {idDocumentoCliente} no existe.");

            if (doc.IdComprobanteTipo != ven)
                throw new BusinessException("Solo se pueden anular por acá comprobantes internos (VEN). Las facturas electrónicas se anulan con nota de crédito.");
            if (!VentaRules.EstadosAnulables.Contains(doc.Estado ?? 0))
                throw new ConflictException($"El comprobante {idDocumentoCliente} no se puede anular en su estado actual.");

            await _comprobantes.BloquearEntidadAsync(doc.IdCliente ?? 0, ct);
            await _anulador.AnularAsync(doc, "VEN", idUsuario, await _clock.GetNowAsync(ct), ct);
            return true;
        }, cancellationToken);
    }
}
