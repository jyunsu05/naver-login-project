require('dotenv').config();

const express = require('express');
const path = require('path');
const fs = require('fs');
const { upsertUser } = require('./user-insert');
const { validateLoginBody, loginSuccess, loginError, formatUserOutput } = require('./login-response');
const naverAuth = require('./naver-auth');
const { saveUserTokens, getUserFromSessionToken, refreshSession, logoutUserSession, resetUserForDev } = require('./user-tokens');
const { ensureTokenColumns } = require('./user-schema');
const { ensureHighScoresTable, validateScoreBody, getHighScoreByUid, submitHighScore } = require('./high-scores');
const { extractSessionToken } = require('./session-jwt');
const localSessionBridge = require('./local-session-bridge');
const { hasServiceConsent, setServiceConsent, clearServiceConsent } = require('./service-consent');
const logger = require('./logger');
const { config, testConnection } = require('./dbconnect');

const app = express();
const PORT = 3000;
const HOST = '127.0.0.1';

function getUnityCallbackUrl() {
  return process.env.UNITY_CALLBACK_URL || 'http://127.0.0.1:7777/naver-login/';
}

function redirectToUnity(res, params) {
  const url = new URL(getUnityCallbackUrl());
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      url.searchParams.set(key, String(value));
    }
  });

  res.redirect(url.toString());
}

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

async function requireSessionUser(req) {
  const sessionToken = extractSessionToken(req);

  if (!sessionToken) {
    return { error: loginError('sessionToken이 필요합니다.', 400) };
  }

  try {
    const user = await getUserFromSessionToken(sessionToken);
    return { user };
  } catch (err) {
    return { error: loginError(err.message, 401) };
  }
}

function scoreSuccess(message, data) {
  return {
    success: true,
    message,
    data,
  };
}

function isLocalRequest(req) {
  const ip = String(req.ip || req.socket?.remoteAddress || '').replace('::ffff:', '');
  return ip === '127.0.0.1' || ip === '::1' || ip === 'localhost';
}

async function resolveBridgedSession() {
  const bridged = localSessionBridge.loadSession();
  if (!bridged?.sessionToken) {
    return null;
  }

  const user = await getUserFromSessionToken(bridged.sessionToken);
  return {
    user,
    sessionToken: bridged.sessionToken,
  };
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

app.get('/login/consent', (req, res) => {
  if (hasServiceConsent(req)) {
    logger.log('APP', '서비스 동의 완료 → /auth/naver 리다이렉트');
    return res.redirect('/auth/naver');
  }

  logger.log('APP', 'login-consent.html 전송');
  return sendHtml(res, 'login-consent.html');
});

app.get('/login/consent/accept', (req, res) => {
  setServiceConsent(res);
  logger.log('APP', '서비스 이용 동의 완료 → /auth/naver 리다이렉트');
  return res.redirect('/auth/naver');
});

app.get('/login/consent/revoke', (req, res) => {
  clearServiceConsent(res);
  logger.log('APP', '서비스 이용 동의 쿠키 삭제');
  return res.redirect('/login/consent');
});

app.get('/login/success', async (req, res) => {
  if (!isLocalRequest(req)) {
    const error = loginError('로컬에서만 사용할 수 있습니다.', 403);
    return res.status(error.statusCode).json(error.body);
  }

  try {
    const bridged = await resolveBridgedSession();
    if (!bridged) {
      const error = loginError('브라우저 로그인 세션이 없습니다.', 404);
      return sendLoginResultPage(res, error.body, error.statusCode);
    }

    const response = loginSuccess(bridged.user, {
      reused: true,
      sessionToken: bridged.sessionToken,
    });
    logger.log('APP', '/login/success 페이지 전송', { uid: bridged.user.uid });
    return sendLoginResultPage(res, response);
  } catch (err) {
    localSessionBridge.clearSession();
    const error = loginError(err.message, 401);
    return sendLoginResultPage(res, error.body, error.statusCode);
  }
});

app.get('/auth/dev/session', async (req, res) => {
  if (!isLocalRequest(req)) {
    const error = loginError('로컬에서만 사용할 수 있습니다.', 403);
    return res.status(error.statusCode).json(error.body);
  }

  try {
    const bridged = await resolveBridgedSession();
    if (!bridged) {
      const error = loginError('브라우저 로그인 세션이 없습니다.', 404);
      return res.status(error.statusCode).json(error.body);
    }

    const response = loginSuccess(bridged.user, {
      reused: true,
      sessionToken: bridged.sessionToken,
    });
    logger.log('NAVER', '/auth/dev/session 성공', { uid: bridged.user.uid });
    return res.status(200).json(response);
  } catch (err) {
    localSessionBridge.clearSession();
    const error = loginError(err.message, 401);
    return res.status(error.statusCode).json(error.body);
  }
});

app.delete('/auth/dev/session', (req, res) => {
  if (!isLocalRequest(req)) {
    const error = loginError('로컬에서만 사용할 수 있습니다.', 403);
    return res.status(error.statusCode).json(error.body);
  }

  localSessionBridge.clearSession();
  logger.log('NAVER', '/auth/dev/session 삭제');
  return res.status(200).json({
    success: true,
    message: '로컬 브라우저 세션 브리지 삭제 완료',
    data: null,
  });
});

app.post('/auth/dev/full-reset', async (req, res) => {
  if (!isLocalRequest(req)) {
    const error = loginError('로컬에서만 사용할 수 있습니다.', 403);
    return res.status(error.statusCode).json(error.body);
  }

  try {
    const { runDevFullReset } = require('./dev-full-reset');
    const ignoreBrowserLock = Boolean(req.body?.ignoreBrowserLock);
    const closeBrowsersFirst = req.body?.closeBrowsersFirst !== false;
    const result = await runDevFullReset({ ignoreBrowserLock, closeBrowsersFirst });
    logger.log('NAVER', '/auth/dev/full-reset', { success: result.success });
    return res.status(result.success ? 200 : 409).json(result);
  } catch (err) {
    logger.error('NAVER', '/auth/dev/full-reset 실패', err.message);
    const error = loginError(err.message, 500);
    return res.status(error.statusCode).json({
      success: false,
      message: error.body.message,
      steps: [],
    });
  }
});

async function handleDevSessionReset(req, res) {
  if (!isLocalRequest(req)) {
    const error = loginError('로컬에서만 사용할 수 있습니다.', 403);
    return res.status(error.statusCode).json(error.body);
  }

  const sessionToken = extractSessionToken(req) || req.body?.sessionToken;
  if (!sessionToken) {
    const error = loginError('sessionToken이 필요합니다.', 400);
    return res.status(error.statusCode).json(error.body);
  }

  try {
    const { user, revokeResult, deleted } = await resetUserForDev(
      (await getUserFromSessionToken(sessionToken)).uid,
    );
    localSessionBridge.clearSession();
    logger.log('NAVER', '개발용 유저 초기화', { uid: user.uid, deleted, revokeOk: revokeResult.ok });

    return res.status(200).json({
      success: true,
      message: '개발용 유저 초기화 완료 (Naver 토큰 폐기 + DB 삭제)',
      data: {
        uid: user.uid,
        deleted,
        naverTokenRevoked: revokeResult.ok,
        naverTokenRevokeMessage: revokeResult.message,
      },
    });
  } catch (err) {
    logger.error('NAVER', '개발용 유저 초기화 실패', err.message);
    const error = loginError(err.message, err.message === '사용자를 찾을 수 없습니다.' ? 404 : 401);
    return res.status(error.statusCode).json(error.body);
  }
}

app.post('/auth/dev/reset', handleDevSessionReset);
app.post('/debug/reset', handleDevSessionReset);

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
  if (!hasServiceConsent(req)) {
    logger.log('NAVER', '서비스 동의 없음 → /login/consent 리다이렉트');
    return res.redirect('/login/consent');
  }

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
  const useUnityRedirect = req.query.delivery === 'unity';

  if (error) {
    logger.warn('NAVER', '네이버 인증 거부', { error, errorDescription });
    if (useUnityRedirect) {
      return redirectToUnity(res, { error: errorDescription || error });
    }
    const loginErr = loginError(errorDescription || error, 400);
    return sendLoginResultPage(res, loginErr.body, loginErr.statusCode);
  }

  if (!code || !state) {
    if (useUnityRedirect) {
      return redirectToUnity(res, { error: 'code 또는 state가 없습니다.' });
    }
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

    const { user: savedUser, sessionToken } = await saveUserTokens(user.uid, {
      accessToken: tokenData.access_token,
      refreshToken: tokenData.refresh_token,
      expiresIn: tokenData.expires_in,
    });

    const response = loginSuccess(savedUser, { sessionToken });
    logger.log('NAVER', '사용자 정보\n' + formatUserOutput(savedUser));
    localSessionBridge.saveSession({ sessionToken, uid: savedUser.uid });

    if (req.query.format === 'json') {
      return res.status(200).json(response);
    }

    if (useUnityRedirect) {
      logger.log('NAVER', 'Unity 콜백(7777)으로 sessionToken 전달');
      return redirectToUnity(res, { token: sessionToken });
    }

    logger.log('NAVER', 'WebView용 login-success.html로 sessionToken 전달');
    return sendLoginResultPage(res, response);
  } catch (err) {
    logger.error('NAVER', '네이버 로그인 콜백 실패', err.message);
    if (useUnityRedirect) {
      return redirectToUnity(res, { error: err.message });
    }
    const loginErr = loginError(err.message, 500);
    return sendLoginResultPage(res, loginErr.body, loginErr.statusCode);
  }
});

app.post('/auth/me', async (req, res) => {
  const sessionToken = extractSessionToken(req);

  if (!sessionToken) {
    const error = loginError('sessionToken이 필요합니다.', 400);
    return res.status(error.statusCode).json(error.body);
  }

  try {
    const user = await getUserFromSessionToken(sessionToken);
    const response = loginSuccess(user, { reused: true, sessionToken });
    logger.log('NAVER', '/auth/me 성공', { uid: user.uid });
    return res.status(200).json(response);
  } catch (err) {
    logger.error('NAVER', '/auth/me 실패', err.message);
    const error = loginError(err.message, 401);
    return res.status(error.statusCode).json(error.body);
  }
});

app.post('/auth/refresh', async (req, res) => {
  const sessionToken = extractSessionToken(req);

  if (!sessionToken) {
    const error = loginError('sessionToken이 필요합니다.', 400);
    return res.status(error.statusCode).json(error.body);
  }

  try {
    const { user, sessionToken: refreshedToken } = await refreshSession(sessionToken);
    const response = loginSuccess(user, { reused: true, sessionToken: refreshedToken });
    logger.log('NAVER', '/auth/refresh 성공', { uid: user.uid });
    return res.status(200).json(response);
  } catch (err) {
    logger.error('NAVER', '/auth/refresh 실패', err.message);
    const error = loginError(err.message, 401);
    return res.status(error.statusCode).json(error.body);
  }
});

app.post('/auth/logout', async (req, res) => {
  const sessionToken = extractSessionToken(req);

  if (!sessionToken) {
    const error = loginError('sessionToken이 필요합니다.', 400);
    return res.status(error.statusCode).json(error.body);
  }

  try {
    const { user, revokeResult } = await logoutUserSession(sessionToken);
    localSessionBridge.clearSession();
    logger.log('NAVER', '세션 로그아웃 완료', {
      uid: user.uid,
      naverTokenRevoked: revokeResult.ok,
    });

    return res.status(200).json({
      success: true,
      message: '로그아웃 완료',
      data: {
        uid: user.uid,
        naverTokenRevoked: revokeResult.ok,
        naverTokenRevokeMessage: revokeResult.message,
      },
    });
  } catch (err) {
    logger.error('NAVER', '로그아웃 실패', err.message);
    const error = loginError(err.message, 401);
    return res.status(error.statusCode).json(error.body);
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

app.get('/scores/high', async (req, res) => {
  logger.log('SCORE', '최고 점수 조회 요청');

  const auth = await requireSessionUser(req);
  if (auth.error) {
    logger.warn('SCORE', '최고 점수 조회 인증 실패', { message: auth.error.body.message });
    return res.status(auth.error.statusCode).json(auth.error.body);
  }

  try {
    const data = await getHighScoreByUid(auth.user.uid);
    const response = scoreSuccess('최고 점수 조회 성공', data);
    logger.log('SCORE', '최고 점수 조회 성공', { uid: auth.user.uid, ...data });
    return res.status(200).json(response);
  } catch (err) {
    logger.error('SCORE', '최고 점수 조회 실패', err.message);
    const error = loginError('최고 점수 조회 실패', 500);
    return res.status(error.statusCode).json(error.body);
  }
});

app.post('/scores/high', async (req, res) => {
  logger.log('SCORE', '최고 점수 저장 요청');

  const auth = await requireSessionUser(req);
  if (auth.error) {
    logger.warn('SCORE', '최고 점수 저장 인증 실패', { message: auth.error.body.message });
    return res.status(auth.error.statusCode).json(auth.error.body);
  }

  const validation = validateScoreBody(req.body);
  if (!validation.ok) {
    logger.warn('SCORE', '최고 점수 저장 검증 실패', { message: validation.message, body: req.body });
    const error = loginError(validation.message, 400);
    return res.status(error.statusCode).json(error.body);
  }

  try {
    const data = await submitHighScore(auth.user.uid, validation.data.score);
    const response = scoreSuccess('최고 점수 저장 성공', data);
    logger.log('SCORE', '최고 점수 저장 성공', { uid: auth.user.uid, ...data });
    return res.status(200).json(response);
  } catch (err) {
    logger.error('SCORE', '최고 점수 저장 실패', err.message);
    const error = loginError('최고 점수 저장 실패', 500);
    return res.status(error.statusCode).json(error.body);
  }
});

app.listen(PORT, HOST, async () => {
  logger.log('SERVER', `서버 시작 http://${HOST}:${PORT}`);
  logger.log('SERVER', '네이버 로그인', {
    callbackUrl: process.env.NAVER_CALLBACK_URL,
    loginUrl: `http://${HOST}:${PORT}/login`,
    unityCallbackUrl: getUnityCallbackUrl(),
    localSessionBridge: localSessionBridge.getBridgePath(),
  });
  logger.log('SERVER', 'DB 설정', {
    host: config.host,
    port: config.port,
    database: config.database,
  });

  try {
    await ensureTokenColumns();
    await ensureHighScoresTable();
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
