import http from 'k6/http';
import { check } from 'k6';
import { Trend, Counter } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import { BASE_URL, jsonHeaders, randomPick } from './lib.js';
import { adminLogin, createLoadTestProducts } from './setup-data.js';

// End-to-end checkout: login -> ensure address -> add 1-2 items to cart -> POST /api/v1/orders
// (COD, fresh Idempotency-Key per attempt). Mirrors .crew-panes/w17/ref/w11-smoke.mjs /
// w16-smoke.mjs and tests/Integration/.../Checkout/CheckoutTestHelpers.cs.

const loginTrend = new Trend('order_login_duration', true);
const addressTrend = new Trend('order_address_duration', true);
const cartTrend = new Trend('order_cart_duration', true);
const orderTrend = new Trend('order_place_duration', true);
const rateLimited = new Counter('order_rate_limited_total');

const USERS = Number(__ENV.USERS || 50);
const LOAD_USER_PASSWORD = __ENV.LOAD_USER_PASSWORD || 'Load@123';
const PRODUCT_COUNT = Number(__ENV.PRODUCT_COUNT || 20);

const MAX_VUS = Number(__ENV.MAX_VUS || USERS);

export const options = {
  scenarios: {
    place_order: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.RATE || 10),
      timeUnit: '1s',
      duration: __ENV.DURATION || '60s',
      preAllocatedVUs: Number(__ENV.PREALLOC_VUS || 20),
      maxVUs: MAX_VUS,
    },
  },
  thresholds: {
    // Report-only: no abortOnFail, so a breach never stops the run. 429s are tracked
    // separately via order_rate_limited_total rather than counted as http_req_failed.
    http_req_duration: ['p(95)<30000'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

// Requires `Seed__LoadTestCustomers=<N>` (N >= USERS) on the API so load{1..N}@zionshop.local /
// Load@123 exist. Creates a dedicated "LT-<timestamp>" product set with huge stock so this
// script never runs out of inventory or collides with the real catalog.
//
// maxVUs must stay <= USERS: loadUserEmail() maps each VU to its own load user by __VU, so if
// k6 ever allocates more VUs than there are load users, two VUs share one user and race on the
// same cart (concurrent DELETE/POST /cart from two VUs) — the W17 baseline run hit exactly this
// (409s on DELETE /cart, one 400 on POST /orders) with maxVUs=60 > USERS=50. Fail fast instead.
export function setup() {
  if (MAX_VUS > USERS) {
    throw new Error(
      `setup: MAX_VUS (${MAX_VUS}) must be <= USERS (${USERS}) — each VU maps to its own load user ` +
        `by __VU, so more VUs than users means two VUs share a user and race on the same cart.`
    );
  }
  const runTag = `${Date.now()}`;
  const adminToken = adminLogin();
  const products = createLoadTestProducts(adminToken, PRODUCT_COUNT, runTag);
  if (products.length === 0) {
    throw new Error('setup: failed to create any load-test products');
  }
  return { products, runTag };
}

// Per-VU state: k6 runs each VU as its own JS VM instance, so these module-level variables are
// naturally VU-scoped and persist across iterations of the same VU (login/address done once).
let vuToken = null;
let vuAddressId = null;

function loadUserEmail() {
  const n = ((__VU - 1) % USERS) + 1;
  return `load${n}@zionshop.local`;
}

function ensureAuth() {
  if (vuToken) return;
  const email = loadUserEmail();
  const res = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email, password: LOAD_USER_PASSWORD }),
    { headers: jsonHeaders(), tags: { name: 'order_login' } }
  );
  loginTrend.add(res.timings.duration);
  const body = res.json();
  if (res.status !== 200 || !body || !body.data || !body.data.accessToken) {
    throw new Error(`VU${__VU}: login failed for ${email} status=${res.status} body=${res.body}`);
  }
  vuToken = body.data.accessToken;
}

// Reuses the VU's first address if one already exists (e.g. left over from a prior run),
// otherwise creates one. Resolved once per VU and cached in vuAddressId.
function ensureAddress() {
  if (vuAddressId) return;
  const listRes = http.get(`${BASE_URL}/api/v1/users/me/addresses`, {
    headers: jsonHeaders(vuToken),
    tags: { name: 'order_address' },
  });
  addressTrend.add(listRes.timings.duration);
  const listBody = listRes.json();
  if (listRes.status === 200 && listBody.data && listBody.data.length > 0) {
    vuAddressId = listBody.data[0].id;
    return;
  }

  const createRes = http.post(
    `${BASE_URL}/api/v1/users/me/addresses`,
    JSON.stringify({
      recipientName: `Load Test VU${__VU}`,
      phoneNumber: '0900000000',
      province: 'Thành phố Hà Nội',
      ward: 'Phường Ba Đình',
      streetAddress: `${__VU} Load Test Street`,
      isDefault: true,
    }),
    { headers: jsonHeaders(vuToken), tags: { name: 'order_address' } }
  );
  if (createRes.status !== 200 && createRes.status !== 201) {
    throw new Error(`VU${__VU}: create address failed status=${createRes.status} body=${createRes.body}`);
  }
  const createBody = createRes.json();
  if (createBody && createBody.data && createBody.data.id) {
    vuAddressId = createBody.data.id;
  } else {
    const relist = http.get(`${BASE_URL}/api/v1/users/me/addresses`, { headers: jsonHeaders(vuToken) });
    vuAddressId = relist.json().data[0].id;
  }
}

// DELETE /api/v1/cart clears the authenticated user's cart. Called at the start of every
// iteration so load users shared across VUs (when maxVUs > USERS) never inherit stale lines.
function clearCart() {
  const res = http.del(`${BASE_URL}/api/v1/cart`, null, {
    headers: jsonHeaders(vuToken),
    tags: { name: 'order_cart' },
  });
  cartTrend.add(res.timings.duration);
}

export default function (data) {
  ensureAuth();
  ensureAddress();
  clearCart();

  const itemCount = 1 + Math.floor(Math.random() * 2); // 1-2 distinct load-test products
  for (let i = 0; i < itemCount; i++) {
    const product = randomPick(data.products);
    const addRes = http.post(
      `${BASE_URL}/api/v1/cart/items`,
      JSON.stringify({ productId: product.id, quantity: 1 }),
      { headers: jsonHeaders(vuToken), tags: { name: 'order_cart' } }
    );
    cartTrend.add(addRes.timings.duration);
    check(addRes, { 'cart add status 200': (r) => r.status === 200 });
  }

  const idempotencyKey = `k6-${__VU}-${__ITER}-${Date.now()}`;
  const orderRes = http.post(
    `${BASE_URL}/api/v1/orders`,
    JSON.stringify({ addressId: vuAddressId, paymentMethod: 'COD', note: 'k6 load test' }),
    {
      headers: Object.assign({ 'Idempotency-Key': idempotencyKey }, jsonHeaders(vuToken)),
      tags: { name: 'order_place' },
    }
  );
  orderTrend.add(orderRes.timings.duration);

  if (orderRes.status === 429) {
    rateLimited.add(1);
  } else {
    const orderBody = orderRes.json();
    check(orderRes, {
      'order place status 200/201': (r) => r.status === 200 || r.status === 201,
      'order has id': () => !!(orderBody && orderBody.data && orderBody.data.id),
    });
  }
}

export function handleSummary(data) {
  const label = __ENV.LABEL || 'run';
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    [`/results/place-order-${label}.json`]: JSON.stringify(data),
  };
}
