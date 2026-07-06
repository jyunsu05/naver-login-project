require('dotenv').config();

const express = require('express');
const path = require('path');
const fs = require('fs');
const { upsertUser } = require('./user-insert');
const { validateLoginBody, loginSuccess, loginError, formatUserOutput } = require('./login-response');
const naverAuth = require('./naver-auth');
const logger = require('./logger');
const { config, testConnection } = require('./dbconnect');

const app = express();
const PORT = 3000;
const HOST = '127.0.0.1';

function sendHtml(res, fileName, options = {}) {
  res.set({
    'Cache-Control': 'no-store',
    ...options.headers,
  });
  return res.sendFile(path.join(__dirname, fileName));
}

function sendLoginResultPage(res, payload, statusCode = 200) {
  const template = fs.readFileSync(path.join(__dirname, 'login-success.html'), 'utf8');
  const html = template.replace('__PAYLOAD__', JSON.stringify(payload));
  res.status(statusCode);
  res.set('Cache-Control', 'no-store');
  res.type('html').send(html);
}

app.use(express.json());

app.use((req, res, next) => {
  const startedAt = Date.now();

  logger.log('HTTP', '요청 수신', {
    method: req.method,
    url: req.url,
    contentType: req.headers['content-type'] || null,
    userAgent: req.headers['user-agent'] || null,
  });

  if (req.method === 'POST' && req.body && Object.keys(req.body).length > 0) {
    logger.log('HTTP', '요청 body', req.body);
  }

  res.on('finish', () => {
    logger.log('HTTP', '응답 완료', {
      method: req.method,
      url: req.url,
      statusCode: res.statusCode,
      elapsedMs: Date.now() - startedAt,
    });
  });

  next();
});

app.get('/', (req, res) => {
  logger.log('APP', '루트 → 네이버 로그인 리다이렉트');
  return res.redirect('/auth/naver');
});

app.get('/login', (req, res) => {
  if (req.query.preview === '1') {
    logger.log('APP', 'login.html 미리보기 전송');
    return sendHtml(res, 'login.html');
  }

  logger.log('APP', '/login → /auth/naver 리다이렉트');
  return res.redirect('/auth/naver');
});

app.get('/login/dev', (req, res) => {
  logger.log('APP', 'login-dev.html 전송');
  return sendHtml(res, 'login-dev.html');
});

app.get('/auth/naver/setup', (req, res) => {
  const { callbackUrl } = naverAuth.getConfig();
  const serviceUrl = process.env.NAVER_SERVICE_URL || 'http://127.0.0.1:3000';
  const html = fs.readFileSync(path.join(__dirname, 'naver-setup.html'), 'utf8')
    .replace('__SERVICE_URL__', serviceUrl)
    .replace('__CALLBACK_URL__', callbackUrl);
  res.set('Cache-Control', 'no-store');
  return res.type('html').send(html);
});

app.get('/auth/naver', (req, res) => {
  try {
    const { callbackUrl } = naverAuth.getConfig();
    const authorizeUrl = naverAuth.buildAuthorizeUrl();
    logger.log('NAVER', '네이버 로그인 리다이렉트', { callbackUrl, authorizeUrl });
    return res.redirect(authorizeUrl);
  } catch (err) {
    logger.error('NAVER', '네이버 로그인 시작 실패', err.message);
    const error = loginError(err.message, 500);
    return res.status(error.statusCode).json(error.body);
  }
});

app.get('/auth/naver/callback', async (req, res) => {
  const { code, state, error, error_description: errorDescription } = req.query;

  if (error) {
    logger.warn('NAVER', '네이버 인증 거부', { error, errorDescription });
    const loginErr = loginError(errorDescription || error, 400);
    return sendLoginResultPage(res, loginErr.body, loginErr.statusCode);
  }

  if (!code || !state) {
    const loginErr = loginError('code 또는 state가 없습니다.', 400);
    return sendLoginResultPage(res, loginErr.body, loginErr.statusCode);
  }

  try {
    const tokenData = await naverAuth.exchangeCodeForToken(code, state);
    const profile = await naverAuth.fetchProfile(tokenData.access_token);
    const mapped = naverAuth.mapProfileToUser(profile);

    logger.log('NAVER', '프로필 조회 성공', mapped);

    const user = await upsertUser({
      uid: mapped.uid,
      email: mapped.email,
      name: mapped.name,
    });

    const response = loginSuccess(user);
    logger.log('NAVER', '사용자 정보\n' + formatUserOutput(user));
    logger.log('NAVER', '응답 JSON', response);
    return sendLoginResultPage(res, response);
  } catch (err) {
    logger.error('NAVER', '네이버 로그인 콜백 실패', err.message);
    const loginErr = loginError(err.message, 500);
    return sendLoginResultPage(res, loginErr.body, loginErr.statusCode);
  }
});

app.post('/login', async (req, res) => {
  logger.log('LOGIN', '로그인 요청 처리 시작');

  const validation = validateLoginBody(req.body);
  if (!validation.ok) {
    logger.warn('LOGIN', '요청 검증 실패', { message: validation.message, body: req.body });
    const error = loginError(validation.message, 400);
    return res.status(error.statusCode).json(error.body);
  }

  const { uid, email, name } = validation.data;
  logger.log('LOGIN', '요청 검증 성공', { uid, email, name });

  try {
    const user = await upsertUser({ uid, email, name });
    const response = loginSuccess(user);

    logger.log('LOGIN', '로그인 성공 응답', response);
    return res.status(200).json(response);
  } catch (err) {
    logger.error('LOGIN', '로그인 처리 실패', err.message);
    const error = loginError('DB 저장 실패', 500);
    return res.status(error.statusCode).json(error.body);
  }
});

app.listen(PORT, HOST, async () => {
  logger.log('SERVER', `서버 시작 http://${HOST}:${PORT}`);
  logger.log('SERVER', '네이버 로그인', {
    callbackUrl: process.env.NAVER_CALLBACK_URL,
    loginUrl: `http://${HOST}:${PORT}/login`,
  });
  logger.log('SERVER', 'DB 설정', {
    host: config.host,
    port: config.port,
    database: config.database,
  });

  try {
    const version = await testConnection();
    logger.log('SERVER', 'DB 연결 확인 완료', { version });
  } catch (err) {
    logger.error('SERVER', 'DB 연결 실패', err.message);
  }

  logger.log('SERVER', '요청 대기 중... (브라우저/Postman 요청 시 아래에 로그가 출력됩니다)');
}).on('error', (err) => {
  if (err.code === 'EADDRINUSE') {
    logger.error('SERVER', `포트 ${PORT}이(가) 이미 사용 중입니다.`);
    logger.error('SERVER', '기존 서버를 종료한 뒤 다시 실행하세요. (Ctrl+C 또는 작업 관리자에서 node 종료)');
    process.exit(1);
  }

  logger.error('SERVER', '서버 시작 실패', err.message);
  process.exit(1);
});
