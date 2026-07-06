const { query, execute } = require('./dbconnect');
const { encryptText, decryptText } = require('./crypto-utils');
const { issueSessionToken } = require('./session-jwt');
const logger = require('./logger');

const USER_COLUMNS = `
  id, uid, email, name, created_at,
  naver_access_token, naver_refresh_token, token_expires_at, session_version
`;

function computeExpiresAt(expiresIn) {
  const seconds = Number(expiresIn) > 0 ? Number(expiresIn) : 3600;
  return new Date(Date.now() + seconds * 1000);
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

module.exports = {
  saveUserTokens,
  findUserByUid,
  mapUserRow,
};
