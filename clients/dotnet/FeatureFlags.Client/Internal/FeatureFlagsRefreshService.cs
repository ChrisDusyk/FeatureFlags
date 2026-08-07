using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FeatureFlags.Client.Internal;

/// <summary>
/// Keeps the snapshot warm for a hosted application: one fetch before the application starts
/// serving, then one every <see cref="FeatureFlagsOptions.PollingInterval"/>.
///
/// <para>
/// The client refreshes lazily too, so this is not what makes it correct — it is what stops the
/// first request of the process from paying for the first fetch, and what keeps a flag change
/// arriving in an application that is idle rather than only in one under load.
/// </para>
/// </summary>
internal sealed class FeatureFlagsRefreshService : BackgroundService
{
    private readonly FeatureFlagClient _client;
    private readonly FeatureFlagsOptions _options;
    private readonly ILogger<FeatureFlagsRefreshService> _logger;

    public FeatureFlagsRefreshService(
        IFeatureFlagClient client,
        IOptions<FeatureFlagsOptions> options,
        ILogger<FeatureFlagsRefreshService> logger)
    {
        // The concrete type, because the polling loop needs the non-throwing refresh that is not
        // part of the interface a caller sees.
        _client = (FeatureFlagClient)client;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// The first fetch is awaited before the loop so that <c>ThrowOnStartupFailure</c> can mean
    /// what it says — a BackgroundService that throws from StartAsync stops the host.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _client.RefreshAsync(_options.ThrowOnStartupFailure, cancellationToken).ConfigureAwait(false);

        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.PollingInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutting down. Not a failure, and not something to log about.
                return;
            }

            // Never throws: a polling loop that dies on one bad response would leave the snapshot
            // frozen at whatever it last held, silently, for the life of the process.
            await _client.RefreshAsync(throwOnFailure: false, stoppingToken).ConfigureAwait(false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping feature flag refresh.");

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
