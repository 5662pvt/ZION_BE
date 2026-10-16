// Lab 01 -- RabbitMQ goes down while an order is in flight.
// API runs with consumers ON (BackgroundProcessing__Enabled=true) so OutboxPublisherService and the
// RabbitMQ consumer both run in this one process. See docs/labs/01-rabbitmq-down.md.
import {
  Evidence, sleep, dockerOrThrow, docker,
  startApi, adminLogin, login, createPublishedProduct, adjustStock, getOrCreateAddress,
  clearCart, addToCart, placeOrder, waitForOrderStatus, adminHealth, findOutboxMessageForOrder,
  waitForInventory, countStockReservations,
} from './lib.mjs';

async function main() {
  const ev = new Evidence('lab01-rabbitmq-down');
  let api;
  let rabbitStopped = false;

  try {
    ev.step('starting API with consumers on (BackgroundProcessing__Enabled=true)');
    api = await startApi({ BackgroundProcessing__Enabled: 'true', RateLimiting__CheckoutPermitLimit: '1000' });
    ev.environment = api.env;

    ev.step('logging in as admin + customer, seeding a published product with stock');
    const admin = await adminLogin();
    const customer = await login('customer@zionshop.local', 'Customer@123');
    const { productId, sku } = await createPublishedProduct(admin.accessToken);
    await adjustStock(admin.accessToken, productId, 20);
    const addressId = await getOrCreateAddress(customer.accessToken);
    await clearCart(customer.accessToken);

    ev.step('baseline: placing an order while RabbitMQ is healthy');
    await addToCart(customer.accessToken, productId, 1);
    const baselineKey = `lab01-baseline-${Date.now()}`;
    const baseline = await placeOrder(customer.accessToken, { addressId, paymentMethod: 'COD', idempotencyKey: baselineKey });
    ev.assert('baseline order accepted (2xx)', baseline.status >= 200 && baseline.status < 300, '2xx', baseline.status);
    const baselineOrderId = baseline.json.data.id;
    const baselineFinal = await waitForOrderStatus(customer.accessToken, baselineOrderId, ['Confirmed', 'Cancelled'], { timeoutMs: 20000 });
    ev.observe('baselineFinalStatus', baselineFinal);
    ev.assert('baseline order confirmed end-to-end while broker healthy', baselineFinal === 'Confirmed', 'Confirmed', baselineFinal);

    ev.step('stopping zionshop-rabbitmq');
    dockerOrThrow('stop', 'zionshop-rabbitmq');
    rabbitStopped = true;
    await sleep(3000);

    ev.step('checking admin health reports RabbitMQ unhealthy');
    const healthDuringOutage = await adminHealth(admin.accessToken);
    const rabbitCheck = healthDuringOutage.checks.find((c) => c.name === 'rabbitmq');
    ev.observe('healthDuringOutage', healthDuringOutage);
    ev.assert('admin health shows rabbitmq unhealthy while broker is down', rabbitCheck?.status === 'Unhealthy', 'Unhealthy', rabbitCheck?.status);

    ev.step('placing an order while the broker is down');
    await addToCart(customer.accessToken, productId, 1);
    const outageKey = `lab01-outage-${Date.now()}`;
    const outageResponse = await placeOrder(customer.accessToken, { addressId, paymentMethod: 'COD', idempotencyKey: outageKey });
    ev.observe('outageOrderResponseStatus', outageResponse.status);
    ev.observe('outageOrderInitialStatus', outageResponse.json?.data?.status);
    ev.assert('order placement still accepted (2xx) while broker is down', outageResponse.status >= 200 && outageResponse.status < 300, '2xx', outageResponse.status);
    const outageOrderId = outageResponse.json.data.id;

    ev.step('checking the Orders outbox row for this order stays unprocessed (Pending, 0 attempts)');
    const pendingMessage = await findOutboxMessageForOrder(admin.accessToken, 'Orders', outageOrderId, { status: 'Pending', timeoutMs: 8000 });
    ev.observe('pendingOutboxMessage', pendingMessage);
    ev.assert('outbox row for the outage order exists and is Pending', pendingMessage?.status === 'Pending', 'Pending', pendingMessage?.status);
    ev.assert('outbox row was not penalized with Attempts while the broker is down (OutboxPublisherService breaks the batch on BrokerUnavailableException before incrementing Attempts)', (pendingMessage?.attempts ?? -1) === 0, 0, pendingMessage?.attempts);

    ev.step('starting zionshop-rabbitmq back up');
    dockerOrThrow('start', 'zionshop-rabbitmq');
    rabbitStopped = false;
    const recoverStart = Date.now();

    ev.step('waiting for the outbox to drain and the order to reach a settled state');
    const finalStatus = await waitForOrderStatus(customer.accessToken, outageOrderId, ['Confirmed', 'Cancelled'], { timeoutMs: 60000 });
    const recoverMs = Date.now() - recoverStart;
    ev.observe('timeToRecoverMs', recoverMs);
    ev.assert('order recovered to Confirmed after the broker came back (no message lost)', finalStatus === 'Confirmed', 'Confirmed', finalStatus);

    ev.step('confirming no duplicate reservation (OnHand/Reserved consistent for 2 orders of qty 1 each) -- polling since the Inventory commit lands via a later async event, after the order already shows Confirmed');
    const { value: inventoryAfter, elapsedMs: inventorySettleMs } = await waitForInventory(
      admin.accessToken,
      sku,
      (inv) => inv.onHand === 18 && inv.reserved === 0,
      { timeoutMs: 30000 },
    );
    ev.observe('inventoryAfter', inventoryAfter);
    ev.observe('inventorySettleMs', inventorySettleMs);
    ev.assert(
      'inventory is consistent after recovery (onHand=18, reserved=0 -- 20 seeded minus 2 orders of 1, both committed)',
      inventoryAfter.onHand === 18 && inventoryAfter.reserved === 0,
      'onHand=18, reserved=0',
      `onHand=${inventoryAfter.onHand}, reserved=${inventoryAfter.reserved}`,
    );

    ev.step('checking exactly one StockReservation row exists for the outage order (no double reservation from the outbox retry path)');
    const outageReservationCount = countStockReservations(outageOrderId, productId);
    ev.observe('outageReservationCount', outageReservationCount);
    ev.assert('exactly one StockReservation row for (outageOrderId, productId)', outageReservationCount === 1, 1, outageReservationCount);
  } finally {
    if (rabbitStopped) {
      ev.step('finally: restarting zionshop-rabbitmq');
      docker('start', 'zionshop-rabbitmq');
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
