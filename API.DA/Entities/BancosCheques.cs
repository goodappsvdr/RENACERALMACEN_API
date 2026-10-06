using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class BancosCheques
{
    public int IdBancoCheque { get; set; }

    public int? IdBancoCuenta { get; set; }

    public int? IdBancoChequera { get; set; }

    public string? NroCheque { get; set; }

    public int? IdComprobante { get; set; }

    public int? IdComprobanteTipo { get; set; }

    public DateTime? FechaEmision { get; set; }

    public DateTime? FechaVencimiento { get; set; }

    public DateTime? FechaImpresion { get; set; }

    public decimal? Importe { get; set; }

    public int? Estado { get; set; }
}
