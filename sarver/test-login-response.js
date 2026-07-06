const {
  validateLoginBody,
  loginSuccess,
  loginError,
} = require('./login-response');

function runValidationTest(name, body, expectOk) {
  const result = validateLoginBody(body);
  const passed = result.ok === expectOk;
  console.log(`${passed ? 'OK' : 'FAIL'} - ${name}`);
  if (!passed) {
    console.log('       ', result);
  }
  return passed;
}

function main() {
  console.log('로그인 응답 형식 테스트\n');

  let passed = 0;
  let total = 0;

  const cases = [
    ['정상 body', { sub: '1', email: 'a@b.com', name: '홍길동' }, true],
    ['빈 body', {}, false],
    ['sub 누락', { email: 'a@b.com', name: '홍길동' }, false],
    ['공백만', { sub: '  ', email: 'a@b.com', name: '홍길동' }, false],
  ];

  for (const [name, body, expectOk] of cases) {
    total += 1;
    if (runValidationTest(name, body, expectOk)) passed += 1;
  }

  const success = loginSuccess({
    user_no: 1,
    sub: '123456789012345678901',
    email: 'hong@gmail.com',
    name: '홍길동',
  });

  total += 1;
  const successOk = success.success === true
    && success.message === '로그인 성공'
    && success.data.user.sub === '123456789012345678901';
  console.log(`${successOk ? 'OK' : 'FAIL'} - 성공 응답 형식`);
  if (successOk) {
    console.log('       ', JSON.stringify(success, null, 2).split('\n').join('\n        '));
    passed += 1;
  }

  const failure = loginError('sub, email, name이 필요합니다.', 400);
  total += 1;
  const failureOk = failure.statusCode === 400
    && failure.body.success === false
    && failure.body.data === null;
  console.log(`${failureOk ? 'OK' : 'FAIL'} - 실패 응답 형식`);
  if (failureOk) passed += 1;

  console.log(`\n완료: ${passed}/${total} 성공`);
  if (passed !== total) process.exit(1);
}

main();
