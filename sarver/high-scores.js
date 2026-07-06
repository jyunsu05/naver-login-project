const { query, execute } = require('./dbconnect');
const { CREATE_USER_HIGH_SCORES_TABLE_SQL } = require('./high-score-schema');
const logger = require('./logger');

async function ensureHighScoresTable() {
  await execute(CREATE_USER_HIGH_SCORES_TABLE_SQL);
  logger.log('SCORE', 'user_high_scores 테이블 확인 완료');
}

function validateScoreBody(body) {
  const score = Number(body?.score);

  if (!Number.isInteger(score) || score < 0) {
    return { ok: false, message: 'score는 0 이상의 정수여야 합니다.' };
  }

  return {
    ok: true,
    data: { score },
  };
}

function toHighScoreDto(row) {
  return {
    highScore: Number(row?.high_score || 0),
    updatedAt: row?.updated_at || null,
  };
}

async function getHighScoreByUid(uid) {
  const rows = await query(
    'SELECT high_score, updated_at FROM user_high_scores WHERE uid = ? LIMIT 1',
    [uid],
  );

  if (rows.length === 0) {
    return {
      highScore: 0,
      updatedAt: null,
    };
  }

  return toHighScoreDto(rows[0]);
}

async function submitHighScore(uid, score) {
  const rows = await query(
    'SELECT high_score FROM user_high_scores WHERE uid = ? LIMIT 1',
    [uid],
  );

  if (rows.length === 0) {
    await execute(
      'INSERT INTO user_high_scores (uid, high_score) VALUES (?, ?)',
      [uid, score],
    );

    return {
      highScore: score,
      previousHighScore: 0,
      isNewRecord: score > 0,
    };
  }

  const previousHighScore = Number(rows[0].high_score || 0);

  if (score <= previousHighScore) {
    return {
      highScore: previousHighScore,
      previousHighScore,
      isNewRecord: false,
    };
  }

  await execute(
    'UPDATE user_high_scores SET high_score = ? WHERE uid = ?',
    [score, uid],
  );

  return {
    highScore: score,
    previousHighScore,
    isNewRecord: true,
  };
}

async function getTopRankings(limit = 10) {
  const safeLimit = Math.min(Math.max(Number(limit) || 10, 1), 100);

  const rows = await query(
    `SELECT
       u.uid,
       u.name,
       h.high_score,
       h.updated_at
     FROM user_high_scores h
     INNER JOIN users u ON u.uid = h.uid
     ORDER BY h.high_score DESC, h.updated_at ASC
     LIMIT ?`,
    [safeLimit],
  );

  return rows.map((row, index) => ({
    rank: index + 1,
    uid: row.uid,
    name: row.name,
    highScore: Number(row.high_score || 0),
    updatedAt: row.updated_at,
  }));
}

module.exports = {
  ensureHighScoresTable,
  validateScoreBody,
  getHighScoreByUid,
  submitHighScore,
  getTopRankings,
};
