// Lab 02 -- the Worker process crashes right after a handler succeeds but before BasicAck.
// API runs with BackgroundProcessing__Enabled=false (no consumer/outbox publisher there); the Worker
// runs both the outbox publisher and the RabbitMQ consumer, with FaultInjection__CrashBeforeAckEventType
// set so CrashBeforeAckFaultInjector (src/BuildingBlocks/ZIONShop.EventBus/FaultInjection) calls
// Environment.FailFast right after OrderCreatedIntegrationEventHandler (Inventory) reserves stock, and
// before the message is ack'd. See docs/labs/02-consumer-crash-before-ack.md.
import {
  Evidence, sleep, readLogFile,
  startApi, startWorker, adminLogin, login, createPublishedProduct, adjustStock, getOrCreateAddress,
  clearCart, addToCart, placeOrder, waitForOrderStatus, rabbitQueue, countInboxMessages,
  waitForInventory, countStockReservations,
} from './lib.mjs';

const CRASH_EVENT_TYPE = 'OrderCreatedIntegrationEvent';
const QUEUE_NAME = 'zionshop.api';
const INVENTORY_CONSUMER = 'OrderCreatedIntegrationEventHandler';

async function main() {
  const ev = new Evidence('lab02-consumer-crash-before-ack');
  let api;
  let worker;
  let worker2;

  try {
    ev.step('starting API (BackgroundProcessing__Enabled=false -- no consumer/outbox publisher on this host)');
    api = await startApi({ BackgroundProcessing__Enabled: 'false' });

    ev.step(`starting Worker with FaultInjection__CrashBeforeAckEventType=${CRASH_EVENT_TYPE}`);
    worker = await startWorker({ FaultInjection__CrashBeforeAckEventType: CRASH_EVENT_TYPE });
    ev.environment = { api: api.env, worker: worker.env };
    ev.observe('apiLogPath', api.logPath);
    ev.observe('workerLogPath', worker.logPath);

    ev.step('logging in as admin + customer, seeding a published product with stock');
    const admin = await adminLogin();
    const customer = await login('customer@zionshop.local', 'Customer@123');
    const { productId, sku } = await createPublishedProduct(admin.accessToken);
    const seeded = await adjustStock(admin.accessToken, productId, 10);
    const addressId = await getOrCreateAddress(customer.accessToken);
    await clearCart(customer.accessToken);

    ev.step('placing an order so Inventory reserves stock via OrderCreatedIntegrationEvent');
    await addToCart(customer.accessToken, productId, 1);
    const key = `lab02-${Date.now()}`;
    const placed = await placeOrder(customer.accessToken, { addressId, paymentMethod: 'COD', idempotencyKey: key });
    ev.assert('order placement accepted (2xx)', placed.status >= 200 && placed.status < 300, '2xx', placed.status);
    const orderId = placed.json.data.id;
    ev.observe('orderId', orderId);

    ev.step('waiting for the Worker to crash (Environment.FailFast after the handler succeeds, before ack)');
    let crashAt = null;
    const exit = await Promise.race([
      worker.exited.then((r) => { crashAt = new Date(); return r; }),
      sleep(30000).then(() => null),
    ]);
    ev.observe('workerExit', exit);
    ev.observe('crashAt', crashAt ? crashAt.toISOString() : null);
    // worker.logPath is this run's first Worker instance only -- lib.mjs gives each startWorker()
    // call its own unique log file, so this can't pick up a crash line from a previous run.
    const log = readLogFile(worker.logPath);
    const crashLines = [...log.matchAll(/FAULT INJECTION: crashing before ACK for (\S+) (\S+)/g)];
    ev.observe('crashLinesInWorkerLog', crashLines.map((m) => m[0]));
    ev.assert('exactly one fault-injection crash line found in this run\'s first Worker log', crashLines.length === 1, 1, crashLines.length);
    const match = crashLines[0];
    ev.assert('worker process logged the fault-injection crash line for the right event type', match?.[1] === CRASH_EVENT_TYPE, CRASH_EVENT_TYPE, match?.[1]);
    ev.assert('worker process actually exited (crashed, not a graceful stop)', exit !== null, 'process exited', exit === null ? 'still running after 30s' : `code=${exit.code} signal=${exit.signal}`);
    const crashedMessageId = match?.[2];

    ev.step('recording queue depth/unacked for the main queue while no consumer is attached (message redelivered)');
    const samples = [];
    for (let i = 0; i < 20; i++) {
      try {
        const q = await rabbitQueue(QUEUE_NAME);
        samples.push({ at: new Date().toISOString(), messages: q.messages, messages_ready: q.messages_ready, messages_unacknowledged: q.messages_unacknowledged });
      } catch (err) {
        samples.push({ at: new Date().toISOString(), error: err.message });
      }
      await sleep(1500);
    }
    ev.observe('queueDepthSamples', samples);
    const everHadMessage = samples.some((s) => (s.messages_ready ?? 0) >= 1 || (s.messages_unacknowledged ?? 0) >= 1);
    ev.assert('the message was never lost -- the main queue showed >=1 ready or unacked message at some point after the crash', everHadMessage, true, everHadMessage);
    const firstReadySample = samples.find((s) => (s.messages_ready ?? 0) >= 1);
    if (firstReadySample && crashAt) {
      const becameReadyAt = new Date(firstReadySample.at);
      ev.observe('messageBecameReadyAgain', { at: firstReadySample.at, delayFromCrashMs: becameReadyAt - crashAt });
    }
    const lastSample = samples[samples.length - 1];
    ev.assert('by the end of the observation window the message is back to ready (unacked == 0, no consumer attached)', (lastSample.messages_unacknowledged ?? -1) === 0, 0, lastSample.messages_unacknowledged);

    ev.step('starting the Worker again WITHOUT the fault-injection flag');
    worker2 = await startWorker({});
    ev.observe('worker2LogPath', worker2.logPath);

    ev.step('waiting for the redelivered message to be handled and the order to settle');
    const finalStatus = await waitForOrderStatus(customer.accessToken, orderId, ['Confirmed', 'Cancelled'], { timeoutMs: 30000 });
    ev.observe('finalOrderStatus', finalStatus);
    ev.assert('order reached Confirmed after redelivery was handled', finalStatus === 'Confirmed', 'Confirmed', finalStatus);

    ev.step('checking the Inbox deduped the redelivery (exactly one row for this message id) via psql -- the admin API (GET /admin/system/inbox) paginates and filters client-side, and can miss the row once the module inbox has thousands of rows');
    if (crashedMessageId) {
      const inboxCheck = countInboxMessages('inventory', crashedMessageId, INVENTORY_CONSUMER);
      ev.observe('inboxMessageCheck', inboxCheck);
      ev.assert('exactly one inbox row exists for the redelivered message id (no double-apply)', inboxCheck.count === 1, 1, inboxCheck.count);
      if (inboxCheck.minProcessedOn && crashAt) {
        const processedAt = new Date(inboxCheck.minProcessedOn.replace(' ', 'T'));
        const processedBeforeCrash = processedAt <= crashAt;
        ev.observe('processedBeforeCrash', processedBeforeCrash);
        // Expected true: the handler's DB transaction (stock reservation + inbox row) committed
        // before the crash -- the crash happens right after that commit, before BasicAck. On
        // redelivery the inbox dedupe check (AnyAsync on (MessageId, Consumer)) finds this row and
        // skips the handler entirely, which is exactly the protection this lab demonstrates.
      }
    } else {
      ev.assert('could extract a message id from the fault-injection log line to check inbox dedupe', false, 'a message id', 'none (log line not matched)');
    }

    ev.step('checking Reserved/OnHand reflect exactly one reservation+commit (no double reservation from redelivery) -- polling since the Inventory commit lands via a later async event (OrderConfirmed), after the order already shows Confirmed, and worker2 must stay up to process it');
    const { value: inventoryAfter, elapsedMs: inventorySettleMs } = await waitForInventory(
      admin.accessToken,
      sku,
      (inv) => inv.onHand === seeded.onHand - 1 && inv.reserved === 0,
      { timeoutMs: 30000 },
    );
    ev.observe('inventoryAfter', inventoryAfter);
    ev.observe('inventorySettleMs', inventorySettleMs);
    ev.assert(
      `inventory reflects exactly one commit of 1 unit (onHand=${seeded.onHand - 1}, reserved=0)`,
      inventoryAfter.onHand === seeded.onHand - 1 && inventoryAfter.reserved === 0,
      `onHand=${seeded.onHand - 1}, reserved=0`,
      `onHand=${inventoryAfter.onHand}, reserved=${inventoryAfter.reserved}`,
    );

    ev.step('checking exactly one StockReservation row exists for the crashed order (the redelivery did not double-reserve)');
    const reservationCount = countStockReservations(orderId, productId);
    ev.observe('reservationCount', reservationCount);
    ev.assert('exactly one StockReservation row for (orderId, productId)', reservationCount === 1, 1, reservationCount);

  } finally {
    if (worker2) {
      ev.step('finally: stopping the second (healthy) Worker');
      await worker2.stop();
    }
    if (worker) {
      ev.step('finally: stopping the crashed Worker process (likely already dead)');
      await worker.stop();
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
