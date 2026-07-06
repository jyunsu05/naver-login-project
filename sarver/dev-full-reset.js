#!/usr/bin/env node
require('dotenv').config();

const fs = require('fs');
const { execute, close } = require('./dbconnect');
const localSessionBridge = require('./local-session-bridge');
const { clearNaverBrowserData } = require('./clear-naver-browser-data');
const { revokeAllStoredNaverTokens } = require('./user-tokens');

function getOutputPath() {
  const equalsArg = process.argv.find((item) => item.startsWith('--output='));
  if (equalsArg) {
    return equalsArg.slice('--output='.length);
  }

  const outputIndex = process.argv.indexOf('--output');
  if (outputIndex >= 0 && outputIndex + 1 < process.argv.length) {
    return process.argv[outputIndex + 1];
  }

  return null;
}

function writeResult(payload) {
  const text = JSON.stringify(payload, null, 2);
  const outputPath = getOutputPath();

  if (outputPath) {
    fs.writeFileSync(outputPath, text, 'utf8');
  }

  console.log(text);
}

async function deleteAllUsers() {
  const result = await execute('DELETE FROM users');
  return result.affectedRows || 0;
}

async function runDevFullReset(options = {}) {
  const steps = [];
  const closeBrowsersFirst = options.closeBrowsersFirst !== false;
  const ignoreBrowserLock = Boolean(options.ignoreBrowserLock);

  try {
    const naverRevokeResult = await revokeAllStoredNaverTokens();
    steps.push({
      step: 'naver-token-revoke',
      ok: naverRevokeResult.failed === 0,
      message: `Naver access_token 폐기 ${naverRevokeResult.revoked}/${naverRevokeResult.attempted}건 완료`,
      ...naverRevokeResult,
    });
  } catch (error) {
    steps.push({
      step: 'naver-token-revoke',
      ok: false,
      message: error.message,
    });
  }

  try {
    const deletedUsers = await deleteAllUsers();
    steps.push({
      step: 'database',
      ok: true,
      message: `users 테이블에서 ${deletedUsers}건 삭제`,
      deletedUsers,
    });
  } catch (error) {
    steps.push({
      step: 'database',
      ok: false,
      message: error.message,
    });
    throw error;
  }

  try {
    localSessionBridge.clearSession();
    steps.push({
      step: 'local-session-bridge',
      ok: true,
      message: `로컬 브리지 파일 삭제 (${localSessionBridge.getBridgePath()})`,
    });
  } catch (error) {
    steps.push({
      step: 'local-session-bridge',
      ok: false,
      message: error.message,
    });
  }

  let browserLogoutComplete = true;

  try {
    const browserResult = await clearNaverBrowserData({
      closeBrowsersFirst,
      ignoreBrowserLock,
    });
    browserLogoutComplete = Boolean(browserResult.browserLogoutComplete);
    steps.push({
      step: 'browser-logout',
      ok: browserResult.ok,
      message: browserResult.message,
      details: browserResult.results,
      deletedTotal: browserResult.deletedTotal || 0,
      browserLogoutComplete,
    });

    if (!browserResult.ok) {
      return {
        success: false,
        browserLogoutComplete: false,
        steps,
        message: ignoreBrowserLock
          ? '브라우저 네이버 로그아웃이 완료되지 않았습니다. Chrome/Edge를 닫고 다시 실행하세요.'
          : browserResult.message,
        needsBrowserClosed: true,
      };
    }
  } catch (error) {
    browserLogoutComplete = false;
    steps.push({
      step: 'browser-logout',
      ok: false,
      message: error.message,
      needsBrowserClosed: true,
    });

    return {
      success: false,
      browserLogoutComplete: false,
      steps,
      message: error.message,
      needsBrowserClosed: true,
    };
  }

  return {
    success: true,
    browserLogoutComplete,
    steps,
    message: '전체 초기화 완료: Unity/DB/브라우저 네이버 로그인이 모두 초기화되었습니다.',
  };
}

async function main() {
  const ignoreBrowserLock = process.argv.includes('--ignore-browser-lock');
  const closeBrowsersFirst = !process.argv.includes('--keep-browsers-open');
  let exitCode = 1;

  try {
    const result = await runDevFullReset({ ignoreBrowserLock, closeBrowsersFirst });
    writeResult(result);
    exitCode = result.success ? 0 : 1;
  } catch (error) {
    writeResult({
      success: false,
      browserLogoutComplete: false,
      message: error.message,
      steps: [],
    });
    exitCode = 1;
  } finally {
    try {
      await close();
    } catch {
      // ignore pool close errors
    }
  }

  process.exitCode = exitCode;
}

if (require.main === module) {
  main();
}

module.exports = {
  runDevFullReset,
  deleteAllUsers,
};
