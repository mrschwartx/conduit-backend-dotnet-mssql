CREATE TABLE follows (
  follower_id UNIQUEIDENTIFIER  NOT NULL,
  followed_id UNIQUEIDENTIFIER  NOT NULL,
  created_at  DATETIMEOFFSET(3) NOT NULL CONSTRAINT df_follows_created_at DEFAULT SYSDATETIMEOFFSET(),
  CONSTRAINT pk_follows PRIMARY KEY (follower_id, followed_id),
  CONSTRAINT fk_follows_follower FOREIGN KEY (follower_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT fk_follows_followed FOREIGN KEY (followed_id) REFERENCES users (id),
  CONSTRAINT ck_follows_distinct CHECK (follower_id <> followed_id)
);

CREATE INDEX follows_followed ON follows (followed_id);
