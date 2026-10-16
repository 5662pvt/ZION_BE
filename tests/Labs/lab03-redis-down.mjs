// Lab 03 -- Redis goes down while the product-by-slug cache (GetProductBySlugQueryHandler) is warm.
// RedisCacheService (src/BuildingBlocks/ZIONShop.Caching/RedisCacheService.cs) fails open: once the
// multiplexer reports disconnected, GetOrCreateAsync skips the cache entirely and calls the DB factory
// directly for every caller (no serialized single-flight queueing on a dead cache). See
// docs/labs/03-redis-down.md.
import {
  Evidence, sleep, apiCall, dockerOrThrow, docker,
  startApi, adminLogin, login, createPublishedProduct, adjustStock, getOrCreateAddress,
  clearCart, addToCart, placeOrder, adminHealth,
} from './lib.mjs';

async function getBySlug(slug) {
  return apiCall('GET', `/products/by-slug/${slug}`, {});
}

async function main() {
  const ev = new Evidence('lab03-redis-down');
  let api;
  let redisStopped = false;

  try {
    ev.step('starting API (Caching:Enabled=true, the appsettings.json default)');
    api = await startApi({});
    ev.environment = api.env;

    ev.step('logging in as admin, seeding a published product');
    const admin = await adminLogin();
    const { slug } = await createPublishedProduct(admin.accessToken);

    ev.step('warming the product-by-slug cache (first call = DB miss, second = Redis hit)');
    const warm1 = await getBySlug(slug);
    const warm2 = await getBySlug(slug);
    ev.observe('warmupLatenciesMs', { miss: warm1.durationMs, hit: warm2.durationMs });
    ev.assert('warm-up calls both succeeded (200)', warm1.status === 200 && warm2.status === 200, 200, `${warm1.status}, ${warm2.status}`);

    ev.step('stopping zionshop-redis');
    dockerOrThrow('stop', 'zionshop-redis');
    redisStopped = true;
    await sleep(2000);

    ev.step('first request right after Redis went down');
    const first = await getBySlug(slug);
    ev.observe('firstRequestAfterOutage', { status: first.status, durationMs: first.durationMs });
    ev.assert('first request after outage still returns 200', first.status === 200, 200, first.status);
    ev.assert('first request after outage completes in < 1000ms (fails open, no long timeout)', first.durationMs < 1000, '<1000ms', `${first.durationMs}ms`);

    ev.step('firing 20 more requests while Redis stays down');
    const subsequent = [];
    for (let i = 0; i < 20; i++) {
      subsequent.push(await getBySlug(slug));
    }
    const allOk = subsequent.every((r) => r.status === 200);
    const allFast = subsequent.every((r) => r.durationMs < 200);
    ev.observe('subsequentLatenciesMs', subsequent.map((r) => r.durationMs));
    ev.assert('all 20 subsequent requests returned 200', allOk, true, allOk);
    ev.assert('all 20 subsequent requests completed in < 200ms each', allFast, true, allFast);

    ev.step('checking admin health reports Redis degraded (not unhealthy -- Redis is optional, app fails open)');
    const health = await adminHealth(admin.accessToken);
    const redisCheck = health.checks.find((c) => c.name === 'redis');
    ev.observe('healthDuringOutage', health);
    ev.assert('admin health shows redis Degraded while Redis is down', redisCheck?.status === 'Degraded', 'Degraded', redisCheck?.status);

    ev.step('checking idempotency-key placement still works (PostgresIdempotencyLock, not Redis-backed) and rate limiting still works (in-memory FixedWindowRateLimiter, not Redis-backed)');
    const customer = await login('customer@zionshop.local', 'Customer@123');
    const { productId } = await createPublishedProduct(admin.accessToken);
    await adjustStock(admin.accessToken, productId, 5);
    const addressId = await getOrCreateAddress(customer.accessToken);
    await clearCart(customer.accessToken);
    await addToCart(customer.accessToken, productId, 1);
    const key = `lab03-${Date.now()}`;
    const placed = await placeOrder(customer.accessToken, { addressId, paymentMethod: 'COD', idempotencyKey: key });
    ev.observe('orderPlacedWhileRedisDown', { status: placed.status, orderId: placed.json?.data?.id });
    ev.assert(
      'placing an order with an Idempotency-Key still succeeds while Redis is down (idempotency lock is PostgresIdempotencyLock, independent of Redis)',
      placed.status >= 200 && placed.status < 300,
      '2xx',
      placed.status,
    );

    ev.step('starting zionshop-redis back up');
    dockerOrThrow('start', 'zionshop-redis');
    redisStopped = false;
    await sleep(3000);

    ev.step('confirming the cache works again after Redis reconnects');
    const after = await getBySlug(slug);
    ev.observe('afterReconnect', { status: after.status, durationMs: after.durationMs });
    ev.assert('product-by-slug works again after Redis reconnects', after.status === 200, 200, after.status);
  } finally {
    if (redisStopped) {
      ev.step('finally: restarting zionshop-redis');
      docker('start', 'zionshop-redis');
    }
    if (api) {
      ev.step('finally: stopping API');
      await api.stop();
    }
    const verdict = await ev.write();
    process.exitCode = verdict === 'PASS' ? 0 : 1;
  }
}

main().catch((err) => {
  console.error(err);
  process.exitCode = 1;
});
