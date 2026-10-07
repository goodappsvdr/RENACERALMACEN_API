using API.SERVICE.Models.Common;
using API.SERVICE.Models.Items;
using API.SERVICE.UseCases.Items;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Items;

/// <summary>ABM de ítems: alta y modificación transaccionales (ítem + sucursales + impuesto + historial de precios).</summary>
public sealed partial class ItemController
{
    /// <summary>Alta de ítem en las sucursales indicadas.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ItemDisplay), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ItemDisplay>> Create([FromBody] CreateItemDto dto, [FromServices] ICreateItemUseCase useCase, CancellationToken cancellationToken)
    {
        var created = await useCase.ExecuteAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.IdItem }, created);
    }

    /// <summary>Modificación de ítem. Registra el cambio de precio en el historial y, según el parámetro CAMBIASTOCK, el stock.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ItemDisplay), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorCatchResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemDisplay>> Update(int id, [FromBody] UpdateItemDto dto, [FromServices] IUpdateItemUseCase useCase, CancellationToken cancellationToken) =>
        Ok(await useCase.ExecuteAsync(id, dto, cancellationToken));
}
