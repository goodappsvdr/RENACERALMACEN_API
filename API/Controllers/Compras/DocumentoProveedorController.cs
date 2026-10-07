using API.SERVICE.Models.Common;
using API.SERVICE.Models.Compras;
using API.SERVICE.UseCases.Compras;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Compras;

/// <summary>Facturas de compra (FC): alta, anulación y remitos / órdenes de compra pendientes de facturar.</summary>
public sealed partial class DocumentoProveedorController
{
    /// <summary>Planilla de caja abierta del usuario y letras posibles según la sucursal y el proveedor.</summary>
    [HttpGet("factura/nueva")]
    [ProducesResponseType(typeof(NuevaFacturaCompraDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NuevaFacturaCompraDisplay>> NuevaFactura(
        [FromQuery] int idProveedor, [FromQuery] int idSucursal, [FromServices] IIniciarFacturaCompraUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idProveedor, idSucursal, cancellationToken));

    /// <summary>Remitos de compra y órdenes de compra del proveedor con líneas pendientes de facturar.</summary>
    [HttpGet("pendientes-facturar")]
    [ProducesResponseType(typeof(IReadOnlyList<ComprobanteCompraPendienteDisplay>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ComprobanteCompraPendienteDisplay>>> PendientesFacturar(
        [FromQuery] int idProveedor, [FromServices] IGetComprobantesCompraParaFacturarUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idProveedor, cancellationToken));

    /// <summary>Líneas del comprobante con saldo pendiente, con su <c>relacion</c> lista para la factura.</summary>
    [HttpGet("{id:int}/lineas-pendientes")]
    [ProducesResponseType(typeof(IReadOnlyList<LineaPendienteCompraDisplay>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LineaPendienteCompraDisplay>>> LineasPendientes(
        int id, [FromServices] IGetLineasPendientesCompraUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, cancellationToken));

    /// <summary>
    /// Alta de factura de compra: detalle, stock, otros tributos, remitos / órdenes facturadas, libro IVA compras (sucursal RI) y
    /// deuda en la cta. cte. del proveedor. 409 si ya está registrada (mismo proveedor, punto de venta y número).
    /// </summary>
    [HttpPost("factura")]
    [ProducesResponseType(typeof(DocumentoProveedorDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentoProveedorDisplay>> CreateFactura(
        [FromBody] CreateFacturaCompraDto dto, [FromServices] ICreateFacturaCompraUseCase useCase, CancellationToken cancellationToken)
    {
        var factura = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = factura.IdDocumentoProveedor }, factura);
    }

    /// <summary>Planilla de caja abierta y letras posibles para una nota de crédito del proveedor.</summary>
    [HttpGet("nota-credito/nueva")]
    [ProducesResponseType(typeof(NuevaFacturaCompraDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NuevaFacturaCompraDisplay>> NuevaNotaCredito(
        [FromQuery] int idProveedor, [FromQuery] int idSucursal, [FromServices] IIniciarNotaCreditoCompraUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(idProveedor, idSucursal, cancellationToken));

    /// <summary>
    /// Alta de nota de crédito de proveedor: resta stock (mercadería devuelta), libro IVA compras como NC (sucursal RI) y saldo a favor
    /// en la cta. cte. del proveedor, que se usa en una orden de pago. 409 si ya está registrada.
    /// </summary>
    [HttpPost("nota-credito")]
    [ProducesResponseType(typeof(DocumentoProveedorDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentoProveedorDisplay>> CreateNotaCredito(
        [FromBody] CreateNotaCreditoCompraDto dto, [FromServices] ICreateNotaCreditoCompraUseCase useCase, CancellationToken cancellationToken)
    {
        var nota = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = nota.IdDocumentoProveedor }, nota);
    }

    /// <summary>Anulación de nota de crédito de proveedor: devuelve el stock. 409 si no está GENERADA / LIQUIDADA o ya se usó en una orden de pago.</summary>
    [HttpPost("nota-credito/{id:int}/anular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AnularNotaCredito(int id, [FromServices] IAnularNotaCreditoCompraUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Anulación de factura de compra. 409 si no está GENERADA / LIQUIDADA o ya tiene pagos imputados.</summary>
    [HttpPost("factura/{id:int}/anular")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AnularFactura(int id, [FromServices] IAnularFacturaCompraUseCase useCase, CancellationToken cancellationToken)
    {
        await useCase.ExecuteAsync(id, cancellationToken);
        return NoContent();
    }
}
