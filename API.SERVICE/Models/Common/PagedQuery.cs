using System.ComponentModel.DataAnnotations;

namespace API.SERVICE.Models.Common;

/// <summary>Parámetros de paginación comunes a todos los listados (las listas siempre van paginadas).</summary>
public class PagedQuery
{
    public const int MaxPageSize = 200;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 25;
}
