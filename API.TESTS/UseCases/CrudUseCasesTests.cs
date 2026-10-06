using API.DA.Entities;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Interfaces.Items;
using API.SERVICE.Models.Items;
using API.SERVICE.Services.Cache;
using API.SERVICE.UseCases.Items;
using FluentAssertions;
using Moq;

namespace API.TESTS.UseCases;

/// <summary>Comportamiento de las bases CRUD a través de un slice generado concreto (Marca).</summary>
public class CrudUseCasesTests
{
    private readonly Mock<IMarcaRepository> _repository = new();
    private readonly Mock<IRedisCacheService> _cache = new();

    [Fact]
    public async Task Create_ValidDto_PersistsMapsAndInvalidatesLookup()
    {
        _repository.Setup(r => r.CreateAsync(It.IsAny<Marcas>(), It.IsAny<CancellationToken>()))
            .Callback<Marcas, CancellationToken>((m, _) => m.IdMarca = 42);

        var result = await new CreateMarcaUseCase(_repository.Object, _cache.Object)
            .ExecuteAsync(new MarcaDto { Descripcion = "ACME", IdEmpresa = 1, Estado = 1 });

        result.IdMarca.Should().Be(42);
        result.Descripcion.Should().Be("ACME");
        _cache.Verify(c => c.RemoveAsync(MarcaCacheKeys.Lookup, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_ExistingEntity_AppliesDtoAndKeepsKey()
    {
        var entity = new Marcas { IdMarca = 5, Descripcion = "Vieja", Estado = 1 };
        _repository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var result = await new UpdateMarcaUseCase(_repository.Object, _cache.Object)
            .ExecuteAsync(5, new MarcaDto { Descripcion = "Nueva", Estado = 0 });

        result.IdMarca.Should().Be(5);
        entity.Descripcion.Should().Be("Nueva");
        entity.Estado.Should().Be(0);
        _repository.Verify(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveAsync(MarcaCacheKeys.Lookup, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_MissingEntity_ThrowsNotFoundAndDoesNotPersist()
    {
        var act = () => new UpdateMarcaUseCase(_repository.Object, _cache.Object).ExecuteAsync(99, new MarcaDto());

        await act.Should().ThrowAsync<NotFoundException>().WithMessage("Marca 99 no existe.");
        _repository.Verify(r => r.UpdateAsync(It.IsAny<Marcas>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetById_MissingEntity_ThrowsNotFound()
    {
        var act = () => new GetMarcaByIdUseCase(_repository.Object).ExecuteAsync(1);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Lookup_UsesCacheWithStandardKeyAndCatalogTtl()
    {
        _cache.Setup(c => c.GetOrSetAsync(
                MarcaCacheKeys.Lookup,
                CacheTtl.Catalog,
                It.IsAny<Func<CancellationToken, Task<IReadOnlyList<MarcaDisplay>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new MarcaDisplay { IdMarca = 1, Descripcion = "ACME" }]);

        var result = await new GetMarcaLookupUseCase(_repository.Object, _cache.Object).ExecuteAsync();

        result.Should().ContainSingle().Which.Descripcion.Should().Be("ACME");
        MarcaCacheKeys.Lookup.Should().Be("goodapps:elrenacer:items:marca:lookup:v1");
    }
}
