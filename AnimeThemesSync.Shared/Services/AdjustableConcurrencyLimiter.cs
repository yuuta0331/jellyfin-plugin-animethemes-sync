using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AnimeThemesSync.Shared.Services;

public sealed class AdjustableConcurrencyLimiter
{
    private readonly object _syncRoot = new();
    private readonly Queue<Waiter> _waiters = new();
    private int _activeCount;
    private int _limit = 1;

    public Task<IDisposable> AcquireAsync(int limit, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            _limit = Math.Max(1, limit);
            if (_activeCount < _limit && _waiters.Count == 0)
            {
                _activeCount++;
                return Task.FromResult<IDisposable>(new Lease(this));
            }

            var waiter = new Waiter(this, cancellationToken);
            _waiters.Enqueue(waiter);
            waiter.RegisterCancellation();
            return waiter.Task;
        }
    }

    private void Release()
    {
        List<Waiter> ready = new();
        lock (_syncRoot)
        {
            _activeCount = Math.Max(0, _activeCount - 1);
            while (_activeCount < _limit && _waiters.Count > 0)
            {
                var waiter = _waiters.Dequeue();
                if (waiter.IsCancelled)
                {
                    waiter.DisposeRegistration();
                    continue;
                }

                _activeCount++;
                ready.Add(waiter);
            }
        }

        foreach (var waiter in ready)
        {
            waiter.SetReady();
        }
    }

    private void CancelWaiter(Waiter waiter)
    {
        waiter.SetCancelled();
    }

    private sealed class Lease : IDisposable
    {
        private AdjustableConcurrencyLimiter? _owner;

        public Lease(AdjustableConcurrencyLimiter owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release();
        }
    }

    private sealed class Waiter
    {
        private readonly AdjustableConcurrencyLimiter _owner;
        private readonly CancellationToken _cancellationToken;
        private readonly TaskCompletionSource<IDisposable> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenRegistration _registration;
        private int _cancelled;

        public Waiter(AdjustableConcurrencyLimiter owner, CancellationToken cancellationToken)
        {
            _owner = owner;
            _cancellationToken = cancellationToken;
        }

        public Task<IDisposable> Task => _completion.Task;

        public bool IsCancelled => Volatile.Read(ref _cancelled) != 0;

        public void RegisterCancellation()
        {
            if (_cancellationToken.CanBeCanceled)
            {
                _registration = _cancellationToken.Register(() => _owner.CancelWaiter(this));
            }
        }

        public void SetReady()
        {
            DisposeRegistration();
            if (!_completion.TrySetResult(new Lease(_owner)))
            {
                _owner.Release();
            }
        }

        public void SetCancelled()
        {
            if (Interlocked.Exchange(ref _cancelled, 1) == 0)
            {
                _completion.TrySetCanceled(_cancellationToken);
            }
        }

        public void DisposeRegistration()
        {
            _registration.Dispose();
        }
    }
}
