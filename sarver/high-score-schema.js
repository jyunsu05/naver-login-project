const CREATE_USER_HIGH_SCORES_TABLE_SQL = `
  CREATE TABLE IF NOT EXISTS user_high_scores (
    id          INT          NOT NULL AUTO_INCREMENT,
    uid         VARCHAR(64)  NOT NULL,
    high_score  INT          NOT NULL DEFAULT 0,
    updated_at  TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    PRIMARY KEY (id),
    UNIQUE KEY UK_user_high_scores_uid (uid),
    KEY IDX_user_high_scores_high_score (high_score DESC)
  )
`;

module.exports = {
  CREATE_USER_HIGH_SCORES_TABLE_SQL,
};
