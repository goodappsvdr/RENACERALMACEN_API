using API.SERVICE.Models.Common;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Ventas;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Ventas;

/// <summary>Remitos de venta (RV): alta, anulación y comprobantes / líneas pendientes de remitir.</summary>
public sealed partial class DocumentoClienteController
{
    /// <summary>Letra según el cliente, planilla de caja abierta, punto de venta y número sugerido para un remito.</summary>
    [HttpGet("remito/nuevo")]
    [ProducesResponseType(typeof(NuevaVentaInternaDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NuevaVentaInternaDisplay>> NuevoRemito(
        [FromQuery] int idEntidad, [FromServices] IIniciarRemitoUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idEntidad, cancellationToken));

    /// <summary>Comprobantes del cliente (presupuestos, internos y facturas) con mercadería pendiente de remitir.</summary>
    [HttpGet("remito/comprobantes-pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<ComprobanteParaRemitirDisplay>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComprobanteParaRemitirDisplay>>> ComprobantesParaRemitir(
        [FromQuery] int idEntidad, [FromServices] IGetComprobantesParaRemitirUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idEntidad, cancellationToken));

    /// <summary>Líneas del comprobante con saldo pendiente (para cargar en un remito o en una factura, con su <c>relacion</c>).</summary>
    [HttpGet("{id:int}/lineas-pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<LineaPendienteDisplay>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LineaPendienteDisplay>>> LineasPendientes(
        int id, [FromServices] IGetLineasPendientesUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, cancellationToken));

    /// <summary>
    /// Alta de remito: detalle, stock según el origen de cada línea, saldo de los comprobantes entregados y su estado
    /// (ENTREGADO / ENTREGADO PARCIAL). 409 si un comprobante ya no tiene pendiente.
    /// </summary>
    [HttpPost("remito")]
    [ProducesResponseType(typeof(DocumentoClienteDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentoClienteDisplay>> CreateRemito(
        [FromBody] CreateRemitoDto dto, [FromServices] ICreateRemitoUseCase useCase, CancellationToken cancellationToken)
    {
        var remito = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = remito.IdDocumentoCliente }, remito);
    }

    /// <summary>Anulación de remito: devuelve saldo a los comprobantes entregados y el stock descontado. 409 si ya fue facturado.</summary>
    [HttpPost("remito/{id:int}/anular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AnularRemito(int id, [FromServices] IAnularRemitoUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }
}
