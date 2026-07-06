const { config, close } = require('./dbconnect');
const { selectAllUsers } = require('./user-select');

async function main() {
  console.log('users 테이블 조회');
  console.log(`database: ${config.database}\n`);

  const users = await selectAllUsers();

  if (users.length === 0) {
    console.log('저장된 사용자가 없습니다.');
    return;
  }

  console.log(`총 ${users.length}건\n`);

  users.forEach((user, index) => {
    console.log(`[${index + 1}]`);
    console.log(`uid: ${user.uid}`);
    console.log(`email: ${user.email}`);
    console.log(`name: ${user.name}`);
    console.log(`id(DB PK): ${user.id}`);
    console.log(`created_at: ${user.created_at}`);
    console.log('');
  });
}

main()
  .catch((err) => {
    console.error('조회 실패:', err.message);
    process.exit(1);
  })
  .finally(() => close());
