using API.SERVICE.Models.Bancos;
using API.SERVICE.Models.Common;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Bancos;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Bancos;

/// <summary>Órdenes de depósito (OD — FrmOrdendeDeposito): alta y anulación transaccionales.</summary>
public sealed partial class OrdenDepositoController
{
    /// <summary>Planilla de caja abierta del usuario, punto de venta y número sugerido para una orden de depósito.</summary>
    [HttpGet("nueva")]
    [ProducesResponseType(typeof(NuevaVentaInternaDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NuevaVentaInternaDisplay>> Nueva([FromServices] IIniciarOrdenBancariaUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync("OD", cancellationToken));

    /// <summary>
    /// Alta de orden de depósito: un movimiento de ingreso en la cuenta por cada elemento (efectivo, cheque de terceros en cartera,
    /// depósito bancario / transferencia, tarjeta). 409 si un cheque ya no está en cartera.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrdenDepositoDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrdenDepositoDisplay>> Create(
        [FromBody] CreateOrdenDepositoDto dto, [FromServices] ICreateOrdenDepositoUseCase useCase, CancellationToken cancellationToken)
    {
        var orden = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = orden.IdOrdenDepostio }, orden);
    }

    /// <summary>Anulación: anula los movimientos bancarios y devuelve los cheques a cartera. 409 si ya está anulada.</summary>
    [HttpPost("{id:int}/anular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Anular(int id, [FromServices] IAnularOrdenBancariaUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.AnularDepositoAsync(id, cancellationToken);
        return NoContent();
    }
}
