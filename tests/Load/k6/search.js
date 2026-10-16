import http from 'k6/http';
import { check } from 'k6';
import { Trend } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import { BASE_URL, jsonHeaders, randomPick } from './lib.js';
import { fetchCatalogSample } from './setup-data.js';

// GET /api/v1/products?keyword=...&page=...&pageSize=... — product search/list.
// NOT cached (only GetProductById / GetProductBySlug / GetCategories use IDistributedCacheService),
// so this baseline measures the uncached catalog query path under both cache-on and cache-off runs.

const searchTrend = new Trend('products_search_duration', true);

const FALLBACK_KEYWORDS = ['ao', 'quan', 'giay', 'tui', 'dien thoai', 'laptop', 'ban', 'ghe', 'den', 'sach', 'vo', 'but'];

export const options = {
  scenarios: {
    search: {
      executor: 'constant-arrival-rate',
      rate: Number(__ENV.RATE || 50),
      timeUnit: '1s',
      duration: __ENV.DURATION || '60s',
      preAllocatedVUs: Number(__ENV.PREALLOC_VUS || 20),
      maxVUs: Number(__ENV.MAX_VUS || 300),
    },
  },
  thresholds: {
    // Report-only: no abortOnFail, so a breach never stops the run — it just shows up (in red)
    // in the printed summary / JSON output for the orchestrator to read afterwards.
    http_req_duration: ['p(95)<30000'],
    http_req_failed: ['rate<1.0'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
};

export function setup() {
  const catalog = fetchCatalogSample(100);
  const nameWords = catalog
    .flatMap((p) => (p.name || '').split(/\s+/))
    .map((w) => w.trim())
    .filter((w) => w.length >= 3);
  const uniqueWords = Array.from(new Set(nameWords)).slice(0, 15);
  const keywords = uniqueWords.length > 0 ? uniqueWords : FALLBACK_KEYWORDS;
  return { keywords, catalogSize: catalog.length };
}

export default function (data) {
  const keyword = randomPick(data.keywords);
  const page = 1 + Math.floor(Math.random() * 5);
  const res = http.get(
    `${BASE_URL}/api/v1/products?keyword=${encodeURIComponent(keyword)}&page=${page}&pageSize=20`,
    { headers: jsonHeaders(), tags: { name: 'products_search' } }
  );
  searchTrend.add(res.timings.duration);
  check(res, { 'search status 200': (r) => r.status === 200 });
}

export function handleSummary(data) {
  const label = __ENV.LABEL || 'run';
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    [`/results/search-${label}.json`]: JSON.stringify(data),
  };
}
