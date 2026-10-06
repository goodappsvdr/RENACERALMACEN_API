namespace API.SERVICE.Interfaces.Afip;

/// <summary>
/// Facturación electrónica (WSFE de AFIP). La implementación actual usa el gateway HTTP de IDEAS SA, igual que el ERP
/// (API_GA_AFIP.vb); detrás de esta interfaz se puede reemplazar por otro proveedor o por un doble en tests.
/// </summary>
public interface IAfipGateway
{
    /// <summary>Último número autorizado para el punto de venta y tipo (FECompUltimoAutorizado).</summary>
    /// <exception cref="AfipNoDisponibleException">AFIP / gateway no respondió o respondió algo inválido.</exception>
    Task<long> GetUltimoAutorizadoAsync(AfipEmisor emisor, int cbteTipo, CancellationToken cancellationToken = default);

    /// <summary>Pide el CAE de un comprobante (FECAESolicitar). AFIP asigna el número.</summary>
    /// <exception cref="AfipNoDisponibleException">
    /// No hubo respuesta válida. Puede que AFIP lo haya emitido igual: antes de reintentar hay que verificar.
    /// </exception>
    Task<AfipRespuestaCae> SolicitarCaeAsync(AfipSolicitudCae solicitud, CancellationToken cancellationToken = default);
}

/// <summary>Quién factura: CUIT y punto de venta AFIP de la sucursal, y su certificado en el gateway.</summary>
public sealed record AfipEmisor(long Cuit, int PuntoVenta, string? CarpetaCertificado, string? ArchivoCertificado);

/// <param name="Monotributo">Sucursal no Responsable Inscripto: comprobante sin discriminar IVA (GenerateVoucherMono).</param>
public sealed record AfipSolicitudCae(
    AfipEmisor Emisor,
    int CbteTipo,
    bool Monotributo,
    DateTime CbteFecha,
    int Concepto,
    long DocNro,
    int DocTipo,
    DateTime FechaVtoPago,
    decimal ImpNeto,
    decimal ImpTotConc,
    decimal ImpOpEx,
    decimal ImpTrib,
    decimal ImpIva,
    decimal ImpTotal,
    DateTime FechaServDesde,
    DateTime FechaServHasta,
    IReadOnlyList<AfipAlicuota> Alicuotas);

/// <param name="Id">Código de alícuota de AFIP: 5 = 21 %, 4 = 10,5 %, 6 = 27 %.</param>
public sealed record AfipAlicuota(int Id, decimal BaseImponible, decimal Importe);

/// <param name="Resultado">"A" aprobado, "R" rechazado (también "P" parcial en lotes).</param>
/// <param name="Numero">Número que asignó AFIP (CbteDesde).</param>
public sealed record AfipRespuestaCae(string Resultado, string? Cae, string? CaeVencimiento, long Numero, string? Observaciones)
{
    public bool Aprobado => Resultado == "A" && !string.IsNullOrWhiteSpace(Cae);
}

/// <summary>El gateway / AFIP no respondió (red, timeout, error 5xx) o respondió algo que no se pudo interpretar.</summary>
public sealed class AfipNoDisponibleException(string message, Exception? inner = null) : Exception(message, inner);
