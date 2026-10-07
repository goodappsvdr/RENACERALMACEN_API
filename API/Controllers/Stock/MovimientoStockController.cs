using API.SERVICE.Models.Common;
using API.SERVICE.Models.Stock;
using API.SERVICE.UseCases.Stock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Stock;

/// <summary>
/// Transferencias de mercadería entre sucursales (comprobante MS — FrmMovimientoStockABM / FrmMovimientoStockRecibirABM):
/// envío (sale del origen y queda en tránsito), recepción o rechazo en el destino, y anulación desde el origen.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
[Tags("Stock")]
[Produces("application/json")]
[ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status401Unauthorized)]
public sealed class MovimientoStockController : ControllerBase
{
    /// <summary>Punto de venta de la sucursal de origen (por defecto la del usuario) y número sugerido.</summary>
    [HttpGet("nuevo")]
    [ProducesResponseType(typeof(NuevoMovimientoStockDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NuevoMovimientoStockDisplay>> Nuevo(
        [FromQuery] int? idSucursalOrigen, [FromServices] IIniciarMovimientoStockUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idSucursalOrigen, cancellationToken));

    /// <summary>Movimientos en tránsito hacia la sucursal (por defecto la del usuario), pendientes de recibir.</summary>
    [HttpGet("en-transito")]
    [ProducesResponseType(typeof(IReadOnlyList<MovimientoStockDisplay>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<MovimientoStockDisplay>>> EnTransito(
        [FromQuery] int? idSucursalDestino, [FromServices] IGetMovimientosStockEnTransitoUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idSucursalDestino, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MovimientoStockDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoStockDisplay>> GetById(int id, [FromServices] IGetMovimientoStockUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, cancellationToken));

    /// <summary>Envío: descuenta del stock de la sucursal de origen (400 si no alcanza) y queda en tránsito.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MovimientoStockDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MovimientoStockDisplay>> Create(
        [FromBody] CreateMovimientoStockDto dto, [FromServices] ICreateMovimientoStockUseCase useCase, CancellationToken cancellationToken)
    {
        var movimiento = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = movimiento.IdMovimientoStock }, movimiento);
    }

    /// <summary>Recepción en el destino: suma a su stock. 409 si ya no está en tránsito.</summary>
    [HttpPost("{id:int}/recibir")]
    [ProducesResponseType(typeof(MovimientoStockDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoStockDisplay>> Recibir(int id, [FromServices] IResolverMovimientoStockUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.RecibirAsync(id, cancellationToken));

    /// <summary>Rechazo en el destino: la mercadería vuelve al stock del origen. 409 si ya no está en tránsito.</summary>
    [HttpPost("{id:int}/rechazar")]
    [ProducesResponseType(typeof(MovimientoStockDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoStockDisplay>> Rechazar(int id, [FromServices] IResolverMovimientoStockUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.RechazarAsync(id, cancellationToken));

    /// <summary>Anulación desde el origen antes de que lo reciban: vuelve a su stock. 409 si ya no está en tránsito.</summary>
    [HttpPost("{id:int}/anular")]
    [ProducesResponseType(typeof(MovimientoStockDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MovimientoStockDisplay>> Anular(int id, [FromServices] IResolverMovimientoStockUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.AnularAsync(id, cancellationToken));
}
