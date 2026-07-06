const fs = require('fs');
const os = require('os');
const path = require('path');
const initSqlJs = require('sql.js');
const { closeBrowsersAndWait } = require('./close-browsers');

const NAVER_NAME_PATTERN = /naver|nid\.naver/i;
const COOKIE_HOST_SQL = `
  host_key LIKE '%naver%'
  OR host_key LIKE '%nid%'
  OR host_key LIKE '%127.0.0.1%'
  OR host_key LIKE '%localhost%'
`;

function getBrowserProfiles() {
  const localAppData = process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local');
  const roots = [
    { browser: 'Chrome', root: path.join(localAppData, 'Google', 'Chrome', 'User Data') },
    { browser: 'Edge', root: path.join(localAppData, 'Microsoft', 'Edge', 'User Data') },
  ];

  const profiles = [];

  for (const { browser, root } of roots) {
    if (!fs.existsSync(root)) {
      continue;
    }

    const profileNames = ['Default'];
    for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
      if (entry.isDirectory() && /^Profile /.test(entry.name)) {
        profileNames.push(entry.name);
      }
    }

    for (const profileName of profileNames) {
      profiles.push({
        browser,
        profileName,
        profileDir: path.join(root, profileName),
        cookiePath: path.join(root, profileName, 'Network', 'Cookies'),
      });
    }
  }

  return profiles;
}

function deleteMatchingStorageFiles(profileDir) {
  const storageRoots = [
    path.join(profileDir, 'Session Storage'),
    path.join(profileDir, 'Local Storage'),
    path.join(profileDir, 'IndexedDB'),
    path.join(profileDir, 'Service Worker'),
  ];

  const deletedFiles = [];

  for (const storageRoot of storageRoots) {
    if (!fs.existsSync(storageRoot)) {
      continue;
    }

    const stack = [storageRoot];
    while (stack.length > 0) {
      const current = stack.pop();
      let entries;
      try {
        entries = fs.readdirSync(current, { withFileTypes: true });
      } catch {
        continue;
      }

      for (const entry of entries) {
        const fullPath = path.join(current, entry.name);
        if (entry.isDirectory()) {
          if (NAVER_NAME_PATTERN.test(entry.name)) {
            try {
              fs.rmSync(fullPath, { recursive: true, force: true });
              deletedFiles.push(fullPath);
            } catch (error) {
              deletedFiles.push({ path: fullPath, error: error.message });
            }
          } else {
            stack.push(fullPath);
          }
          continue;
        }

        if (!NAVER_NAME_PATTERN.test(entry.name)) {
          continue;
        }

        try {
          fs.unlinkSync(fullPath);
          deletedFiles.push(fullPath);
        } catch (error) {
          deletedFiles.push({ path: fullPath, error: error.message });
        }
      }
    }
  }

  return deletedFiles;
}

async function clearCookiesInFile(cookiePath) {
  const SQL = await initSqlJs();
  const tempPath = `${cookiePath}.naver-reset-${process.pid}.tmp`;

  try {
    fs.copyFileSync(cookiePath, tempPath);
  } catch (error) {
    error.message = `쿠키 파일 복사 실패: ${error.message}`;
    throw error;
  }

  try {
    const fileBuffer = fs.readFileSync(tempPath);
    const database = new SQL.Database(fileBuffer);

    const countResult = database.exec(
      `SELECT COUNT(*) AS count FROM cookies WHERE ${COOKIE_HOST_SQL}`,
    );
    const beforeCount = countResult[0]?.values?.[0]?.[0] || 0;

    if (beforeCount === 0) {
      database.close();
      return { deleted: 0, skipped: true };
    }

    database.run(`DELETE FROM cookies WHERE ${COOKIE_HOST_SQL}`);
    const exported = Buffer.from(database.export());
    database.close();

    fs.writeFileSync(tempPath, exported);
    fs.copyFileSync(tempPath, cookiePath);
    return { deleted: beforeCount, skipped: false };
  } finally {
    try {
      if (fs.existsSync(tempPath)) {
        fs.unlinkSync(tempPath);
      }
    } catch {
      // ignore temp cleanup errors
    }
  }
}

async function clearNaverBrowserData(options = {}) {
  const results = [];
  const closeBrowsersFirst = options.closeBrowsersFirst !== false;

  if (closeBrowsersFirst && process.platform === 'win32') {
    const closeResult = await closeBrowsersAndWait({ waitMs: options.browserCloseWaitMs });
    results.push({
      type: 'close-browsers',
      ok: true,
      message: closeResult.message,
      closed: closeResult.closed,
    });
  }

  const profiles = getBrowserProfiles();
  if (profiles.length === 0) {
    return {
      ok: true,
      message: 'Chrome/Edge 프로필을 찾지 못했습니다.',
      results,
      deletedTotal: 0,
      browserLogoutComplete: true,
    };
  }

  for (const profile of profiles) {
    const label = `${profile.browser}/${profile.profileName}`;

    if (fs.existsSync(profile.cookiePath)) {
      try {
        const outcome = await clearCookiesInFile(profile.cookiePath);
        results.push({
          browser: profile.browser,
          profile: profile.profileName,
          type: 'cookies',
          path: profile.cookiePath,
          deleted: outcome.deleted,
          ok: true,
        });
      } catch (error) {
        const locked = /EBUSY|EPERM|locked|being used/i.test(error.message);
        results.push({
          browser: profile.browser,
          profile: profile.profileName,
          type: 'cookies',
          path: profile.cookiePath,
          ok: false,
          error: error.message,
          needsBrowserClosed: locked,
        });

        if (!options.ignoreBrowserLock) {
          throw new Error(
            `${label} 쿠키 삭제 실패. Chrome/Edge를 모두 닫은 뒤 다시 시도하세요.\n(${error.message})`,
          );
        }
      }
    }

    try {
      const deletedFiles = deleteMatchingStorageFiles(profile.profileDir);
      results.push({
        browser: profile.browser,
        profile: profile.profileName,
        type: 'storage-files',
        deleted: deletedFiles.length,
        ok: true,
        paths: deletedFiles.slice(0, 20),
      });
    } catch (error) {
      results.push({
        browser: profile.browser,
        profile: profile.profileName,
        type: 'storage-files',
        ok: false,
        error: error.message,
      });
    }
  }

  const deletedTotal = results.reduce((sum, item) => sum + (item.deleted || 0), 0);
  const cookieFailures = results.filter((item) => item.type === 'cookies' && item.ok === false);

  const browserLogoutComplete = cookieFailures.length === 0;

  return {
    ok: browserLogoutComplete,
    browserLogoutComplete,
    deletedTotal,
    results,
    message: browserLogoutComplete
      ? `브라우저 로그아웃 완료: naver/로컬 서버 쿠키 ${deletedTotal}건 삭제`
      : '브라우저 네이버 로그아웃 실패: 쿠키 삭제에 실패한 프로필이 있습니다.',
  };
}

module.exports = {
  clearNaverBrowserData,
  getBrowserProfiles,
};
