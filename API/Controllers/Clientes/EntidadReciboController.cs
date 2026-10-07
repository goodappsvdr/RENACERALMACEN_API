using API.SERVICE.Models.Clientes;
using API.SERVICE.Models.Common;
using API.SERVICE.UseCases.Clientes;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Clientes;

/// <summary>Recibos de cobro: alta y anulación transaccionales (cta. cte., caja, cheques, bancos, retenciones).</summary>
public sealed partial class EntidadReciboController
{
    /// <summary>Planilla de caja abierta del usuario, punto de venta y número sugerido para un recibo nuevo.</summary>
    [HttpGet("nuevo")]
    [ProducesResponseType(typeof(NuevoReciboDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NuevoReciboDisplay>> Nuevo([FromServices] IIniciarReciboUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    /// <summary>Comprobantes pendientes del cliente, para imputar en un recibo.</summary>
    [HttpGet("comprobantes-pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<ComprobantePendienteDisplay>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComprobantePendienteDisplay>>> ComprobantesPendientes(
        [FromQuery] int idEntidad, [FromServices] IGetComprobantesPendientesUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idEntidad, cancellationToken));

    /// <summary>Alta de recibo: imputa los comprobantes y registra las formas de pago.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EntidadReciboDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EntidadReciboDisplay>> Create([FromBody] CreateReciboDto dto, [FromServices] ICreateReciboUseCase useCase, CancellationToken cancellationToken)
    {
        var created = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.IdEntidadRecibo }, created);
    }

    /// <summary>Clientes con saldo a cobrar, candidatos a la emisión masiva de recibos.</summary>
    [HttpGet("automaticos/entidades")]
    [ProducesResponseType(typeof(IReadOnlyList<EntidadReciboAutomaticoDisplay>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EntidadReciboAutomaticoDisplay>>> EntidadesRecibosAutomaticos(
        [FromServices] IGetEntidadesRecibosAutomaticosUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    /// <summary>
    /// Emisión masiva: un recibo en efectivo por cliente (hasta 10 por vez) que cancela todos sus comprobantes pendientes.
    /// Cada cliente se graba en su propia transacción; la respuesta informa el resultado de cada uno.
    /// </summary>
    [HttpPost("automaticos")]
    [ProducesResponseType(typeof(IReadOnlyList<ReciboAutomaticoResultado>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ReciboAutomaticoResultado>>> GenerarAutomaticos(
        [FromBody] GenerarRecibosAutomaticosDto dto, [FromServices] IGenerarRecibosAutomaticosUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(dto, cancellationToken));

    /// <summary>Anulación: revierte la cta. cte. de los comprobantes imputados y anula caja, cheques, bancos y retenciones.</summary>
    [HttpPost("{id:int}/anular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Anular(int id, [FromServices] IAnularReciboUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }
}
