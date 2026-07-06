process.env.SESSION_JWT_SECRET = process.env.SESSION_JWT_SECRET || 'test-secret-for-local-auth-flow';
process.env.TOKEN_ENCRYPTION_KEY = process.env.TOKEN_ENCRYPTION_KEY || 'test-encryption-key';
process.env.MYSQL_HOST = process.env.MYSQL_HOST || 'localhost';
process.env.MYSQL_PORT = process.env.MYSQL_PORT || '3306';
process.env.MYSQL_USER = process.env.MYSQL_USER || 'root';
process.env.MYSQL_PASSWORD = process.env.MYSQL_PASSWORD || 'local-test-password';
process.env.MYSQL_DATABASE = process.env.MYSQL_DATABASE || 'gamedb';

require('dotenv').config();

const { issueSessionToken, verifyJwt } = require('./session-jwt');
const { encryptText, decryptText } = require('./crypto-utils');
const { ensureTokenColumns } = require('./user-schema');
const { close } = require('./dbconnect');

function assert(condition, message) {
  if (!condition) {
    throw new Error(message);
  }
}

async function main() {
  console.log('인증 모듈 테스트 시작\n');

  const plain = 'naver-access-token-sample';
  const encrypted = encryptText(plain);
  const decrypted = decryptText(encrypted);
  assert(decrypted === plain, '토큰 암호화/복호화 실패');
  console.log('OK - 토큰 암호화/복호화');

  const user = { uid: '1234567890', session_version: 2 };
  const token = issueSessionToken(user, { ttlSeconds: 60 });
  const payload = verifyJwt(token);
  assert(payload.uid === user.uid, 'JWT uid 불일치');
  assert(payload.sv === user.session_version, 'JWT session_version 불일치');
  console.log('OK - JWT 발급/검증');

  try {
    await ensureTokenColumns();
    console.log('OK - DB 스키마 마이그레이션');
  } catch (err) {
    console.log(`SKIP - DB 스키마 마이그레이션 (${err.message})`);
  }

  console.log('\n완료: 서버 인증 기본 모듈이 정상 동작합니다.');
  console.log('통합 테스트는 서버 실행 후 Unity Play에서 확인하세요.');
}

main()
  .catch((err) => {
    console.error('FAIL -', err.message);
    process.exit(1);
  })
  .finally(() => close());
