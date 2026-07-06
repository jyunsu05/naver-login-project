const crypto = require('crypto');

const NAVER_AUTHORIZE_URL = 'https://nid.naver.com/oauth2.0/authorize';
const NAVER_TOKEN_URL = 'https://nid.naver.com/oauth2.0/token';
const NAVER_PROFILE_URL = 'https://openapi.naver.com/v1/nid/me';

const pendingStates = new Map();
const STATE_TTL_MS = 10 * 60 * 1000;

function getConfig() {
  const clientId = process.env.NAVER_CLIENT_ID;
  const clientSecret = process.env.NAVER_CLIENT_SECRET;
  const callbackUrl = process.env.NAVER_CALLBACK_URL || 'http://localhost:3000/auth/naver/callback';

  if (!clientId || !clientSecret) {
    throw new Error('NAVER_CLIENT_ID, NAVER_CLIENT_SECRET이 .env에 필요합니다.');
  }

  return { clientId, clientSecret, callbackUrl };
}

function pruneExpiredStates() {
  const now = Date.now();
  for (const [state, createdAt] of pendingStates.entries()) {
    if (now - createdAt > STATE_TTL_MS) {
      pendingStates.delete(state);
    }
  }
}

function createState() {
  pruneExpiredStates();
  const state = crypto.randomBytes(16).toString('hex');
  pendingStates.set(state, Date.now());
  return state;
}

function consumeState(state) {
  pruneExpiredStates();
  if (!state || !pendingStates.has(state)) {
    return false;
  }
  pendingStates.delete(state);
  return true;
}

function buildAuthorizeUrl() {
  const { clientId, callbackUrl } = getConfig();
  const state = createState();
  const params = new URLSearchParams({
    response_type: 'code',
    client_id: clientId,
    redirect_uri: callbackUrl,
    state,
  });

  return `${NAVER_AUTHORIZE_URL}?${params.toString()}`;
}

async function exchangeCodeForToken(code, state) {
  if (!consumeState(state)) {
    throw new Error('유효하지 않거나 만료된 state입니다.');
  }

  const { clientId, clientSecret, callbackUrl } = getConfig();
  const params = new URLSearchParams({
    grant_type: 'authorization_code',
    client_id: clientId,
    client_secret: clientSecret,
    redirect_uri: callbackUrl,
    code,
    state,
  });

  const response = await fetch(`${NAVER_TOKEN_URL}?${params.toString()}`);
  const data = await response.json();

  if (!response.ok || data.error) {
    throw new Error(data.error_description || data.error || '네이버 토큰 발급 실패');
  }

  return data;
}

async function revokeAccessToken(accessToken) {
  if (!accessToken) {
    throw new Error('폐기할 access token이 없습니다.');
  }

  const { clientId, clientSecret } = getConfig();
  const params = new URLSearchParams({
    grant_type: 'delete',
    client_id: clientId,
    client_secret: clientSecret,
    access_token: accessToken,
    service_provider: 'NAVER',
  });

  const response = await fetch(NAVER_TOKEN_URL, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: params.toString(),
  });
  const data = await response.json();

  if (!response.ok || data.error) {
    throw new Error(data.error_description || data.error || '네이버 토큰 폐기 실패');
  }

  if (data.result && data.result !== 'success') {
    throw new Error('네이버 토큰 폐기 실패');
  }

  return data;
}

async function refreshAccessToken(refreshToken) {
  if (!refreshToken) {
    throw new Error('refresh token이 없습니다.');
  }

  const { clientId, clientSecret } = getConfig();
  const params = new URLSearchParams({
    grant_type: 'refresh_token',
    client_id: clientId,
    client_secret: clientSecret,
    refresh_token: refreshToken,
  });

  const response = await fetch(`${NAVER_TOKEN_URL}?${params.toString()}`);
  const data = await response.json();

  if (!response.ok || data.error) {
    throw new Error(data.error_description || data.error || '네이버 토큰 갱신 실패');
  }

  return data;
}

async function fetchProfile(accessToken) {
  const response = await fetch(NAVER_PROFILE_URL, {
    headers: {
      Authorization: `Bearer ${accessToken}`,
    },
  });

  const data = await response.json();

  if (!response.ok || data.resultcode !== '00') {
    throw new Error(data.message || '네이버 프로필 조회 실패');
  }

  return data.response;
}

function mapProfileToUser(profile) {
  const uid = String(profile.id);
  const name = String(profile.name || profile.nickname || '네이버사용자').trim();
  const email = String(profile.email || `naver+${uid}@oauth.local`).trim();

  return { uid, email, name };
}

module.exports = {
  buildAuthorizeUrl,
  exchangeCodeForToken,
  revokeAccessToken,
  refreshAccessToken,
  fetchProfile,
  mapProfileToUser,
  getConfig,
};
