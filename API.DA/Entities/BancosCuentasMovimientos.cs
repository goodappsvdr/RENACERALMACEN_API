using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class BancosCuentasMovimientos
{
    public int IdBancoCuentaMovimiento { get; set; }

    public int? IdBancoCuenta { get; set; }

    public int? IdMovimientoTipo { get; set; }

    public decimal? Debe { get; set; }

    public decimal? Haber { get; set; }

    public decimal? Importe { get; set; }

    public DateTime? Fecha { get; set; }

    public int? IdBancoOrigen { get; set; }

    public int? IdBancoSucursalOrigen { get; set; }

    public int? CuentaTipoOrigen { get; set; }

    public string? NroCuentaOrigen { get; set; }

    public int? IdBancoDestino { get; set; }

    public int? IdBancoSucursalDestino { get; set; }

    public int? CuentaTipoDestino { get; set; }

    public string? NroCuentaDestino { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public int? IdComprobante { get; set; }

    public int? Estado { get; set; }

    public decimal? Total { get; set; }
}
