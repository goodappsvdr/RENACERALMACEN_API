using API.SERVICE.Models.Common;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Ventas;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Ventas;

/// <summary>Comprobantes internos de venta (VEN, letra X, sin AFIP): alta y anulación transaccionales.</summary>
public sealed partial class DocumentoClienteController
{
    /// <summary>Planilla de caja abierta del usuario, punto de venta y número sugerido para un comprobante interno.</summary>
    [HttpGet("interno/nuevo")]
    [ProducesResponseType(typeof(NuevaVentaInternaDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NuevaVentaInternaDisplay>> NuevoInterno([FromServices] IIniciarVentaInternaUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    /// <summary>
    /// Alta de comprobante interno: detalle, stock, presupuestos/remitos facturados, cta. cte. y, si se informan
    /// formas de pago, el recibo del cobro en el momento. 409 si el cliente supera su límite de cta. cte. y no se confirmó.
    /// </summary>
    [HttpPost("interno")]
    [ProducesResponseType(typeof(VentaInternaResultado), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<VentaInternaResultado>> CreateInterno([FromBody] CreateVentaInternaDto dto, [FromServices] ICreateVentaInternaUseCase useCase, CancellationToken cancellationToken)
    {
        var resultado = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = resultado.Comprobante.IdDocumentoCliente }, resultado);
    }

    /// <summary>Anulación de comprobante interno (devuelve stock, anula cta. cte., caja y el recibo del cobro en el momento).</summary>
    [HttpPost("{id:int}/anular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Anular(int id, [FromServices] IAnularVentaInternaUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }
}
