import http from 'k6/http';
import { check } from 'k6';

export const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:8080';

export function jsonHeaders(token) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  return headers;
}

export function parseEnvelope(res) {
  try {
    return res.json();
  } catch (e) {
    return null;
  }
}

// POST /api/v1/auth/login -> { success, data: { accessToken, ... } }. Throws (aborting the
// run with a clear message) rather than letting every later request fail on a missing token.
export function login(email, password, tagName) {
  const res = http.post(
    `${BASE_URL}/api/v1/auth/login`,
    JSON.stringify({ email, password }),
    { headers: jsonHeaders(), tags: { name: tagName || 'auth_login' } }
  );
  const body = parseEnvelope(res);
  const ok = check(res, {
    'login succeeded': () => res.status === 200 && !!(body && body.data && body.data.accessToken),
  });
  if (!ok) {
    throw new Error(`login failed for ${email}: status=${res.status} body=${res.body}`);
  }
  return body.data.accessToken;
}

export function randomPick(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}
