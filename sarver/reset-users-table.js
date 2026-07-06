const mysql = require('mysql2/promise');
const { config, close } = require('./dbconnect');

const DROP_USERS_TABLE_SQL = 'DROP TABLE IF EXISTS users';

const CREATE_USERS_TABLE_SQL = `
  CREATE TABLE users (
    id         INT          NOT NULL AUTO_INCREMENT,
    uid        VARCHAR(64)  NOT NULL,
    email      VARCHAR(100) NOT NULL,
    name       VARCHAR(50)  NOT NULL,
    created_at TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (id),
    UNIQUE KEY UK_users_uid (uid)
  )
`;

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

async function resetUsersTable() {
  await ensureDatabase();

  const connection = await mysql.createConnection(config);
  try {
    await connection.query(DROP_USERS_TABLE_SQL);
    await connection.query(CREATE_USERS_TABLE_SQL);
  } finally {
    await connection.end();
  }

  return {
    database: config.database,
    table: 'users',
    columns: ['id', 'uid', 'email', 'name', 'created_at'],
  };
}

module.exports = {
  DROP_USERS_TABLE_SQL,
  CREATE_USERS_TABLE_SQL,
  resetUsersTable,
};

if (require.main === module) {
  resetUsersTable()
    .then(({ database, table, columns }) => {
      console.log(`${table} 테이블 삭제 후 재생성 완료`);
      console.log(`database: ${database}`);
      console.log(`columns: ${columns.join(', ')}`);
    })
    .catch((err) => {
      console.error('테이블 재생성 실패:', err.message);
      process.exit(1);
    })
    .finally(() => close());
}
