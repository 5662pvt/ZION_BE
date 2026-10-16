import http from 'k6/http';
import { check } from 'k6';
import { Trend } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import { BASE_URL, jsonHeaders, randomPick } from './lib.js';
import { fetchCatalogSample } from './setup-data.js';

// GET /api/v1/products/by-slug/{slug} and GET /api/v1/products/{id} — the cached product-detail
// path (GetProductBySlugQueryHandler / GetProductByIdQueryHandler both go through
// IDistributedCacheService.GetOrCreateAsync, 5 min TTL). This is the main cache-on vs cache-off
// comparison endpoint. ~80% of traffic goes by slug (the storefront's actual lookup), 20% by id.

const bySlugTrend = new Trend('product_detail_by_slug_duration', true);
const byIdTrend = new Trend('product_detail_by_id_duration', true);

export const options = {
  scenarios: {
    product_detail: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.RATE || 100),
      timeUnit: '1s',
      duration: __ENV.DURATION || '60s',
      preAllocatedVUs: Number(__ENV.PREALLOC_VUS || 30),
      maxVUs: Number(__ENV.MAX_VUS || 400),
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<30000'],
    http_req_failed: ['rate<1.0'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

export function setup() {
  const catalog = fetchCatalogSample(100);
  return { catalog };
}

export default function (data) {
  const product = randomPick(data.catalog);
  if (Math.random() < 0.8) {
    const res = http.get(`${BASE_URL}/api/v1/products/by-slug/${product.slug}`, {
      headers: jsonHeaders(),
      tags: { name: 'product_detail_by_slug' },
    });
    bySlugTrend.add(res.timings.duration);
    check(res, { 'product by slug status 200': (r) => r.status === 200 });
  } else {
    const res = http.get(`${BASE_URL}/api/v1/products/${product.id}`, {
      headers: jsonHeaders(),
      tags: { name: 'product_detail_by_id' },
    });
    byIdTrend.add(res.timings.duration);
    check(res, { 'product by id status 200': (r) => r.status === 200 });
  }
}

export function handleSummary(data) {
  const label = __ENV.LABEL || 'run';
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    [`/results/product-detail-${label}.json`]: JSON.stringify(data),
  };
}
