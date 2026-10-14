using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Infrastructure.Persistence;

namespace ZIONShop.Orders.Infrastructure.Services;

/// <summary>
/// Serializes same (userId, key) PlaceOrder requests with a session-level PostgreSQL advisory lock
/// (<c>pg_try_advisory_lock</c>), held on the <see cref="OrdersDbContext"/>'s own connection -- opened
/// explicitly so it stays open (and the lock held) across every command the rest of the handler issues
/// through that same context, until released.
/// </summary>
public class PostgresIdempotencyLock : IIdempotencyLock
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan AcquireTimeout = TimeSpan.FromSeconds(10);

    private readonly OrdersDbContext _db;

    public PostgresIdempotencyLock(OrdersDbContext db) => _db = db;

    public async Task<IAsyncDisposable?> AcquireAsync(Guid userId, string key, CancellationToken cancellationToken = default)
    {
        var lockKey = ComputeLockKey(userId, key);
        await _db.Database.OpenConnectionAsync(cancellationToken);

        var deadline = DateTime.UtcNow + AcquireTimeout;
        while (true)
        {
            if (await TryAcquireAsync(lockKey, cancellationToken))
                return new Releaser(_db, lockKey);

            if (DateTime.UtcNow >= deadline)
            {
                await _db.Database.CloseConnectionAsync();
                return null;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    private async Task<bool> TryAcquireAsync(long lockKey, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@key)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = lockKey;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool acquired && acquired;
    }

    /// <summary>Deterministic 64-bit key for <c>pg_advisory_lock</c> derived from (userId, key).</summary>
    private static long ComputeLockKey(Guid userId, string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}|{key}"));
        return BitConverter.ToInt64(hash, 0);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly OrdersDbContext _db;
        private readonly long _lockKey;

        public Releaser(OrdersDbContext db, long lockKey)
        {
            _db = db;
            _lockKey = lockKey;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                var connection = _db.Database.GetDbConnection();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(@key)";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "key";
                parameter.Value = _lockKey;
                command.Parameters.Add(parameter);
                await command.ExecuteScalarAsync();
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }
        }
    }
}
