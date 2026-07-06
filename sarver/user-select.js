const { query } = require('./dbconnect');
const logger = require('./logger');

async function selectAllUsers() {
  logger.log('USER', '전체 사용자 조회');
  const rows = await query(
    `SELECT id, uid, email, name, created_at
     FROM users
     ORDER BY id`,
  );
  logger.log('USER', `전체 사용자 ${rows.length}건 조회 완료`);
  return rows;
}

async function selectUserByUid(uid) {
  logger.log('USER', 'uid로 사용자 조회', { uid });
  const rows = await query(
    `SELECT id, uid, email, name, created_at
     FROM users
     WHERE uid = ?`,
    [uid],
  );

  if (rows[0]) {
    logger.log('USER', '사용자 조회 성공', rows[0]);
  } else {
    logger.warn('USER', '사용자 조회 결과 없음', { uid });
  }

  return rows[0] || null;
}

module.exports = {
  selectAllUsers,
  selectUserByUid,
};
