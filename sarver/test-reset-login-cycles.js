#!/usr/bin/env node
require('dotenv').config();

const { runDevFullReset } = require('./dev-full-reset');
const localSessionBridge = require('./local-session-bridge');
const { upsertUser } = require('./user-insert');
const {
  saveUserTokens,
  getUserFromSessionToken,
  logoutUserSession,
} = require('./user-tokens');
const { close } = require('./dbconnect');

const BASE = process.env.TEST_SERVER_URL || 'http://127.0.0.1:3000';

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function http(method, path, { headers = {}, body } = {}) {
  const response = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      ...headers,
      ...(body ? { 'Content-Type': 'application/json' } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
    redirect: 'manual',
  });

  const text = await response.text();
  let json = null;
  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    json = null;
  }

  return {
    status: response.status,
    headers: response.headers,
    text,
    json,
    location: response.headers.get('location'),
  };
}

async function simulateOAuthSuccess(cycle) {
  const uid = `e2e-cycle-${cycle}`;
  const user = await upsertUser({
    uid,
    email: `e2e${cycle}@test.local`,
    name: `E2E User ${cycle}`,
  });

  const { sessionToken } = await saveUserTokens(user.uid, {
    accessToken: `mock-access-${cycle}-${Date.now()}`,
    refreshToken: `mock-refresh-${cycle}`,
    expiresIn: 3600,
  });

  localSessionBridge.saveSession({ sessionToken, uid: user.uid });
  return { user, sessionToken };
}

async function testConsentGate(cookieHeader) {
  const withoutConsent = await http('GET', '/auth/naver');
  assert(withoutConsent.status === 302, `동의 없을 때 /auth/naver 기대 302, 실제 ${withoutConsent.status}`);
  assert(
    (withoutConsent.location || '').includes('/login/consent'),
    `동의 없을 때 consent로 리다이렉트 기대, 실제 ${withoutConsent.location}`,
  );

  const accept = await http('GET', '/login/consent/accept', {
    headers: cookieHeader ? { Cookie: cookieHeader } : {},
  });
  assert(accept.status === 302, `consent/accept 기대 302, 실제 ${accept.status}`);
  assert(
    (accept.location || '').includes('/auth/naver'),
    `consent/accept 후 /auth/naver 기대, 실제 ${accept.location}`,
  );

  const setCookie = accept.headers.get('set-cookie') || '';
  const mergedCookie = [cookieHeader, setCookie.split(';')[0]].filter(Boolean).join('; ');

  const withConsent = await http('GET', '/auth/naver', {
    headers: { Cookie: mergedCookie },
  });
  assert(withConsent.status === 302, `동의 후 /auth/naver 기대 302, 실제 ${withConsent.status}`);
  assert(
    (withConsent.location || '').includes('nid.naver.com'),
    `동의 후 Naver authorize 기대, 실제 ${withConsent.location}`,
  );

  return mergedCookie;
}

async function testBridgeAndAuthMe(sessionToken, cycle) {
  const bridge = await http('GET', '/auth/dev/session');
  assert(bridge.status === 200, `[cycle ${cycle}] bridge 기대 200, 실제 ${bridge.status}`);
  assert(bridge.json?.success === true, `[cycle ${cycle}] bridge success=false`);
  assert(bridge.json?.data?.sessionToken === sessionToken, `[cycle ${cycle}] bridge token 불일치`);

  const me = await http('POST', '/auth/me', {
    headers: { Authorization: `Bearer ${sessionToken}` },
  });
  assert(me.status === 200, `[cycle ${cycle}] /auth/me 기대 200, 실제 ${me.status}`);
  assert(me.json?.success === true, `[cycle ${cycle}] /auth/me success=false`);
  assert(me.json?.data?.user?.uid, `[cycle ${cycle}] /auth/me user 없음`);

  const meBody = await http('POST', '/auth/me', {
    body: { sessionToken },
  });
  assert(meBody.status === 200, `[cycle ${cycle}] /auth/me(body) 기대 200, 실제 ${meBody.status}`);
}

async function main() {
  console.log('=== E2E: 초기 로그인 → 전체 초기화 → 로그인 3회 ===\n');

  console.log('[0] 초기 1회 로그인 상태 만들기');
  const initial = await simulateOAuthSuccess(0);
  await testBridgeAndAuthMe(initial.sessionToken, 0);
  console.log('OK - 초기 로그인 상태\n');

  console.log('[1] 전체 초기화');
  const reset = await runDevFullReset({
    closeBrowsersFirst: true,
    ignoreBrowserLock: true,
  });
  assert(reset.success, `전체 초기화 실패: ${reset.message}`);
  console.log('OK - 전체 초기화');
  console.log(JSON.stringify(reset.steps.map((s) => `${s.step}: ${s.message}`), null, 2), '\n');

  let consentCookie = '';
  for (let cycle = 1; cycle <= 3; cycle += 1) {
    console.log(`[로그인 ${cycle}/3]`);
    if (cycle === 1) {
      consentCookie = await testConsentGate('');
      console.log(`OK - cycle ${cycle} 서비스 동의 게이트`);
    } else {
      const naver = await http('GET', '/auth/naver', {
        headers: consentCookie ? { Cookie: consentCookie } : {},
      });
      assert(naver.status === 302, `[cycle ${cycle}] /auth/naver 기대 302`);
      console.log(`OK - cycle ${cycle} 동의 쿠키 유지 (/auth/naver)`);
    }

    const login = await simulateOAuthSuccess(cycle);
    await testBridgeAndAuthMe(login.sessionToken, cycle);

    const logout = await http('POST', '/auth/logout', {
      headers: { Authorization: `Bearer ${login.sessionToken}` },
    });
    assert(logout.status === 200, `[cycle ${cycle}] logout 기대 200, 실제 ${logout.status}`);
    localSessionBridge.clearSession();
    console.log(`OK - cycle ${cycle} 로그인·브리지·/auth/me·로그아웃\n`);
  }

  console.log('=== 모든 사이클 통과 ===');
}

main()
  .catch((err) => {
    console.error('\nFAIL -', err.message);
    process.exitCode = 1;
  })
  .finally(() => close().catch(() => {}));
