const { config, testConnection, query, close } = require('./dbconnect');

async function runTest(name, fn) {
  process.stdout.write(`[TEST] ${name} ... `);
  try {
    const result = await fn();
    console.log('OK');
    if (result !== undefined) {
      console.log('       ', result);
    }
    return true;
  } catch (err) {
    console.log('FAIL');
    console.error('       ', err.message);
    return false;
  }
}

async function main() {
  console.log('MySQL 연결 테스트');
  console.log('설정:', {
    host: config.host,
    port: config.port,
    user: config.user,
    database: config.database,
  });
  console.log('');

  const results = [];

  results.push(await runTest('연결(ping)', () => testConnection()));
  results.push(await runTest('현재 DB 확인', async () => {
    const rows = await query('SELECT DATABASE() AS db');
    return rows[0].db;
  }));
  results.push(await runTest('서버 시간', async () => {
    const rows = await query('SELECT NOW() AS now');
    return rows[0].now;
  }));

  await close();

  const passed = results.filter(Boolean).length;
  const total = results.length;

  console.log('');
  console.log(`완료: ${passed}/${total} 성공`);

  if (passed !== total) {
    process.exit(1);
  }
}

main().catch((err) => {
  console.error('실행 오류:', err.message);
  process.exit(1);
});
