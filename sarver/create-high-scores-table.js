const mysql = require('mysql2/promise');
const { config, close } = require('./dbconnect');
const { CREATE_USER_HIGH_SCORES_TABLE_SQL } = require('./high-score-schema');

async function ensureDatabase() {
  const connection = await mysql.createConnection({
    host: config.host,
    port: config.port,
    user: config.user,
    password: config.password,
  });

  try {
    await connection.query(`CREATE DATABASE IF NOT EXISTS \`${config.database}\``);
  } finally {
    await connection.end();
  }
}

async function createHighScoresTable() {
  await ensureDatabase();

  const connection = await mysql.createConnection(config);
  try {
    await connection.query(CREATE_USER_HIGH_SCORES_TABLE_SQL);
  } finally {
    await connection.end();
  }

  return {
    database: config.database,
    table: 'user_high_scores',
  };
}

module.exports = {
  CREATE_USER_HIGH_SCORES_TABLE_SQL,
  createHighScoresTable,
};

if (require.main === module) {
  createHighScoresTable()
    .then(({ database, table }) => {
      console.log(`${table} 테이블 생성 완료`);
      console.log(`database: ${database}`);
    })
    .catch((err) => {
      console.error('테이블 생성 실패:', err.message);
      process.exit(1);
    })
    .finally(() => close());
}
