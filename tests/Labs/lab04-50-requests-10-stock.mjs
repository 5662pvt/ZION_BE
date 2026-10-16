// Lab 04 -- 50 concurrent place-order requests racing for 10 units of stock.
// Relies on the pre-seeded load-test customers (Seed__LoadTestCustomers=N -> load1..loadN@zionshop.local
// / Load@123, src/Modules/Auth/ZIONShop.Auth.Infrastructure/Persistence/AuthDbSeeder.cs) seeded by
// another worker's env change -- this lab does NOT register fresh users. Correctness is enforced by
// InventoryItem.Reserve/IInventoryItemRepository.TryReserveAsync's single atomic conditional UPDATE
// (OnHand - Reserved >= quantity), not by the synchronous pre-check in PlaceOrderCommandHandler (which
// only protects against obviously-too-large single orders and can race under concurrency). See
// docs/labs/04-50-requests-10-stock.md.
import {
  Evidence, sleep,
  startApi, adminLogin, login, createPublishedProduct, adjustStock,
  getOrCreateAddress, clearCart, addToCart, placeOrder, getOrder, waitForInventory,
} from './lib.mjs';

const LOAD_USERS = 50;
const STOCK = 10;

async function main() {
  const ev = new Evidence('lab04-50-requests-10-stock');
  let api;

  try {
    ev.step(`starting API with Seed__LoadTestCustomers=${LOAD_USERS} and raised auth/checkout rate limits`);
    api = await startApi({
      BackgroundProcessing__Enabled: 'true',
      Seed__LoadTestCustomers: String(LOAD_USERS),
      RateLimiting__AuthPermitLimit: '500',
      RateLimiting__CheckoutPermitLimit: '500',
    });
    ev.environment = api.env;

    ev.step('logging in as admin, creating a published product with stock=10');
    const admin = await adminLogin();
    const { productId, sku } = await createPublishedProduct(admin.accessToken);
    await adjustStock(admin.accessToken, productId, STOCK);

    ev.step(`logging in ${LOAD_USERS} load-test customers sequentially and preparing their cart (1 unit) + address`);
    const runId = Date.now();
    const users = [];
    for (let i = 1; i <= LOAD_USERS; i++) {
      const session = await login(`load${i}@zionshop.local`, 'Load@123');
      const addressId = await getOrCreateAddress(session.accessToken);
      await clearCart(session.accessToken);
      await addToCart(session.accessToken, productId, 1);
      users.push({ index: i, token: session.accessToken, addressId });
    }
    ev.observe('preparedUsers', users.length);

    ev.step(`firing ${LOAD_USERS} place-order requests concurrently (shared start barrier via Promise.all)`);
    const t0 = Date.now();
    const responses = await Promise.all(users.map((u) =>
      placeOrder(u.token, { addressId: u.addressId, paymentMethod: 'COD', idempotencyKey: `lab04-${runId}-${u.index}` }),
    ));
    const wallMs = Date.now() - t0;

    const statusHistogram = {};
    for (const r of responses) statusHistogram[r.status] = (statusHistogram[r.status] ?? 0) + 1;
    ev.observe('statusHistogram', statusHistogram);
    ev.observe('latenciesMs', responses.map((r) => r.durationMs));
    ev.observe('wallClockMs', wallMs);

    const no5xx = responses.every((r) => r.status < 500);
    ev.assert('no 5xx responses under the concurrent burst', no5xx, true, no5xx);

    const accepted = responses.filter((r) => r.status >= 200 && r.status < 300);
    const syncRejected = responses.filter((r) => r.status === 409);
    ev.observe('acceptedCount', accepted.length);
    ev.observe('syncRejectedCount', syncRejected.length);
    ev.assert('every request settled either 2xx (order created) or 409 (sync stock pre-check rejected)', accepted.length + syncRejected.length === LOAD_USERS, LOAD_USERS, accepted.length + syncRejected.length);

    ev.step('waiting for every accepted order to settle (async Inventory reservation via the outbox/RabbitMQ)');
    // GET /orders/{id} is owner/staff-only, so each order must be polled with its own placing user's
    // token -- recover that pairing from the original (user, response) index alignment.
    const acceptedWithUser = responses
      .map((r, i) => ({ response: r, user: users[i] }))
      .filter(({ response }) => response.status >= 200 && response.status < 300);
    const finalStatuses = [];
    for (const { response, user } of acceptedWithUser) {
      const orderId = response.json.data.id;
      let status = response.json.data.status;
      const deadline = Date.now() + 45000;
      while (Date.now() < deadline && status !== 'Confirmed' && status !== 'Cancelled') {
        await sleep(500);
        const g = await getOrder(user.token, orderId);
        status = g.json?.data?.status ?? status;
      }
      finalStatuses.push(status);
    }
    const confirmedCount = finalStatuses.filter((s) => s === 'Confirmed').length;
    const cancelledCount = finalStatuses.filter((s) => s === 'Cancelled').length;
    const stillPending = finalStatuses.length - confirmedCount - cancelledCount;
    ev.observe('finalStatusCounts', { confirmed: confirmedCount, cancelled: cancelledCount, stillPending });

    ev.assert('every accepted order reached a terminal state within 45s', stillPending === 0, 0, stillPending);
    ev.assert(`exactly ${STOCK} orders ended up Confirmed (reserved the available stock)`, confirmedCount === STOCK, STOCK, confirmedCount);
    ev.assert('the rest of the accepted orders ended up Cancelled (out of stock)', cancelledCount === accepted.length - STOCK, accepted.length - STOCK, cancelledCount);

    ev.step('checking final inventory is consistent (never oversold) -- polling since the Inventory commit lands via a later async event, after each order already shows Confirmed');
    const { value: inventoryAfter, elapsedMs: inventorySettleMs } = await waitForInventory(
      admin.accessToken,
      sku,
      (inv) => inv.onHand === 0 && inv.reserved === 0,
      { timeoutMs: 30000 },
    );
    ev.observe('inventoryAfter', inventoryAfter);
    ev.observe('inventorySettleMs', inventorySettleMs);
    ev.assert(`onHand reached 0 (${STOCK} seeded, ${STOCK} committed) and reserved returned to 0`, inventoryAfter.onHand === 0 && inventoryAfter.reserved === 0, 'onHand=0, reserved=0', `onHand=${inventoryAfter.onHand}, reserved=${inventoryAfter.reserved}`);
    ev.assert('stock was never oversold (onHand never negative)', inventoryAfter.onHand >= 0, '>=0', inventoryAfter.onHand);
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
