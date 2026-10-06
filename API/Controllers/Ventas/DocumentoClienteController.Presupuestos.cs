using API.SERVICE.Models.Common;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Ventas;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Ventas;

/// <summary>Presupuestos (PV): alta, modificación y anulación. Las líneas quedan pendientes para remitir o facturar.</summary>
public sealed partial class DocumentoClienteController
{
    /// <summary>Letra según el cliente, planilla de caja abierta, punto de venta y número sugerido para un presupuesto.</summary>
    [HttpGet("presupuesto/nuevo")]
    [ProducesResponseType(typeof(NuevaVentaInternaDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NuevaVentaInternaDisplay>> NuevoPresupuesto(
        [FromQuery] int idEntidad, [FromServices] IIniciarPresupuestoUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idEntidad, cancellationToken));

    /// <summary>Alta de presupuesto: cabecera, observación, vencimiento a 30 días y líneas con saldo pendiente (no mueve stock).</summary>
    [HttpPost("presupuesto")]
    [ProducesResponseType(typeof(DocumentoClienteDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentoClienteDisplay>> CreatePresupuesto(
        [FromBody] CreatePresupuestoDto dto, [FromServices] ICreatePresupuestoUseCase useCase, CancellationToken cancellationToken)
    {
        var presupuesto = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = presupuesto.IdDocumentoCliente }, presupuesto);
    }

    /// <summary>Modificación: cabecera y reemplazo de todas las líneas. 409 si no está GENERADO o ya se remitió/facturó algo.</summary>
    [HttpPut("presupuesto/{id:int}")]
    [ProducesResponseType(typeof(DocumentoClienteDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentoClienteDisplay>> UpdatePresupuesto(
        int id, [FromBody] UpdatePresupuestoDto dto, [FromServices] IUpdatePresupuestoUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, dto, cancellationToken));

    /// <summary>Anulación: líneas, saldos y comprobante. 409 si no está GENERADO o ya se remitió/facturó algo.</summary>
    [HttpPost("presupuesto/{id:int}/anular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AnularPresupuesto(int id, [FromServices] IAnularPresupuestoUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }
}
