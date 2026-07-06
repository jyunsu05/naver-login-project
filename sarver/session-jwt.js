const crypto = require('crypto');

const DEFAULT_TTL_SECONDS = 60 * 60 * 24 * 30;

function getJwtSecret() {
  const secret = process.env.SESSION_JWT_SECRET;
  if (!secret) {
    throw new Error('SESSION_JWT_SECRET이 .env에 필요합니다.');
  }

  return secret;
}

function base64UrlEncode(value) {
  return Buffer.from(value)
    .toString('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
}

function base64UrlDecode(value) {
  const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
  const padding = normalized.length % 4 === 0 ? '' : '='.repeat(4 - (normalized.length % 4));
  return Buffer.from(normalized + padding, 'base64').toString('utf8');
}

function signJwt(payload) {
  const header = base64UrlEncode(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const body = base64UrlEncode(JSON.stringify(payload));
  const signature = crypto
    .createHmac('sha256', getJwtSecret())
    .update(`${header}.${body}`)
    .digest('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');

  return `${header}.${body}.${signature}`;
}

function verifyJwt(token) {
  if (!token) {
    throw new Error('sessionToken이 필요합니다.');
  }

  const parts = String(token).split('.');
  if (parts.length !== 3) {
    throw new Error('유효하지 않은 sessionToken입니다.');
  }

  const [header, body, signature] = parts;
  const expected = crypto
    .createHmac('sha256', getJwtSecret())
    .update(`${header}.${body}`)
    .digest('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');

  const expectedBuffer = Buffer.from(expected);
  const signatureBuffer = Buffer.from(signature);
  if (
    expectedBuffer.length !== signatureBuffer.length
    || !crypto.timingSafeEqual(expectedBuffer, signatureBuffer)
  ) {
    throw new Error('sessionToken 서명이 올바르지 않습니다.');
  }

  const payload = JSON.parse(base64UrlDecode(body));
  if (!payload.exp || payload.exp * 1000 <= Date.now()) {
    throw new Error('sessionToken이 만료되었습니다.');
  }

  return payload;
}

function issueSessionToken(user, options = {}) {
  const ttlSeconds = Number(options.ttlSeconds) > 0
    ? Number(options.ttlSeconds)
    : Number(process.env.SESSION_JWT_TTL_SECONDS || DEFAULT_TTL_SECONDS);

  const now = Math.floor(Date.now() / 1000);
  const payload = {
    uid: user.uid,
    sv: Number(user.session_version || 0),
    iat: now,
    exp: now + ttlSeconds,
  };

  return signJwt(payload);
}

function extractSessionToken(req) {
  const authHeader = String(req.headers.authorization || '');
  if (authHeader.startsWith('Bearer ')) {
    return authHeader.slice('Bearer '.length).trim();
  }

  return String(req.body?.sessionToken || req.query?.token || '').trim();
}

module.exports = {
  issueSessionToken,
  verifyJwt,
  extractSessionToken,
};
