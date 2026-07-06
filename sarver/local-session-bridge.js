const fs = require('fs');
const os = require('os');
const path = require('path');

const BRIDGE_DIR = path.join(os.homedir(), '.naver-login-project');
const BRIDGE_FILE = path.join(BRIDGE_DIR, 'local-session.json');

function ensureBridgeDir() {
  fs.mkdirSync(BRIDGE_DIR, { recursive: true });
}

function saveSession({ sessionToken, uid }) {
  if (!sessionToken) {
    return;
  }

  ensureBridgeDir();
  const payload = {
    sessionToken,
    uid: uid || null,
    updatedAt: new Date().toISOString(),
  };
  fs.writeFileSync(BRIDGE_FILE, JSON.stringify(payload, null, 2), 'utf8');
}

function loadSession() {
  try {
    if (!fs.existsSync(BRIDGE_FILE)) {
      return null;
    }

    const raw = fs.readFileSync(BRIDGE_FILE, 'utf8');
    const parsed = JSON.parse(raw);
    const sessionToken = String(parsed.sessionToken || '').trim();

    if (!sessionToken) {
      return null;
    }

    return {
      sessionToken,
      uid: parsed.uid || null,
      updatedAt: parsed.updatedAt || null,
    };
  } catch {
    return null;
  }
}

function clearSession() {
  try {
    if (fs.existsSync(BRIDGE_FILE)) {
      fs.unlinkSync(BRIDGE_FILE);
    }
  } catch {
    // ignore cleanup errors
  }
}

function getBridgePath() {
  return BRIDGE_FILE;
}

module.exports = {
  saveSession,
  loadSession,
  clearSession,
  getBridgePath,
};
