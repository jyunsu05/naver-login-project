const { query, execute } = require('./dbconnect');
const logger = require('./logger');

const USER_TOKEN_COLUMNS = [
  'naver_access_token',
  'naver_refresh_token',
  'token_expires_at',
  'session_version',
];

async function ensureTokenColumns() {
  const columns = await query('SHOW COLUMNS FROM users');
  const names = new Set(columns.map((column) => column.Field));
  const alters = [];

  if (!names.has('uid') && names.has('sub')) {
    await execute('ALTER TABLE users CHANGE COLUMN sub uid VARCHAR(64) NOT NULL');
    names.delete('sub');
    names.add('uid');
    logger.log('USER', 'sub 컬럼을 uid로 변경');
  }

  if (names.has('access_token') && !names.has('naver_access_token')) {
    await execute('ALTER TABLE users CHANGE COLUMN access_token naver_access_token TEXT NULL');
    names.delete('access_token');
    names.add('naver_access_token');
    logger.log('USER', 'access_token 컬럼을 naver_access_token으로 변경');
  } else if (!names.has('naver_access_token')) {
    alters.push('ADD COLUMN naver_access_token TEXT NULL');
  }

  if (names.has('refresh_token') && !names.has('naver_refresh_token')) {
    await execute('ALTER TABLE users CHANGE COLUMN refresh_token naver_refresh_token TEXT NULL');
    names.delete('refresh_token');
    names.add('naver_refresh_token');
    logger.log('USER', 'refresh_token 컬럼을 naver_refresh_token으로 변경');
  } else if (!names.has('naver_refresh_token')) {
    alters.push('ADD COLUMN naver_refresh_token TEXT NULL');
  }

  if (!names.has('token_expires_at')) {
    alters.push('ADD COLUMN token_expires_at DATETIME NULL');
  }

  if (!names.has('session_version')) {
    alters.push('ADD COLUMN session_version INT NOT NULL DEFAULT 0');
  }

  if (names.has('session_token')) {
    const indexes = await query('SHOW INDEX FROM users');
    const hasSessionIndex = indexes.some((index) => index.Key_name === 'UK_users_session_token');
    if (hasSessionIndex) {
      await execute('ALTER TABLE users DROP INDEX UK_users_session_token');
    }
    await execute('ALTER TABLE users DROP COLUMN session_token');
    logger.log('USER', 'legacy session_token 컬럼 제거');
  }

  if (alters.length > 0) {
    await execute(`ALTER TABLE users ${alters.join(', ')}`);
    logger.log('USER', '토큰 컬럼 추가', { added: alters.length });
  }

  logger.log('USER', 'DB 스키마 마이그레이션 완료', { columns: USER_TOKEN_COLUMNS });
}

module.exports = {
  USER_TOKEN_COLUMNS,
  ensureTokenColumns,
};
