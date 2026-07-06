const naverAuth = require('./naver-auth');
const { upsertUser } = require('./user-insert');
const { query, execute } = require('./dbconnect');
const { encryptText, decryptText } = require('./crypto-utils');
const { issueSessionToken, verifyJwt } = require('./session-jwt');
const logger = require('./logger');

const TOKEN_EXPIRY_BUFFER_MS = 60 * 1000;

const USER_COLUMNS = `
  id, uid, email, name, created_at,
  naver_access_token, naver_refresh_token, token_expires_at, session_version
`;

function computeExpiresAt(expiresIn) {
  const seconds = Number(expiresIn) > 0 ? Number(expiresIn) : 3600;
  return new Date(Date.now() + seconds * 1000);
}

function isAccessTokenExpired(tokenExpiresAt) {
  if (!tokenExpiresAt) {
    return true;
  }

  return new Date(tokenExpiresAt).getTime() <= Date.now() + TOKEN_EXPIRY_BUFFER_MS;
}

function mapUserRow(row) {
  if (!row) {
    return null;
  }

  return {
    ...row,
    naver_access_token: decryptText(row.naver_access_token),
    naver_refresh_token: decryptText(row.naver_refresh_token),
  };
}

async function findUserByUid(uid) {
  const rows = await query(
    `SELECT ${USER_COLUMNS} FROM users WHERE uid = ?`,
    [uid],
  );

  return mapUserRow(rows[0]);
}

async function saveUserTokens(uid, { accessToken, refreshToken, expiresIn }) {
  const tokenExpiresAt = computeExpiresAt(expiresIn);

  await execute(
    `UPDATE users
     SET naver_access_token = ?,
         naver_refresh_token = ?,
         token_expires_at = ?
     WHERE uid = ?`,
    [
      encryptText(accessToken),
      encryptText(refreshToken),
      tokenExpiresAt,
      uid,
    ],
  );

  const user = await findUserByUid(uid);
  const sessionToken = issueSessionToken(user);

  logger.log('USER', '네이버 토큰 저장 완료', {
    uid,
    tokenExpiresAt,
    hasRefreshToken: Boolean(refreshToken),
  });

  return { user, sessionToken };
}

async function getUserFromSessionToken(sessionToken) {
  const payload = verifyJwt(sessionToken);
  const user = await findUserByUid(payload.uid);

  if (!user) {
    throw new Error('사용자를 찾을 수 없습니다.');
  }

  if (Number(user.session_version || 0) !== Number(payload.sv || 0)) {
    throw new Error('세션이 만료되었습니다. 다시 로그인해 주세요.');
  }

  return user;
}

async function updateStoredTokens(uid, tokenData) {
  const tokenExpiresAt = computeExpiresAt(tokenData.expires_in);

  await execute(
    `UPDATE users
     SET naver_access_token = ?,
         naver_refresh_token = COALESCE(?, naver_refresh_token),
         token_expires_at = ?
     WHERE uid = ?`,
    [
      encryptText(tokenData.access_token),
      tokenData.refresh_token ? encryptText(tokenData.refresh_token) : null,
      tokenExpiresAt,
      uid,
    ],
  );

  return findUserByUid(uid);
}

async function refreshNaverTokensIfNeeded(user) {
  if (!user.naver_refresh_token && isAccessTokenExpired(user.token_expires_at)) {
    throw new Error('저장된 로그인 정보가 만료되었습니다. 다시 네이버 로그인이 필요합니다.');
  }

  if (!isAccessTokenExpired(user.token_expires_at)) {
    return user;
  }

  logger.log('NAVER', 'access token 만료, refresh token으로 갱신 시도', { uid: user.uid });
  const tokenData = await naverAuth.refreshAccessToken(user.naver_refresh_token);
  return updateStoredTokens(user.uid, tokenData);
}

async function loginWithStoredTokens(user) {
  const refreshedUser = await refreshNaverTokensIfNeeded(user);
  const profile = await naverAuth.fetchProfile(refreshedUser.naver_access_token);
  const mapped = naverAuth.mapProfileToUser(profile);
  const updatedUser = await upsertUser(mapped);
  const latestUser = await findUserByUid(updatedUser.uid);
  const sessionToken = issueSessionToken(latestUser);

  logger.log('NAVER', '저장된 토큰으로 자동 로그인 성공', {
    uid: latestUser.uid,
    email: latestUser.email,
  });

  return { user: latestUser, sessionToken };
}

async function refreshSession(sessionToken) {
  const user = await getUserFromSessionToken(sessionToken);
  return loginWithStoredTokens(user);
}

async function clearUserSession(uid) {
  await execute(
    `UPDATE users
     SET naver_access_token = NULL,
         naver_refresh_token = NULL,
         token_expires_at = NULL,
         session_version = session_version + 1
     WHERE uid = ?`,
    [uid],
  );
}

async function revokeStoredNaverAccessToken(user) {
  if (!user?.naver_access_token) {
    return { ok: false, skipped: true, message: '저장된 Naver access_token 없음' };
  }

  try {
    await naverAuth.revokeAccessToken(user.naver_access_token);
    logger.log('NAVER', 'Naver access_token 폐기 완료', { uid: user.uid });
    return { ok: true, skipped: false, message: 'Naver access_token 폐기 완료' };
  } catch (error) {
    logger.warn('NAVER', 'Naver access_token 폐기 실패', { uid: user.uid, error: error.message });
    return { ok: false, skipped: false, message: error.message };
  }
}

async function revokeAllStoredNaverTokens() {
  const rows = await query(
    `SELECT uid, naver_access_token
     FROM users
     WHERE naver_access_token IS NOT NULL
       AND naver_access_token != ''`,
  );

  const results = [];
  for (const row of rows) {
    const user = mapUserRow(row);
    const outcome = await revokeStoredNaverAccessToken(user);
    results.push({
      uid: row.uid,
      ...outcome,
    });
  }

  return {
    attempted: rows.length,
    revoked: results.filter((item) => item.ok).length,
    failed: results.filter((item) => !item.ok && !item.skipped).length,
    skipped: results.filter((item) => item.skipped).length,
    results,
  };
}

async function logoutUserSession(sessionToken) {
  const user = await getUserFromSessionToken(sessionToken);
  const revokeResult = await revokeStoredNaverAccessToken(user);
  await clearUserSession(user.uid);
  return { user, revokeResult };
}

async function resetUserForDev(uid) {
  const user = await findUserByUid(uid);
  if (!user) {
    throw new Error('사용자를 찾을 수 없습니다.');
  }

  const revokeResult = await revokeStoredNaverAccessToken(user);
  const deleteResult = await execute('DELETE FROM users WHERE uid = ?', [uid]);

  return {
    user,
    revokeResult,
    deleted: (deleteResult.affectedRows || 0) > 0,
  };
}

module.exports = {
  saveUserTokens,
  findUserByUid,
  mapUserRow,
  getUserFromSessionToken,
  refreshSession,
  clearUserSession,
  revokeStoredNaverAccessToken,
  revokeAllStoredNaverTokens,
  logoutUserSession,
  resetUserForDev,
  isAccessTokenExpired,
};
