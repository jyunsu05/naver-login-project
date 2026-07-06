const { execSync } = require('child_process');

const BROWSER_PROCESSES = [
  'chrome.exe',
  'msedge.exe',
];

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function closeBrowserProcesses() {
  const closed = [];

  for (const processName of BROWSER_PROCESSES) {
    try {
      execSync(`taskkill /F /IM ${processName} /T`, {
        stdio: 'ignore',
        windowsHide: true,
      });
      closed.push(processName);
    } catch {
      // process was not running
    }
  }

  return closed;
}

async function closeBrowsersAndWait(options = {}) {
  const waitMs = Number(options.waitMs) || 1500;
  const closed = closeBrowserProcesses();
  await sleep(waitMs);
  return {
    closed,
    message: closed.length > 0
      ? `브라우저 종료: ${closed.join(', ')}`
      : '실행 중인 Chrome/Edge 없음',
  };
}

module.exports = {
  closeBrowserProcesses,
  closeBrowsersAndWait,
  BROWSER_PROCESSES,
};
