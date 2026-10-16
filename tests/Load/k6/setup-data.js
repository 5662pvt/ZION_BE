import http from 'k6/http';
import { BASE_URL, jsonHeaders, login } from './lib.js';

// Shared data bootstrap helpers, called from each script's own setup() — not a standalone
// tool, k6 has no "run this before the test" mechanism other than the setup() lifecycle hook.

export function adminLogin() {
  const email = __ENV.ADMIN_EMAIL || 'admin@zionshop.local';
  const password = __ENV.ADMIN_PASSWORD || 'Admin@123';
  return login(email, password, 'setup_admin_login');
}

// GET /api/v1/products (public search/list, NOT cached) and collect {id, slug, name} for the
// existing published catalog. Used by search.js / product-detail.js so they don't need their
// own product fixtures. Fails fast with a clear message if the catalog is empty.
export function fetchCatalogSample(limit) {
  const pageSize = limit || 100;
  const res = http.get(`${BASE_URL}/api/v1/products?page=1&pageSize=${pageSize}`, {
    headers: jsonHeaders(),
    tags: { name: 'setup_fetch_catalog' },
  });
  const body = res.json();
  const items = (body && body.data) || [];
  if (res.status !== 200 || items.length === 0) {
    throw new Error(
      `setup: no published products found in the catalog (status=${res.status}, count=${items.length}). ` +
      'Seed the database with published products (e.g. run the API seeder) before running this script.'
    );
  }
  return items.map((p) => ({ id: p.id, slug: p.slug, name: p.name }));
}

// Creates `count` published products named "LT-<runTag> ..." with SKU "LT-<runTag>-<i>" and a
// large stock adjustment, dedicated to place-order.js so it never competes for stock with the
// real catalog or other load-test runs. Mirrors CheckoutTestHelpers.CreatePublishedProductAsync
// / AdjustStockAsync (tests/Integration/ZIONShop.Api.IntegrationTests/Checkout).
export function createLoadTestProducts(adminToken, count, runTag) {
  const products = [];
  for (let i = 0; i < count; i++) {
    const slugTag = `${runTag}-${i}`.toLowerCase();
    const sku = `LT-${runTag}-${i}`.toUpperCase();

    const createRes = http.post(
      `${BASE_URL}/api/v1/products`,
      JSON.stringify({
        name: `LT-${runTag} Load Test Product ${i}`,
        slug: `lt-${slugTag}-load-test-product`,
        sku,
        price: 100000,
        currency: 'VND',
        categoryId: null,
        description: 'k6 load test product',
        brand: 'LoadTest',
        images: [],
      }),
      { headers: jsonHeaders(adminToken), tags: { name: 'setup_create_product' } }
    );
    if (createRes.status !== 200 && createRes.status !== 201) {
      throw new Error(`setup: create load-test product #${i} failed status=${createRes.status} body=${createRes.body}`);
    }
    const created = createRes.json();
    const productId = created.data.id;

    const publishRes = http.post(`${BASE_URL}/api/v1/products/${productId}/publish`, null, {
      headers: jsonHeaders(adminToken),
      tags: { name: 'setup_publish_product' },
    });
    if (publishRes.status !== 200) {
      throw new Error(`setup: publish load-test product #${i} (${productId}) failed status=${publishRes.status} body=${publishRes.body}`);
    }

    const stockRes = http.post(
      `${BASE_URL}/api/v1/inventory/products/${productId}/adjust`,
      JSON.stringify({ delta: 1000000, reason: 'k6 load test seed' }),
      { headers: jsonHeaders(adminToken), tags: { name: 'setup_adjust_stock' } }
    );
    if (stockRes.status !== 200) {
      throw new Error(`setup: adjust stock for load-test product #${i} (${productId}) failed status=${stockRes.status} body=${stockRes.body}`);
    }

    products.push({ id: productId, sku });
  }
  return products;
}
