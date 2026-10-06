using API.SERVICE.Models.Common;
using API.SERVICE.Models.Ventas;
using API.SERVICE.UseCases.Ventas;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Ventas;

/// <summary>Factura electrónica (FV, letras A/B/C): alta en dos fases (grabar y pedir CAE) y reintento de autorización.</summary>
public sealed partial class DocumentoClienteController
{
    /// <summary>Planilla de caja abierta, punto de venta AFIP y próximo número según AFIP. 503 si AFIP no responde.</summary>
    [HttpGet("electronica/nuevo")]
    [ProducesResponseType(typeof(NuevaFacturaElectronicaDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<NuevaFacturaElectronicaDisplay>> NuevaElectronica(
        [FromQuery] string letra, [FromQuery] int idSucursal, [FromServices] IIniciarFacturaElectronicaUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(letra, idSucursal, cancellationToken));

    /// <summary>
    /// Alta de factura electrónica. Se graba completa sin CAE y después se pide el CAE a AFIP:
    /// 201 autorizada; 202 grabada pero AFIP no respondió (queda pendiente, reintentar con /autorizar);
    /// 422 AFIP la rechazó (se revierte y se anula); 503 AFIP no respondió antes de grabar (no se grabó nada).
    /// </summary>
    [HttpPost("electronica")]
    [ProducesResponseType(typeof(FacturaElectronicaResultado), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(FacturaElectronicaResultado), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FacturaElectronicaResultado>> CreateElectronica(
        [FromBody] CreateFacturaElectronicaDto dto, [FromServices] ICreateFacturaElectronicaUseCase useCase, CancellationToken cancellationToken)
    {
        var resultado = await useCase.ExecuteAsync(dto, cancellationToken);
        return resultado.Estado == EstadoAutorizacionAfip.Autorizada
            ? CreatedAtAction(nameof(GetById), new { id = resultado.Comprobante.IdDocumentoCliente }, resultado)
            : AcceptedAtAction(nameof(GetById), new { id = resultado.Comprobante.IdDocumentoCliente }, resultado);
    }

    /// <summary>
    /// Reintenta pedir el CAE de una factura pendiente. Antes verifica en AFIP que el intento anterior no se haya
    /// emitido igual (si hay dudas devuelve 409 para verificar a mano y no duplicar la factura).
    /// </summary>
    [HttpPost("{id:int}/autorizar")]
    [ProducesResponseType(typeof(AutorizacionAfipResultado), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AutorizacionAfipResultado>> Autorizar(int id, [FromServices] IAutorizarFacturaElectronicaUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, reintento: true, cancellationToken));

    /// <summary>Facturas electrónicas grabadas que todavía no tienen CAE.</summary>
    [HttpGet("electronica/pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentoClienteDisplay>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DocumentoClienteDisplay>>> PendientesAfip([FromServices] IGetFacturasPendientesAfipUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(cancellationToken));
}
