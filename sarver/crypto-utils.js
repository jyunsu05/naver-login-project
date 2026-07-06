const crypto = require('crypto');

const ALGORITHM = 'aes-256-gcm';
const IV_LENGTH = 12;

function getEncryptionKey() {
  const secret = process.env.TOKEN_ENCRYPTION_KEY || process.env.SESSION_JWT_SECRET;
  if (!secret) {
    throw new Error('TOKEN_ENCRYPTION_KEY 또는 SESSION_JWT_SECRET이 .env에 필요합니다.');
  }

  return crypto.createHash('sha256').update(secret).digest();
}

function encryptText(plainText) {
  if (!plainText) {
    return null;
  }

  const key = getEncryptionKey();
  const iv = crypto.randomBytes(IV_LENGTH);
  const cipher = crypto.createCipheriv(ALGORITHM, key, iv);
  const encrypted = Buffer.concat([cipher.update(String(plainText), 'utf8'), cipher.final()]);
  const tag = cipher.getAuthTag();

  return Buffer.concat([iv, tag, encrypted]).toString('base64');
}

function decryptText(encryptedText) {
  if (!encryptedText) {
    return null;
  }

  const key = getEncryptionKey();
  const buffer = Buffer.from(String(encryptedText), 'base64');
  const iv = buffer.subarray(0, IV_LENGTH);
  const tag = buffer.subarray(IV_LENGTH, IV_LENGTH + 16);
  const encrypted = buffer.subarray(IV_LENGTH + 16);
  const decipher = crypto.createDecipheriv(ALGORITHM, key, iv);
  decipher.setAuthTag(tag);

  const decrypted = Buffer.concat([decipher.update(encrypted), decipher.final()]);
  return decrypted.toString('utf8');
}

module.exports = {
  encryptText,
  decryptText,
};
