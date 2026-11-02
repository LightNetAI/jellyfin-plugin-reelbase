using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Reelbase;

/// <summary>
/// Reads the saved extension list when the server starts, and checks its version once.
/// </summary>
/// <remarks>
/// The saved list is read first, so a scan that is already running has something to judge files
/// against. The version check then runs in the background: a server that cannot reach Reelbase
/// must still start.
/// </remarks>
public class ReelbaseExtensionCatalogStartup : IHostedService
{
    private readonly IReelbaseExtensionCatalog _catalog;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReelbaseExtensionCatalogStartup> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReelbaseExtensionCatalogStartup"/> class.
    /// </summary>
    /// <param name="catalog">Instance of the <see cref="IReelbaseExtensionCatalog"/> interface.</param>
    /// <param name="scopeFactory">Instance of the <see cref="IServiceScopeFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{ReelbaseExtensionCatalogStartup}"/> interface.</param>
    public ReelbaseExtensionCatalogStartup(
        IReelbaseExtensionCatalog catalog,
        IServiceScopeFactory scopeFactory,
        ILogger<ReelbaseExtensionCatalogStartup> logger)
    {
        _catalog = catalog;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _catalog.Load();

        _ = Task.Run(RefreshAsync, CancellationToken.None);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RefreshAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IReelbaseClient>();

            await _catalog.RefreshAsync(client, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A check that fails at startup must not take the server with it.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogDebug(ex, "Could not check the approved extension list at startup.");
        }
    }
}
