using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class DocumentosClienteRemitosDetalle
{
    public int IdDocumentoClienteRemitoDetalle { get; set; }

    public int? IdDocumentoClienteRemito { get; set; }

    public int? IdItem { get; set; }

    public decimal? CantidadFactura { get; set; }

    public decimal? CantidadRemito { get; set; }

    public decimal? Saldo { get; set; }

    public int? IdDocumentoCliente { get; set; }

    public int? IdRemito { get; set; }
}
