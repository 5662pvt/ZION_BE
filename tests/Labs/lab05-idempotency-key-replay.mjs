// Lab 05 -- Idempotency-Key replay semantics on POST /api/v1/orders.
// Exercises PlaceOrderCommandHandler (src/Modules/Orders/ZIONShop.Orders.Application/Features/PlaceOrder/
// PlaceOrderCommandHandler.cs): same key -> replay (same order id, `Idempotent-Replayed: true` on the
// replay); concurrent same new key -> exactly one order wins the (UserId,Key) unique index, the rest
// replay it; same key + different body -> 409 Orders.IdempotencyKeyReused. See
// docs/labs/05-idempotency-key-replay.md.
import {
  Evidence,
  startApi, adminLogin, login, createPublishedProduct, adjustStock,
  getOrCreateAddress, clearCart, addToCart, placeOrder, getMyOrdersCount,
} from './lib.mjs';

function isReplayed(response) {
  return response.headers.get('idempotent-replayed') === 'true';
}

async function main() {
  const ev = new Evidence('lab05-idempotency-key-replay');
  let api;

  try {
    ev.step('starting API with consumers on and a raised checkout rate limit (10 concurrent requests, same user)');
    api = await startApi({ BackgroundProcessing__Enabled: 'true', RateLimiting__CheckoutPermitLimit: '200' });
    ev.environment = api.env;

    ev.step('logging in as admin + customer, seeding a published product with ample stock');
    const admin = await adminLogin();
    const customer = await login('customer@zionshop.local', 'Customer@123');
    const { productId } = await createPublishedProduct(admin.accessToken);
    await adjustStock(admin.accessToken, productId, 50);
    const addressId = await getOrCreateAddress(customer.accessToken);
    await clearCart(customer.accessToken);

    const runId = Date.now();
    const baselineCount = await getMyOrdersCount(customer.accessToken);
    ev.observe('baselineOrderCount', baselineCount);

    // (a) same key, called sequentially twice -> same order id, second is a replay.
    ev.step('(a) same Idempotency-Key, called sequentially twice');
    await addToCart(customer.accessToken, productId, 1);
    const keyA = `lab05-seq-${runId}`;
    const a1 = await placeOrder(customer.accessToken, { addressId, paymentMethod: 'COD', idempotencyKey: keyA, note: 'seq' });
    const a2 = await placeOrder(customer.accessToken, { addressId, paymentMethod: 'COD', idempotencyKey: keyA, note: 'seq' });
    ev.observe('a1', { status: a1.status, orderId: a1.json?.data?.id, replayed: isReplayed(a1) });
    ev.observe('a2', { status: a2.status, orderId: a2.json?.data?.id, replayed: isReplayed(a2) });
    ev.assert('(a) both calls succeeded (2xx)', a1.status >= 200 && a1.status < 300 && a2.status >= 200 && a2.status < 300, '2xx, 2xx', `${a1.status}, ${a2.status}`);
    ev.assert('(a) both calls returned the same order id', a1.json?.data?.id === a2.json?.data?.id, a1.json?.data?.id, a2.json?.data?.id);
    ev.assert('(a) the first call was not a replay', !isReplayed(a1), false, isReplayed(a1));
    ev.assert('(a) the second call was a replay (Idempotent-Replayed: true)', isReplayed(a2), true, isReplayed(a2));

    const countAfterA = await getMyOrdersCount(customer.accessToken);
    ev.observe('countAfterA', countAfterA);
    ev.assert('(a) exactly one order was created for this key', countAfterA === baselineCount + 1, baselineCount + 1, countAfterA);

    // (b) 10 concurrent requests, same brand-new key -> exactly one order wins.
    ev.step('(b) 10 concurrent requests with the same brand-new Idempotency-Key');
    await addToCart(customer.accessToken, productId, 1);
    const keyB = `lab05-conc-${runId}`;
    const bResponses = await Promise.all(
      Array.from({ length: 10 }, () => placeOrder(customer.accessToken, { addressId, paymentMethod: 'COD', idempotencyKey: keyB, note: 'conc' })),
    );
    const bStatuses = bResponses.map((r) => r.status);
    const bIds = new Set(bResponses.map((r) => r.json?.data?.id));
    const bReplayedCount = bResponses.filter(isReplayed).length;
    ev.observe('bStatuses', bStatuses);
    ev.observe('bDistinctOrderIds', [...bIds]);
    ev.observe('bReplayedCount', bReplayedCount);
    ev.assert('(b) every concurrent request settled 2xx', bStatuses.every((s) => s >= 200 && s < 300), true, bStatuses.every((s) => s >= 200 && s < 300));
    ev.assert('(b) exactly one distinct order id was created across all 10 concurrent requests', bIds.size === 1, 1, bIds.size);
    ev.assert('(b) exactly 9 of the 10 responses were replays of the winner', bReplayedCount === 9, 9, bReplayedCount);

    const countAfterB = await getMyOrdersCount(customer.accessToken);
    ev.observe('countAfterB', countAfterB);
    ev.assert('(b) exactly one order was created for this key', countAfterB === countAfterA + 1, countAfterA + 1, countAfterB);

    // (c) same key, different request body -> 409 Orders.IdempotencyKeyReused.
    ev.step('(c) same Idempotency-Key, different request body (different note)');
    await addToCart(customer.accessToken, productId, 1);
    const keyC = `lab05-diff-${runId}`;
    const c1 = await placeOrder(customer.accessToken, { addressId, paymentMethod: 'COD', idempotencyKey: keyC, note: 'first note' });
    const c2 = await placeOrder(customer.accessToken, { addressId, paymentMethod: 'COD', idempotencyKey: keyC, note: 'a different note' });
    ev.observe('c1', { status: c1.status, orderId: c1.json?.data?.id });
    ev.observe('c2', { status: c2.status, errors: c2.json?.errors });
    ev.assert('(c) the first call succeeded (2xx)', c1.status >= 200 && c1.status < 300, '2xx', c1.status);
    ev.assert('(c) the second call (different body, same key) returned 409 Conflict', c2.status === 409, 409, c2.status);
    ev.assert('(c) the 409 error code is Orders.IdempotencyKeyReused', c2.json?.errors?.[0]?.code === 'Orders.IdempotencyKeyReused', 'Orders.IdempotencyKeyReused', c2.json?.errors?.[0]?.code);

    // (d) total order count increased by exactly the expected amount (1 + 1 + 1 = 3).
    ev.step('(d) checking the total order count increased by exactly the expected amount');
    const countAfterC = await getMyOrdersCount(customer.accessToken);
    ev.observe('countAfterC', countAfterC);
    ev.assert('(d) order count increased by exactly 3 across (a)+(b)+(c) (1 order each, despite 2+10+2=14 HTTP calls)', countAfterC === baselineCount + 3, baselineCount + 3, countAfterC);
  } finally {
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
