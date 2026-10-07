using API.SERVICE.Models.Caja;
using API.SERVICE.Models.Common;
using API.SERVICE.UseCases.Caja;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Caja;

/// <summary>Apertura, consulta y cierre de planillas de caja (FrmPlanillasCajaABM).</summary>
public sealed partial class CajaPlanillaController
{
    /// <summary>Planillas de las sucursales que opera el usuario, más nuevas primero.</summary>
    [HttpGet("mias")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanillaCajaListaDisplay>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PlanillaCajaListaDisplay>>> Mias([FromServices] IGetPlanillasCajaUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));

    /// <summary>Planilla con ingresos / egresos del detalle, saldo calculado y si el usuario puede cerrarla.</summary>
    [HttpGet("{id:int}/resumen")]
    [ProducesResponseType(typeof(PlanillaCajaResumenDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanillaCajaResumenDisplay>> Resumen(int id, [FromServices] IGetPlanillaCajaResumenUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, cancellationToken));

    /// <summary>Saldo inicial sugerido (diferencia de la última planilla), puntos de venta habilitados y si ya tiene una abierta.</summary>
    [HttpGet("nueva")]
    [ProducesResponseType(typeof(NuevaPlanillaCajaDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<NuevaPlanillaCajaDisplay>> Nueva([FromQuery] int? idUsuario, [FromServices] IIniciarPlanillaCajaUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idUsuario, cancellationToken));

    /// <summary>Abre la caja del usuario. 409 si ya tiene una abierta.</summary>
    [HttpPost("abrir")]
    [ProducesResponseType(typeof(PlanillaCajaResumenDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlanillaCajaResumenDisplay>> Abrir(
        [FromBody] AbrirPlanillaCajaDto dto, [FromServices] IAbrirPlanillaCajaUseCase useCase, CancellationToken cancellationToken)
    {
        var planilla = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = planilla.IdPlanillaCaja }, planilla);
    }

    /// <summary>
    /// Actualiza saldo inicial y rendido y, con <c>cerrar: true</c>, cierra la planilla. Ingresos, egresos y diferencia los calcula
    /// el servidor. 409 si ya está cerrada; 403 si un no administrador intenta cerrar una planilla de otro día.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(PlanillaCajaResumenDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlanillaCajaResumenDisplay>> Modificar(
        int id, [FromBody] ModificarPlanillaCajaDto dto, [FromServices] IModificarPlanillaCajaUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, dto, cancellationToken));
}
