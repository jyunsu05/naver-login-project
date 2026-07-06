const { upsertUser } = require('./user-insert');
const { query, close } = require('./dbconnect');

async function main() {
  const sample = {
    sub: '123456789012345678901',
    email: 'hong@gmail.com',
    name: '홍길동',
  };

  console.log('사용자 DB 적재 테스트');
  console.log('입력:', sample);

  const user = await upsertUser(sample);

  console.log('저장 결과:', user);

  const rows = await query('SELECT * FROM users WHERE sub = ?', [sample.sub]);
  console.log('DB 조회:', rows[0]);

  await close();
}

main().catch((err) => {
  console.error('테스트 실패:', err.message);
  process.exit(1);
});
