const mysql = require('mysql2/promise');
const { config, close } = require('./dbconnect');

const CREATE_USERS_TABLE_SQL = `
  CREATE TABLE IF NOT EXISTS users (
    user_no     INT          NOT NULL AUTO_INCREMENT,
    sub         VARCHAR(50)  NOT NULL,
    email       VARCHAR(100) NOT NULL,
    name        VARCHAR(50)  NOT NULL,
    created_at  TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at  TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (user_no),
    UNIQUE KEY UK_users_sub (sub)
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

async function createUsersTable() {
  await ensureDatabase();

  const connection = await mysql.createConnection(config);
  try {
    await connection.query(CREATE_USERS_TABLE_SQL);
  } finally {
    await connection.end();
  }

  return {
    database: config.database,
    table: 'users',
  };
}

module.exports = {
  CREATE_USERS_TABLE_SQL,
  createUsersTable,
};

if (require.main === module) {
  createUsersTable()
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
