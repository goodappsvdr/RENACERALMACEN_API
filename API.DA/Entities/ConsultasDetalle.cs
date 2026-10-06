using System;
using System.Collections.Generic;

namespace API.DA.Entities;

public partial class ConsultasDetalle
{
    public int IdConsultaDetalle { get; set; }

    public int IdConsulta { get; set; }

    public int IdMotivo { get; set; }
}
