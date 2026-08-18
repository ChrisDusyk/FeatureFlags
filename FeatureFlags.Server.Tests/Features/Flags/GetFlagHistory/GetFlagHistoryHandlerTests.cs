using FeatureFlags.Domain.Environments;
using FeatureFlags.Domain.Flags;
using FeatureFlags.Domain.Flags.Events;
using FeatureFlags.Domain.Shared;
using FeatureFlags.Domain.Users;
using FeatureFlags.Server.Features.Flags.GetFlagHistory;
using FeatureFlags.Server.Tests.Fakes;

namespace FeatureFlags.Server.Tests.Features.Flags.GetFlagHistory;

public class GetFlagHistoryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Ada = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeFlagViewRepository _viewRepository = new();
    private readonly FakeUserRepository _userRepository = new();

    private GetFlagHistoryHandler CreateSut() => new(_viewRepository, _userRepository);

    private FlagKey SeedFlag(string key = "new-checkout")
    {
        var flagKey = FlagKey.Create(key).Value;

        var view = new FlagView(
            Guid.CreateVersion7(),
            flagKey,
            "New checkout",
            string.Empty,
            Now,
            Now,
            [.. EnvironmentKey.All.Select(environment => new FlagStateView(environment, false, Now))]);

        _viewRepository.Seed(view);
        return flagKey;
    }

    [Fact]
    public async Task HandleAsync_WithAnUnknownKey_ShouldReturnNotFound()
    {
        var result = await CreateSut().HandleAsync(
            new GetFlagHistoryQuery(FlagKey.Create("nothing-here").Value),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Flag.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task HandleAsync_WithNoHistory_ShouldReturnAnEmptyList()
    {
        var key = SeedFlag();

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Entries);
    }

    [Fact]
    public async Task HandleAsync_ShouldResolveTheActorsNameFromTheUserMirror()
    {
        var key = SeedFlag();
        _userRepository.Seed(User.FromPersisted(Ada, "ada@example.com", "Ada Lovelace", UserRole.User, Now, Now));
        _viewRepository.SeedHistory(key, new FlagCreatedEvent(Guid.CreateVersion7(), key, "New checkout", "", Now, Ada));

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        Assert.Equal("Ada Lovelace", Assert.Single(result.Value.Entries).CausedByName);
    }

    [Fact]
    public async Task HandleAsync_WhenTheUserHasNoDisplayName_ShouldFallBackToEmail()
    {
        var key = SeedFlag();
        _userRepository.Seed(User.FromPersisted(Ada, "ada@example.com", "", UserRole.User, Now, Now));
        _viewRepository.SeedHistory(key, new FlagCreatedEvent(Guid.CreateVersion7(), key, "New checkout", "", Now, Ada));

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        Assert.Equal("ada@example.com", Assert.Single(result.Value.Entries).CausedByName);
    }

    [Fact]
    public async Task HandleAsync_WhenAnEventHasNoCausedBy_ShouldReportNoName()
    {
        // A backfilled, pre-attribution event — the migration's lossy backfill leaves these null.
        var key = SeedFlag();
        _viewRepository.SeedHistory(key, new FlagCreatedEvent(Guid.CreateVersion7(), key, "New checkout", "", Now, null));

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(result.Value.Entries).CausedByName);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCausedByUserNoLongerExists_ShouldReportNoName()
    {
        var key = SeedFlag();
        _viewRepository.SeedHistory(key, new FlagCreatedEvent(Guid.CreateVersion7(), key, "New checkout", "", Now, Ada));

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(result.Value.Entries).CausedByName);
    }

    [Fact]
    public async Task HandleAsync_ShouldResolveEachDistinctActorOnlyOnce()
    {
        var key = SeedFlag();
        var flagId = Guid.CreateVersion7();
        _userRepository.Seed(User.FromPersisted(Ada, "ada@example.com", "Ada Lovelace", UserRole.User, Now, Now));
        _viewRepository.SeedHistory(
            key,
            new FlagStateChangedEvent(flagId, EnvironmentKey.Staging, true, Now.AddHours(2), Ada),
            new FlagStateChangedEvent(flagId, EnvironmentKey.Development, true, Now.AddHours(1), Ada),
            new FlagCreatedEvent(flagId, key, "New checkout", "", Now, Ada));

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        Assert.All(result.Value.Entries, entry => Assert.Equal("Ada Lovelace", entry.CausedByName));
    }

    [Fact]
    public async Task HandleAsync_ShouldMapAFlagCreatedEventWithItsNameAndDescription()
    {
        var key = SeedFlag();
        var flagId = Guid.CreateVersion7();
        _viewRepository.SeedHistory(key, new FlagCreatedEvent(flagId, key, "New checkout", "Notes.", Now, null));

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        var entry = Assert.Single(result.Value.Entries);
        Assert.Equal("FlagCreated", entry.EventType);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.Equal("New checkout", entry.Name);
        Assert.Equal("Notes.", entry.Description);
        Assert.Null(entry.Environment);
        Assert.Null(entry.IsEnabled);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapAFlagDetailsChangedEventWithItsNameAndDescription()
    {
        var key = SeedFlag();
        var flagId = Guid.CreateVersion7();
        _viewRepository.SeedHistory(key, new FlagDetailsChangedEvent(flagId, "Renamed", "New notes.", Now, null));

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        var entry = Assert.Single(result.Value.Entries);
        Assert.Equal("FlagDetailsChanged", entry.EventType);
        Assert.Equal("Renamed", entry.Name);
        Assert.Equal("New notes.", entry.Description);
        Assert.Null(entry.Environment);
        Assert.Null(entry.IsEnabled);
    }

    [Fact]
    public async Task HandleAsync_ShouldMapAFlagStateChangedEventWithItsEnvironmentAndState()
    {
        var key = SeedFlag();
        var flagId = Guid.CreateVersion7();
        _viewRepository.SeedHistory(key, new FlagStateChangedEvent(flagId, EnvironmentKey.Production, true, Now, null));

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        var entry = Assert.Single(result.Value.Entries);
        Assert.Equal("FlagStateChanged", entry.EventType);
        Assert.Equal("prod", entry.Environment);
        Assert.True(entry.IsEnabled);
        Assert.Null(entry.Name);
        Assert.Null(entry.Description);
    }

    [Fact]
    public async Task HandleAsync_ShouldPreserveTheRepositorysOrdering()
    {
        // The fake, like the real repository, hands back events in whatever order it was seeded
        // with — the handler must not reorder them, since "newest first" is the repository's job.
        var key = SeedFlag();
        var flagId = Guid.CreateVersion7();
        _viewRepository.SeedHistory(
            key,
            new FlagStateChangedEvent(flagId, EnvironmentKey.Production, true, Now.AddHours(2), null),
            new FlagStateChangedEvent(flagId, EnvironmentKey.Staging, true, Now.AddHours(1), null),
            new FlagCreatedEvent(flagId, key, "New checkout", "", Now, null));

        var result = await CreateSut().HandleAsync(new GetFlagHistoryQuery(key), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["FlagStateChanged", "FlagStateChanged", "FlagCreated"],
            result.Value.Entries.Select(entry => entry.EventType));
    }
}
