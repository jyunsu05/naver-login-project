require('dotenv').config();

const { ensureTokenColumns } = require('./user-schema');
const { testConnection } = require('./dbconnect');

async function main() {
  console.log('DB 스키마 마이그레이션 테스트\n');

  const version = await testConnection();
  console.log('OK - DB 연결:', version);

  await ensureTokenColumns();
  console.log('OK - ensureTokenColumns 완료');

  console.log('\n완료: Issue #1 DB 스키마 마이그레이션 검증');
}

main().catch((err) => {
  console.error('FAIL -', err.message);
  process.exit(1);
});
