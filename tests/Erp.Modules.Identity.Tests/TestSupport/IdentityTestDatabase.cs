using Erp.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Identity.Tests.TestSupport;

/// <summary>
/// An isolated in-memory database per test. Handlers use <see cref="Db"/>; assertions use
/// <see cref="Query"/>, a fresh context, so they see what was saved rather than tracked state.
/// </summary>
internal sealed class IdentityTestDatabase : IDisposable
{
    private readonly DbContextOptions<IdentityDbContext> _options =
        new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private readonly List<IdentityDbContext> _contexts = [];

    public IdentityTestDatabase()
    {
        Db = Create();
    }

    public IdentityDbContext Db { get; }

    public IdentityDbContext Query() => Create();

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    private IdentityDbContext Create()
    {
        var context = new IdentityDbContext(_options);
        _contexts.Add(context);
        return context;
    }
}
