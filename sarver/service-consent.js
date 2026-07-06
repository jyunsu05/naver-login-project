const CONSENT_COOKIE_NAME = 'naver_service_consent';
const CONSENT_COOKIE_VALUE = '1';
const CONSENT_MAX_AGE_SECONDS = 365 * 24 * 60 * 60;

function getCookie(req, name) {
  const raw = req.headers.cookie || '';
  const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const match = raw.match(new RegExp(`(?:^|; )${escaped}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

function hasServiceConsent(req) {
  return getCookie(req, CONSENT_COOKIE_NAME) === CONSENT_COOKIE_VALUE;
}

function buildConsentCookieHeader({ maxAge = CONSENT_MAX_AGE_SECONDS } = {}) {
  const parts = [
    `${CONSENT_COOKIE_NAME}=${CONSENT_COOKIE_VALUE}`,
    'Path=/',
    `Max-Age=${maxAge}`,
    'SameSite=Lax',
  ];

  if (maxAge <= 0) {
    parts.push('Expires=Thu, 01 Jan 1970 00:00:00 GMT');
  }

  return parts.join('; ');
}

function setServiceConsent(res) {
  res.setHeader('Set-Cookie', buildConsentCookieHeader());
}

function clearServiceConsent(res) {
  res.setHeader('Set-Cookie', buildConsentCookieHeader({ maxAge: 0 }));
}

module.exports = {
  CONSENT_COOKIE_NAME,
  hasServiceConsent,
  setServiceConsent,
  clearServiceConsent,
};
