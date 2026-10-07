using API.SERVICE.Models.Common;
using API.SERVICE.Models.Compras;
using API.SERVICE.Models.Proveedores;
using API.SERVICE.UseCases.Compras;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Proveedores;

/// <summary>Órdenes de pago a proveedores (ProveedoresRecibos — FrmOrdendePago): alta y anulación transaccionales.</summary>
public sealed partial class ProveedorReciboController
{
    /// <summary>Planilla de caja abierta del usuario, punto de venta y número sugerido para una orden de pago.</summary>
    [HttpGet("nueva")]
    [ProducesResponseType(typeof(NuevaOrdenPagoDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NuevaOrdenPagoDisplay>> Nueva([FromServices] IIniciarOrdenPagoUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    /// <summary>Comprobantes pendientes en la cta. cte. del proveedor (facturas, notas de crédito, OP/recibos con saldo, ventas a compensar).</summary>
    [HttpGet("comprobantes-pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<ComprobantePendientePagoDisplay>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComprobantePendientePagoDisplay>>> ComprobantesPendientes(
        [FromQuery] int idProveedor, [FromServices] IGetComprobantesPendientesPagoUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idProveedor, cancellationToken));

    /// <summary>
    /// Alta de orden de pago: imputación a comprobantes, cta. cte., formas de pago (efectivo, cheques de terceros en cartera,
    /// cheques propios, transferencias, tarjetas, retenciones), caja y numeración. 409 si un cheque ya no está disponible.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProveedorReciboDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProveedorReciboDisplay>> Create(
        [FromBody] CreateOrdenPagoDto dto, [FromServices] ICreateOrdenPagoUseCase useCase, CancellationToken cancellationToken)
    {
        var orden = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = orden.IdProveedorRecibo }, orden);
    }

    /// <summary>Anulación: devuelve cheques a cartera, anula caja / bancos / retenciones y devuelve el saldo a los comprobantes. 409 si no está GENERADA.</summary>
    [HttpPost("{id:int}/anular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Anular(int id, [FromServices] IAnularOrdenPagoUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }
}
