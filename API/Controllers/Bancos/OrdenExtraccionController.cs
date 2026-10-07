using API.SERVICE.Models.Bancos;
using API.SERVICE.Models.Common;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Bancos;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Bancos;

/// <summary>Órdenes de extracción (OE — FrmOrdendeExtraccion): alta y anulación transaccionales.</summary>
public sealed partial class OrdenExtraccionController
{
    /// <summary>Planilla de caja abierta del usuario, punto de venta y número sugerido para una orden de extracción.</summary>
    [HttpGet("nueva")]
    [ProducesResponseType(typeof(NuevaVentaInternaDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NuevaVentaInternaDisplay>> Nueva([FromServices] IIniciarOrdenBancariaUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync("OE", cancellationToken));

    /// <summary>Alta de orden de extracción de efectivo: movimiento de egreso en la cuenta.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrdenExtraccionDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdenExtraccionDisplay>> Create(
        [FromBody] CreateOrdenExtraccionDto dto, [FromServices] ICreateOrdenExtraccionUseCase useCase, CancellationToken cancellationToken)
    {
        var orden = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = orden.IdOrdenExtraccion }, orden);
    }

    /// <summary>Anulación: anula el movimiento bancario. 409 si ya está anulada.</summary>
    [HttpPost("{id:int}/anular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Anular(int id, [FromServices] IAnularOrdenBancariaUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.AnularExtraccionAsync(id, cancellationToken);
        return NoContent();
    }
}
