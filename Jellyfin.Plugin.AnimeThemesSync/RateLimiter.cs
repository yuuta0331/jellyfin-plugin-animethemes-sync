using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AnimeThemesSync;

/// <summary>
/// A reactive rate limiter that tracks response headers.
/// </summary>
public class RateLimiter : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _logger;
    private readonly string _serviceName;

    private int _remaining;
    private DateTimeOffset _resetTime;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimiter"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="serviceName">Name of the service (AniList/AnimeThemes) for logging.</param>
    /// <param name="initialLimit">Assumed initial limit before first response.</param>
    public RateLimiter(ILogger logger, string serviceName, int initialLimit)
    {
        _logger = logger;
        _serviceName = serviceName;
        _remaining = initialLimit;
        _resetTime = DateTimeOffset.MinValue;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _lock.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Waits if the rate limit is exhausted.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task WaitIfNeededAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;

            if (now > _resetTime)
            {
                // Reset period has passed; safe to proceed.
            }
            else if (_remaining <= 1)
            {
                var delay = _resetTime - now + TimeSpan.FromSeconds(1);

                if (delay.TotalMilliseconds > 0)
                {
                    _logger.LogInformation(
                        "[{Service}] Rate limit exhausted. Waiting {Delay} seconds until {ResetTime}",
                        _serviceName,
                        delay.TotalSeconds,
                        _resetTime);

                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Updates the rate limit state from response headers.
    /// </summary>
    /// <param name="headers">HTTP Response Headers.</param>
    public void UpdateState(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues(Constants.RateLimitRemainingHeader, out var remainingValues) &&
            headers.TryGetValues(Constants.RateLimitResetHeader, out var resetValues))
        {
            var remainingStr = remainingValues.FirstOrDefault();
            var resetStr = resetValues.FirstOrDefault();

            if (int.TryParse(remainingStr, out var remaining) && long.TryParse(resetStr, out var resetEpoch))
            {
                _lock.Wait();
                try
                {
                    _remaining = remaining;
                    _resetTime = DateTimeOffset.FromUnixTimeSeconds(resetEpoch);

                    if (_remaining < 10)
                    {
                        _logger.LogDebug(
                            "[{Service}] Rate limit low: {Remaining} remaining, resets at {ResetTime}",
                            _serviceName,
                            _remaining,
                            _resetTime);
                    }
                }
                finally
                {
                    _lock.Release();
                }
            }
        }
    }
}
