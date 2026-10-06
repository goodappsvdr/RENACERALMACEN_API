using API.SERVICE.Services.Cache;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace API.TESTS.Cache;

public class RedisCacheServiceTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task GetOrSetAsync_Miss_CallsFactoryOnceAndCachesValue()
    {
        var sut = CreateSut(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var key = Key("a");
        var calls = 0;

        var first = await sut.GetOrSetAsync(key, Ttl, _ => { calls++; return Task.FromResult(new List<string> { "x" }); });
        var second = await sut.GetOrSetAsync(key, Ttl, _ => { calls++; return Task.FromResult(new List<string> { "y" }); });

        calls.Should().Be(1);
        first.Should().Equal("x");
        second.Should().Equal("x");
    }

    [Fact]
    public async Task GetOrSetAsync_ConcurrentMisses_CallsFactoryOnce()
    {
        var sut = CreateSut(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
        var key = Key("a");
        var calls = 0;

        async Task<List<int>> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(50);
            return [1, 2, 3];
        }

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => sut.GetOrSetAsync(key, Ttl, Factory)));

        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrSetAsync_CacheDown_FallsBackToFactoryWithoutThrowing()
    {
        var broken = new Mock<IDistributedCache>();
        broken.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("redis caído"));
        broken.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("redis caído"));
        var sut = CreateSut(broken.Object);

        var value = await sut.GetOrSetAsync(Key("b"), Ttl, _ => Task.FromResult(new List<string> { "desde-db" }));

        value.Should().Equal("desde-db");
    }

    [Fact]
    public async Task SetAsync_WithoutTtl_Throws()
    {
        var sut = CreateSut(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

        var act = () => sut.SetAsync(Key("c"), "valor", TimeSpan.Zero);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CacheKeys_For_FollowsStandardFormat()
    {
        CacheKeys.For("Items", "Marca", "lookup").Should().Be("goodapps:elrenacer:items:marca:lookup:v1");
    }

    private static RedisCacheService CreateSut(IDistributedCache cache) => new(cache, NullLogger<RedisCacheService>.Instance);

    // Keys únicas por test: el lock por key es estático.
    private static string Key(string name) => CacheKeys.For("tests", Guid.NewGuid().ToString("N"), name);
}
