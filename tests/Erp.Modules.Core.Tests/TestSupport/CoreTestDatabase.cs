using Erp.Modules.Core.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Core.Tests.TestSupport;

/// <summary>
/// An isolated in-memory database per test. Handlers use <see cref="Db"/>; assertions use
/// <see cref="Query"/>, a fresh context, so they see what was saved rather than tracked state.
/// </summary>
internal sealed class CoreTestDatabase : IDisposable
{
    private readonly DbContextOptions<CoreDbContext> _options =
        new DbContextOptionsBuilder<CoreDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private readonly List<CoreDbContext> _contexts = [];

    public CoreTestDatabase()
    {
        Db = Create();
    }

    public CoreDbContext Db { get; }

    public CoreDbContext Query() => Create();

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    private CoreDbContext Create()
    {
        var context = new CoreDbContext(_options);
        _contexts.Add(context);
        return context;
    }
}
