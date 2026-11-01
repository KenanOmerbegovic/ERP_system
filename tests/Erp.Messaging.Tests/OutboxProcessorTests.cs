using Erp.Messaging.Outbox;
using Erp.SharedKernel.Messaging;
using Erp.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Erp.Messaging.Tests;

public sealed class OutboxProcessorTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider _services;
    private readonly Received _received = new();

    public OutboxProcessorTests()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<IClock>(new FixedClock(Now));
        services.AddSingleton(_received);
        services.AddIntegrationEventHandler<SomethingHappened, RecordingHandler>();
        services.AddIntegrationEventHandler<SomethingHappened, SecondRecordingHandler>();
        _services = services.BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task Delivers_event_to_every_handler_and_marks_it_processed()
    {
        var happened = new SomethingHappened(Guid.NewGuid(), Now, "Firma d.o.o.");
        await AddAsync(happened);

        var count = await Processor().ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal([happened, happened], _received.Events);
        Assert.Equal(["first", "second"], _received.Handlers);
        var message = await Query(db => db.Set<OutboxMessage>().SingleAsync());
        Assert.Equal(Now, message.ProcessedAt);
    }

    [Fact]
    public async Task Processed_messages_are_not_delivered_again()
    {
        await AddAsync(new SomethingHappened(Guid.NewGuid(), Now, "x"));
        await Processor().ProcessBatchAsync(CancellationToken.None);

        var count = await Processor().ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Equal(2, _received.Events.Count);
    }

    [Fact]
    public async Task Events_without_handlers_are_marked_processed()
    {
        await AddAsync(new NobodyCares(Guid.NewGuid(), Now));

        await Processor().ProcessBatchAsync(CancellationToken.None);

        Assert.NotNull((await Query(db => db.Set<OutboxMessage>().SingleAsync())).ProcessedAt);
        Assert.Empty(_received.Events);
    }

    [Fact]
    public async Task Failed_delivery_is_retried_until_max_attempts()
    {
        _received.FailWith = new InvalidOperationException("handler broke");
        await AddAsync(new SomethingHappened(Guid.NewGuid(), Now, "x"));

        await Processor(maxAttempts: 2).ProcessBatchAsync(CancellationToken.None);
        var afterFirst = await Query(db => db.Set<OutboxMessage>().SingleAsync());
        await Processor(maxAttempts: 2).ProcessBatchAsync(CancellationToken.None);
        var third = await Processor(maxAttempts: 2).ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(1, afterFirst.Attempts);
        Assert.Contains("handler broke", afterFirst.LastError, StringComparison.Ordinal);
        Assert.Null(afterFirst.ProcessedAt);
        Assert.Equal(0, third);
        Assert.Equal(2, (await Query(db => db.Set<OutboxMessage>().SingleAsync())).Attempts);
    }

    [Fact]
    public async Task Oldest_messages_go_first()
    {
        var later = new SomethingHappened(Guid.NewGuid(), Now.AddMinutes(1), "later");
        var earlier = new SomethingHappened(Guid.NewGuid(), Now, "earlier");
        await AddAsync(later);
        await AddAsync(earlier);

        await Processor().ProcessBatchAsync(CancellationToken.None);

        Assert.Equal(["earlier", "earlier", "later", "later"], _received.Events.Cast<SomethingHappened>().Select(e => e.Name));
    }

    [Fact]
    public void Message_stores_event_as_json_with_its_type_name()
    {
        var message = OutboxMessage.From(new SomethingHappened(Guid.NewGuid(), Now, "Firma"));

        Assert.Equal(typeof(SomethingHappened).FullName, message.Type);
        Assert.Contains("\"name\":\"Firma\"", message.Content, StringComparison.Ordinal);
        Assert.Equal(Now, message.OccurredAt);
    }

    private OutboxProcessor<TestDbContext> Processor(int maxAttempts = 5) => new(
        _services.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new OutboxOptions { PollInterval = TimeSpan.FromSeconds(1), BatchSize = 10, MaxAttempts = maxAttempts }),
        NullLogger<OutboxProcessor<TestDbContext>>.Instance);

    private async Task AddAsync(IIntegrationEvent integrationEvent)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        db.Set<OutboxMessage>().Add(OutboxMessage.From(integrationEvent));
        await db.SaveChangesAsync();
    }

    private async Task<T> Query<T>(Func<TestDbContext, Task<T>> query)
    {
        await using var scope = _services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<TestDbContext>());
    }

    public sealed record SomethingHappened(Guid Id, DateTimeOffset OccurredAt, string Name) : IIntegrationEvent;

    public sealed record NobodyCares(Guid Id, DateTimeOffset OccurredAt) : IIntegrationEvent;

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddOutbox();
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class Received
    {
        public List<IIntegrationEvent> Events { get; } = [];

        public List<string> Handlers { get; } = [];

        public Exception? FailWith { get; set; }
    }

    private sealed class RecordingHandler(Received received) : IIntegrationEventHandler<SomethingHappened>
    {
        public Task HandleAsync(SomethingHappened integrationEvent, CancellationToken cancellationToken)
        {
            if (received.FailWith is not null)
            {
                throw received.FailWith;
            }

            received.Events.Add(integrationEvent);
            received.Handlers.Add("first");
            return Task.CompletedTask;
        }
    }

    private sealed class SecondRecordingHandler(Received received) : IIntegrationEventHandler<SomethingHappened>
    {
        public Task HandleAsync(SomethingHappened integrationEvent, CancellationToken cancellationToken)
        {
            received.Events.Add(integrationEvent);
            received.Handlers.Add("second");
            return Task.CompletedTask;
        }
    }
}
