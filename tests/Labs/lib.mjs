// Shared helpers for tests/Labs/*.mjs. Zero npm dependencies: global fetch, node:* built-ins only.
// See tests/Labs/README.md for prerequisites and how to run a lab.

import { spawn, spawnSync } from 'node:child_process';
import { setTimeout as sleep } from 'node:timers/promises';
import { fileURLToPath } from 'node:url';
import net from 'node:net';
import path from 'node:path';
import fs from 'node:fs';

// ---------------------------------------------------------------------------
// Paths
// ---------------------------------------------------------------------------

const LABS_DIR = fileURLToPath(new URL('.', import.meta.url));
export const BE_ROOT = path.resolve(LABS_DIR, '..', '..');
export const API_PROJECT_DIR = path.join(BE_ROOT, 'src', 'Api', 'ZIONShop.Api');
export const WORKER_PROJECT_DIR = path.join(BE_ROOT, 'src', 'Worker', 'ZIONShop.Worker');
export const LOGS_DIR = path.join(LABS_DIR, '.logs');
export const EVIDENCE_DIR = path.join(BE_ROOT, 'docs', 'labs', 'evidence');

for (const dir of [LOGS_DIR, EVIDENCE_DIR]) {
  fs.mkdirSync(dir, { recursive: true });
}

export { sleep };

export function nowIso() {
  return new Date().toISOString();
}

/**
 * Parses a psql `-At` timestamptz text value (e.g. "2026-10-16 08:17:43.637768+00" or
 * "...+07:00") into a Date. JS `Date` cannot parse psql's bare `+HH`/`-HH` offset (no colon,
 * no minutes) -- it silently produces an Invalid Date instead of throwing, so without this a
 * downstream comparison just evaluates to false with no error. This normalizes the offset to
 * `+HH:MM`/`-HH:MM`, trims fractional seconds to milliseconds (JS Date only supports 3 digits),
 * and throws if the result is still not parseable.
 */
export function parsePgTimestamp(text) {
  let iso = text.trim().replace(' ', 'T');
  iso = iso.replace(/\.(\d+)/, (_, frac) => `.${frac.slice(0, 3)}`);
  iso = iso.replace(/([+-]\d{2})(?::?(\d{2}))?$/, (_, hh, mm) => `${hh}:${mm ?? '00'}`);
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    throw new Error(`parsePgTimestamp: could not parse "${text}" (normalized to "${iso}")`);
  }
  return date;
}

// ---------------------------------------------------------------------------
// Evidence collector
// ---------------------------------------------------------------------------

export class Evidence {
  constructor(labName) {
    this.labName = labName;
    this.startedAt = nowIso();
    this.environment = {};
    this.steps = [];
    this.observations = {};
    this.assertions = [];
  }

  step(title) {
    const entry = { at: nowIso(), title };
    this.steps.push(entry);
    console.log(`[${entry.at}] ${title}`);
  }

  observe(key, value) {
    this.observations[key] = value;
    console.log(`  observed ${key} = ${JSON.stringify(value)}`);
  }

  /** @param {boolean} pass caller computes the condition; this just records + logs it. */
  assert(name, pass, expected, actual) {
    const entry = { name, expected, actual, pass: !!pass };
    this.assertions.push(entry);
    console.log(`  [${entry.pass ? 'PASS' : 'FAIL'}] ${name} (expected ${JSON.stringify(expected)}, actual ${JSON.stringify(actual)})`);
    return entry.pass;
  }

  async write() {
    this.finishedAt = nowIso();
    this.verdict = this.assertions.length > 0 && this.assertions.every((a) => a.pass) ? 'PASS' : 'FAIL';
    const payload = {
      startedAt: this.startedAt,
      finishedAt: this.finishedAt,
      environment: this.environment,
      steps: this.steps,
      observations: this.observations,
      assertions: this.assertions,
      verdict: this.verdict,
    };
    const file = path.join(EVIDENCE_DIR, `${this.labName}.json`);
    await fs.promises.writeFile(file, JSON.stringify(payload, null, 2), 'utf8');
    console.log(`\nEvidence written to ${file} -- verdict: ${this.verdict}`);
    return this.verdict;
  }
}

export function step(title) {
  console.log(`[${nowIso()}] ${title}`);
}

// ---------------------------------------------------------------------------
// Port / process management
// ---------------------------------------------------------------------------

export async function assertPortFree(port) {
  await new Promise((resolve, reject) => {
    const tester = net.createServer();
    tester.once('error', (err) => {
      if (err.code === 'EADDRINUSE') {
        reject(new Error(
          `Port ${port} is already in use -- stop whatever is listening on it (a leftover dotnet process?) before running this lab.`,
        ));
      } else {
        reject(err);
      }
    });
    tester.once('listening', () => tester.close(() => resolve()));
    tester.listen(port, '127.0.0.1');
  });
}

// One Node process per lab run, so these are stable for the whole run and unique across runs.
const RUN_LAB_NAME = path.basename(process.argv[1] ?? 'lab', '.mjs');
const RUN_TS = nowIso().replace(/[:.]/g, '-');
const spawnSeqByLabel = new Map();

function killTree(pid) {
  if (!pid) return;
  try {
    spawnSync('taskkill', ['/PID', String(pid), '/T', '/F']);
  } catch {
    // process may already be gone
  }
}

async function waitForHealth(url, { timeoutMs = 90000, intervalMs = 1000 } = {}) {
  const deadline = Date.now() + timeoutMs;
  let lastErr = 'no attempt made';
  while (Date.now() < deadline) {
    try {
      const res = await fetch(url);
      if (res.status === 200) return true;
      lastErr = `HTTP ${res.status}`;
    } catch (err) {
      lastErr = err.message;
    }
    await sleep(intervalMs);
  }
  throw new Error(`Timed out after ${timeoutMs}ms waiting for ${url} to return 200 (last: ${lastErr})`);
}

function spawnDotnet(label, projectDir, extraEnv) {
  // Per-run, per-process-instance file name: a lab that restarts the same proc (e.g. lab02's
  // Worker crash + restart) must not have its second instance overwrite the first's log, since
  // later steps (crash-line extraction) need to read back exactly the right instance's output.
  const seq = (spawnSeqByLabel.get(label) ?? 0) + 1;
  spawnSeqByLabel.set(label, seq);
  const logPath = path.join(LOGS_DIR, `${RUN_LAB_NAME}-${label}-${RUN_TS}-${seq}.log`);
  const logStream = fs.createWriteStream(logPath, { flags: 'w' });
  logStream.write(`\n----- starting ${label} at ${nowIso()} (cwd=${projectDir}) -----\n`);
  const env = { ...process.env, ...extraEnv };
  const proc = spawn('dotnet', ['run', '--no-build', '--no-launch-profile'], { cwd: projectDir, env });
  proc.stdout.on('data', (d) => logStream.write(d));
  proc.stderr.on('data', (d) => logStream.write(d));
  const exited = new Promise((resolve) => {
    proc.on('exit', (code, signal) => {
      logStream.write(`\n----- ${label} exited: code=${code} signal=${signal} at ${nowIso()} -----\n`);
      resolve({ code, signal });
    });
  });
  return { proc, logPath, exited };
}

export function readLogFile(logPath) {
  try {
    return fs.readFileSync(logPath, 'utf8');
  } catch {
    return '';
  }
}

function tailLog(logPath, lines = 40) {
  try {
    const content = fs.readFileSync(logPath, 'utf8');
    return content.split('\n').slice(-lines).join('\n');
  } catch {
    return '(log file not readable)';
  }
}

/**
 * Starts the API (dotnet run, cwd=src/Api/ZIONShop.Api) and waits for /health/live=200.
 * extraEnv overrides the lab's defaults: ASPNETCORE_ENVIRONMENT=Development,
 * ASPNETCORE_URLS=http://localhost:8080, EventBus__Provider=RabbitMq, EventBus__Host=localhost,
 * Redis__Configuration=localhost:6379, BackgroundProcessing__Enabled=false.
 */
export async function startApi(extraEnv = {}) {
  await assertPortFree(8080);
  const baseEnv = {
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: 'http://localhost:8080',
    EventBus__Provider: 'RabbitMq',
    EventBus__Host: 'localhost',
    Redis__Configuration: 'localhost:6379',
    BackgroundProcessing__Enabled: 'false',
    ...extraEnv,
  };
  const handle = spawnDotnet('api', API_PROJECT_DIR, baseEnv);
  try {
    await waitForHealth('http://localhost:8080/health/live');
  } catch (err) {
    killTree(handle.proc.pid);
    throw new Error(`API failed to start: ${err.message}\n--- tail of ${handle.logPath} ---\n${tailLog(handle.logPath)}`);
  }
  return {
    ...handle,
    env: baseEnv,
    stop: async () => {
      killTree(handle.proc.pid);
      await Promise.race([handle.exited, sleep(10000)]);
    },
  };
}

/**
 * Starts the Worker (dotnet run, cwd=src/Worker/ZIONShop.Worker) and waits for /health/live=200.
 * The Worker forces BackgroundProcessing:Enabled=true itself regardless of extraEnv.
 */
export async function startWorker(extraEnv = {}) {
  await assertPortFree(8090);
  const baseEnv = {
    ASPNETCORE_ENVIRONMENT: 'Development',
    ASPNETCORE_URLS: 'http://localhost:8090',
    EventBus__Provider: 'RabbitMq',
    EventBus__Host: 'localhost',
    Redis__Configuration: 'localhost:6379',
    ...extraEnv,
  };
  const handle = spawnDotnet('worker', WORKER_PROJECT_DIR, baseEnv);
  try {
    await waitForHealth('http://localhost:8090/health/live');
  } catch (err) {
    killTree(handle.proc.pid);
    throw new Error(`Worker failed to start: ${err.message}\n--- tail of ${handle.logPath} ---\n${tailLog(handle.logPath)}`);
  }
  return {
    ...handle,
    env: baseEnv,
    stop: async () => {
      killTree(handle.proc.pid);
      await Promise.race([handle.exited, sleep(10000)]);
    },
  };
}

// ---------------------------------------------------------------------------
// docker / psql / RabbitMQ management API
// ---------------------------------------------------------------------------

export function docker(...args) {
  const r = spawnSync('docker', args, { encoding: 'utf8', env: { ...process.env, MSYS_NO_PATHCONV: '1' } });
  return { code: r.status, stdout: r.stdout ?? '', stderr: r.stderr ?? '' };
}

export function dockerOrThrow(...args) {
  const r = docker(...args);
  if (r.code !== 0) throw new Error(`docker ${args.join(' ')} failed (code ${r.code}): ${r.stderr || r.stdout}`);
  return r;
}

/** Runs one SQL statement inside the zionshop-postgres container via `docker exec psql -At`.
 * Returns rows as string[][] split on '|' (psql unaligned output), [] for no rows. */
export function psql(sql, { database = 'ZionShop' } = {}) {
  const password = process.env.DB_PASSWORD || '123@123a';
  const r = docker('exec', '-e', `PGPASSWORD=${password}`, 'zionshop-postgres', 'psql', '-At', '-U', 'postgres', '-d', database, '-c', sql);
  if (r.code !== 0) throw new Error(`psql failed: ${r.stderr || r.stdout}`);
  const text = r.stdout.trim();
  return text ? text.split('\n').map((line) => line.split('|')) : [];
}

async function rabbitApi(urlPath) {
  const user = process.env.RABBITMQ_USER || 'guest';
  const pass = process.env.RABBITMQ_PASS || 'guest';
  const auth = Buffer.from(`${user}:${pass}`).toString('base64');
  const res = await fetch(`http://localhost:15672${urlPath}`, { headers: { Authorization: `Basic ${auth}` } });
  if (!res.ok) throw new Error(`RabbitMQ management API ${urlPath} returned ${res.status}`);
  return res.json();
}

export async function rabbitQueues() {
  return rabbitApi('/api/queues/%2F');
}

export async function rabbitQueue(name) {
  return rabbitApi(`/api/queues/%2F/${encodeURIComponent(name)}`);
}

// ---------------------------------------------------------------------------
// waitUntil
// ---------------------------------------------------------------------------

export async function waitUntil(fn, { timeoutMs = 20000, intervalMs = 500, label = 'condition' } = {}) {
  const deadline = Date.now() + timeoutMs;
  let last;
  while (Date.now() < deadline) {
    last = await fn();
    if (last) return last;
    await sleep(intervalMs);
  }
  throw new Error(`Timed out after ${timeoutMs}ms waiting for: ${label}`);
}

// ---------------------------------------------------------------------------
// API HTTP helpers (api/v1)
// ---------------------------------------------------------------------------

const API_BASE = 'http://localhost:8080/api/v1';

export async function apiCall(method, urlPath, { token, body, headers = {} } = {}) {
  const t0 = Date.now();
  const res = await fetch(API_BASE + urlPath, {
    method,
    headers: {
      'content-type': 'application/json',
      ...(token ? { authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const durationMs = Date.now() - t0;
  const text = await res.text();
  let json;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = text;
  }
  return { status: res.status, headers: res.headers, json, durationMs };
}

export async function login(email, password) {
  const r = await apiCall('POST', '/auth/login', { body: { email, password } });
  if (r.status !== 200) throw new Error(`login failed for ${email}: ${r.status} ${JSON.stringify(r.json)}`);
  return r.json.data; // { accessToken, accessTokenExpiresAt, refreshToken, userId, email, roles }
}

export async function adminLogin() {
  return login('admin@zionshop.local', 'Admin@123');
}

export async function createPublishedProduct(adminToken, { price = 100000 } = {}) {
  const token = Math.random().toString(36).slice(2, 10);
  const sku = `LAB-${token.toUpperCase()}`;
  const create = await apiCall('POST', '/products', {
    token: adminToken,
    body: {
      name: `Lab Product ${token}`,
      slug: `lab-product-${token}`,
      sku,
      price,
      currency: 'VND',
      categoryId: null,
      description: 'tests/Labs seed product',
      brand: 'LabBrand',
      images: [],
    },
  });
  if (create.status >= 300) throw new Error(`create product failed: ${create.status} ${JSON.stringify(create.json)}`);
  const productId = create.json.data.id;
  const slug = create.json.data.slug;
  const publish = await apiCall('POST', `/products/${productId}/publish`, { token: adminToken });
  if (publish.status >= 300) throw new Error(`publish product failed: ${publish.status} ${JSON.stringify(publish.json)}`);
  return { productId, sku, slug };
}

export async function adjustStock(adminToken, productId, delta, reason = 'tests/Labs seed') {
  const r = await apiCall('POST', `/inventory/products/${productId}/adjust`, { token: adminToken, body: { delta, reason } });
  if (r.status >= 300) throw new Error(`adjust stock failed: ${r.status} ${JSON.stringify(r.json)}`);
  return r.json.data;
}

export async function getInventoryByProductId(token, productId) {
  const r = await apiCall('GET', `/inventory/products/${productId}`, { token });
  if (r.status >= 300) throw new Error(`get availability failed: ${r.status} ${JSON.stringify(r.json)}`);
  return r.json.data;
}

export async function getInventoryBySku(adminToken, sku) {
  const r = await apiCall('GET', `/inventory?keyword=${encodeURIComponent(sku)}`, { token: adminToken });
  if (r.status >= 300) throw new Error(`list inventory failed: ${r.status} ${JSON.stringify(r.json)}`);
  const match = r.json.data.find((i) => i.sku === sku);
  if (!match) throw new Error(`no inventory row found for SKU ${sku}`);
  return match;
}

/**
 * Polls GET /inventory for `sku` until `predicate(inventory)` is true or `timeoutMs` elapses.
 * The Inventory "commit" that finalizes OnHand/Reserved for a COD order is driven by a later
 * integration event (OrderConfirmed -> Inventory's OrderConfirmedIntegrationEventHandler), so it
 * lands asynchronously *after* the order already shows Confirmed -- callers must poll here instead
 * of reading inventory once right after the order settles. Returns the last observed value
 * regardless of whether the predicate ever passed, so the caller's own assertion stays exact.
 */
export async function waitForInventory(adminToken, sku, predicate, { timeoutMs = 30000, intervalMs = 500 } = {}) {
  const start = Date.now();
  let last;
  for (;;) {
    last = await getInventoryBySku(adminToken, sku);
    if (predicate(last)) break;
    if (Date.now() - start >= timeoutMs) break;
    await sleep(intervalMs);
  }
  return { value: last, elapsedMs: Date.now() - start };
}

/** Counts StockReservations rows for one (orderId, productId) pair -- the table's unique index -- via psql. */
export function countStockReservations(orderId, productId) {
  const rows = psql(`SELECT COUNT(*) FROM inventory."StockReservations" WHERE "OrderId" = '${orderId}' AND "ProductId" = '${productId}';`);
  return rows.length ? parseInt(rows[0][0], 10) : 0;
}

/**
 * Counts Inbox rows (schema.inbox_messages, PK (MessageId, Consumer)) for one messageId + a
 * consumer-name suffix, via psql -- bypasses the paginated admin API (GET /admin/system/inbox),
 * which filters client-side and can miss the row once a module's inbox has thousands of entries.
 * Returns { count, minProcessedOn } where minProcessedOn is the raw psql timestamptz text (or null).
 */
export function countInboxMessages(schema, messageId, consumerLike) {
  const rows = psql(
    `SELECT COUNT(*), MIN("ProcessedOn") FROM ${schema}.inbox_messages WHERE "MessageId" = '${messageId}'::uuid AND "Consumer" LIKE '%${consumerLike}';`,
  );
  if (!rows.length) return { count: 0, minProcessedOn: null };
  const [count, minProcessedOn] = rows[0];
  return { count: parseInt(count, 10), minProcessedOn: minProcessedOn || null };
}

export async function getOrCreateAddress(token) {
  const existing = await apiCall('GET', '/users/me/addresses', { token });
  if (existing.status === 200 && Array.isArray(existing.json.data) && existing.json.data.length > 0) {
    return existing.json.data[0].id;
  }
  const r = await apiCall('POST', '/users/me/addresses', {
    token,
    body: {
      recipientName: 'Lab Tester',
      phoneNumber: '0900000000',
      province: 'Thành phố Hà Nội',
      ward: 'Phường Ba Đình',
      streetAddress: '1 Lab St',
      isDefault: true,
    },
  });
  if (r.status >= 300) throw new Error(`create address failed: ${r.status} ${JSON.stringify(r.json)}`);
  return r.json.data.id;
}

export async function clearCart(token) {
  const r = await apiCall('DELETE', '/cart', { token });
  if (r.status >= 300 && r.status !== 404) throw new Error(`clear cart failed: ${r.status} ${JSON.stringify(r.json)}`);
}

export async function addToCart(token, productId, quantity) {
  const r = await apiCall('POST', '/cart/items', { token, body: { productId, quantity } });
  if (r.status >= 300) throw new Error(`add to cart failed: ${r.status} ${JSON.stringify(r.json)}`);
  return r.json.data;
}

export async function placeOrder(token, { addressId, paymentMethod = 'COD', note, couponCode, idempotencyKey } = {}) {
  const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
  return apiCall('POST', '/orders', { token, body: { addressId, paymentMethod, note, couponCode }, headers });
}

export async function getOrder(token, orderId) {
  return apiCall('GET', `/orders/${orderId}`, { token });
}

export async function waitForOrderStatus(token, orderId, statuses, { timeoutMs = 30000, intervalMs = 500 } = {}) {
  const deadline = Date.now() + timeoutMs;
  let last;
  while (Date.now() < deadline) {
    const r = await getOrder(token, orderId);
    last = r.json?.data?.status;
    if (statuses.includes(last)) return last;
    await sleep(intervalMs);
  }
  throw new Error(`order ${orderId} did not reach one of [${statuses.join(',')}] within ${timeoutMs}ms; last observed status: ${last}`);
}

export async function getMyOrdersCount(token) {
  const r = await apiCall('GET', '/orders/mine?page=1&pageSize=1', { token });
  if (r.status >= 300) throw new Error(`get my orders failed: ${r.status}`);
  return r.json.pagination.totalCount;
}

// ---------------------------------------------------------------------------
// Admin "Hệ thống" endpoints (api/v1/admin/system)
// ---------------------------------------------------------------------------

export async function adminHealth(adminToken) {
  const r = await apiCall('GET', '/admin/system/health', { token: adminToken });
  if (r.status >= 300) throw new Error(`admin health failed: ${r.status} ${JSON.stringify(r.json)}`);
  return r.json.data; // { status, totalDurationMs, checkedAt, checks: [{name,status,durationMs,description}] }
}

export async function adminOutboxList(adminToken, { module, status, page = 1, pageSize = 50 } = {}) {
  const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (module) qs.set('module', module);
  if (status) qs.set('status', status);
  const r = await apiCall('GET', `/admin/system/outbox?${qs}`, { token: adminToken });
  if (r.status >= 300) throw new Error(`admin outbox list failed: ${r.status} ${JSON.stringify(r.json)}`);
  return r.json.data; // OutboxMessageSummary[]
}

export async function adminOutboxDetail(adminToken, module, id) {
  const r = await apiCall('GET', `/admin/system/outbox/${module}/${id}`, { token: adminToken });
  if (r.status >= 300) throw new Error(`admin outbox detail failed: ${r.status} ${JSON.stringify(r.json)}`);
  return r.json.data; // OutboxMessageDetail
}

/** Polls the given module's outbox until a row whose (unmasked) payload contains orderId is found. */
export async function findOutboxMessageForOrder(adminToken, module, orderId, { status, timeoutMs = 10000, intervalMs = 500 } = {}) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const list = await adminOutboxList(adminToken, { module, status, pageSize: 100 });
    for (const summary of list) {
      const detail = await adminOutboxDetail(adminToken, module, summary.id);
      if (detail.payload.includes(orderId)) return detail;
    }
    await sleep(intervalMs);
  }
  return null;
}

export async function adminInboxList(adminToken, { module, page = 1, pageSize = 50 } = {}) {
  const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (module) qs.set('module', module);
  const r = await apiCall('GET', `/admin/system/inbox?${qs}`, { token: adminToken });
  if (r.status >= 300) throw new Error(`admin inbox list failed: ${r.status} ${JSON.stringify(r.json)}`);
  return r.json.data; // InboxMessageSummary[]
}
