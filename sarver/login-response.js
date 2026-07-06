function validateLoginBody(body) {
  if (!body || typeof body !== 'object') {
    return { ok: false, message: 'JSON body가 필요합니다.' };
  }

  const uid = String(body.uid ?? body.sub ?? '').trim();
  const email = String(body.email ?? '').trim();
  const name = String(body.name ?? '').trim();

  if (!uid || !email || !name) {
    return { ok: false, message: 'uid, email, name이 필요합니다.' };
  }

  return {
    ok: true,
    data: { uid, email, name },
  };
}

function toUserDto(user) {
  return {
    id: user.id,
    uid: user.uid,
    email: user.email,
    name: user.name,
    createdAt: user.created_at,
  };
}

function loginSuccess(user, options = {}) {
  const reused = Boolean(options.reused);
  const response = {
    success: true,
    message: reused ? '자동 로그인 성공' : '로그인 성공',
    data: {
      user: toUserDto(user),
      loginType: reused ? 'session' : 'naver',
    },
  };

  if (options.sessionToken) {
    response.data.sessionToken = options.sessionToken;
  }

  return response;
}

function formatUserOutput(user) {
  return `uid: ${user.uid}\nemail: ${user.email}\nname: ${user.name}`;
}

function loginError(message, statusCode = 400) {
  return {
    statusCode,
    body: {
      success: false,
      message,
      data: null,
    },
  };
}

module.exports = {
  validateLoginBody,
  loginSuccess,
  loginError,
  toUserDto,
  formatUserOutput,
};
