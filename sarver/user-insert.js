const { query, execute } = require('./dbconnect');
const logger = require('./logger');

async function upsertUser({ uid, email, name }) {
  logger.log('USER', 'DB 적재 시작', { uid, email, name });

  const existingRows = await query(
    `SELECT id, uid, email, name, created_at
     FROM users
     WHERE uid = ?`,
    [uid],
  );

  const isUpdate = existingRows.length > 0;
  if (isUpdate) {
    logger.log('USER', '기존 사용자 발견', existingRows[0]);
  } else {
    logger.log('USER', '신규 사용자 등록 예정', { uid });
  }

  const result = await execute(
    `INSERT INTO users (uid, email, name)
     VALUES (?, ?, ?)
     ON DUPLICATE KEY UPDATE
       email = VALUES(email),
       name = VALUES(name)`,
    [uid, email, name],
  );

  const action = isUpdate ? 'UPDATE' : 'INSERT';
  logger.log('USER', `DB 적재 완료 (${action})`, {
    affectedRows: result.affectedRows,
    insertId: result.insertId,
    changedRows: result.changedRows,
  });

  const rows = await query(
    `SELECT id, uid, email, name, created_at
     FROM users
     WHERE uid = ?`,
    [uid],
  );

  logger.log('USER', '저장 후 조회 결과', rows[0]);
  return rows[0];
}

module.exports = {
  upsertUser,
};
