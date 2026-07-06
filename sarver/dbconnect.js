require('dotenv').config();

const mysql = require('mysql2/promise');
const logger = require('./logger');

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`환경 변수 ${name}이(가) .env 파일에 없습니다.`);
  }
  return value;
}

const config = {
  host: requireEnv('MYSQL_HOST'),
  port: Number(requireEnv('MYSQL_PORT')),
  user: requireEnv('MYSQL_USER'),
  password: requireEnv('MYSQL_PASSWORD'),
  database: requireEnv('MYSQL_DATABASE'),
};

let pool;

function getPool() {
  if (!pool) {
    logger.log('DB', '연결 풀 생성', {
      host: config.host,
      port: config.port,
      user: config.user,
      database: config.database,
    });

    pool = mysql.createPool({
      ...config,
      waitForConnections: true,
      connectionLimit: 5,
    });
  }
  return pool;
}

async function query(sql, params = []) {
  const startedAt = Date.now();
  logger.log('DB', '쿼리 실행', { sql: sql.trim(), params });

  const [rows] = await getPool().query(sql, params);
  const elapsedMs = Date.now() - startedAt;

  logger.log('DB', '쿼리 완료', {
    rowCount: Array.isArray(rows) ? rows.length : 0,
    elapsedMs,
  });

  return rows;
}

async function execute(sql, params = []) {
  const startedAt = Date.now();
  logger.log('DB', '쿼리 실행', { sql: sql.trim(), params });

  const [result] = await getPool().execute(sql, params);
  const elapsedMs = Date.now() - startedAt;

  logger.log('DB', '쿼리 완료', {
    affectedRows: result.affectedRows,
    insertId: result.insertId,
    changedRows: result.changedRows,
    elapsedMs,
  });

  return result;
}

async function testConnection() {
  logger.log('DB', '연결 테스트 시작');
  const connection = await mysql.createConnection(config);
  try {
    await connection.ping();
    const [rows] = await connection.query('SELECT VERSION() AS version');
    logger.log('DB', '연결 테스트 성공', { version: rows[0].version });
    return rows[0].version;
  } finally {
    await connection.end();
  }
}

async function close() {
  if (pool) {
    logger.log('DB', '연결 풀 종료');
    await pool.end();
    pool = null;
  }
}

module.exports = {
  config,
  getPool,
  query,
  execute,
  testConnection,
  close,
};
