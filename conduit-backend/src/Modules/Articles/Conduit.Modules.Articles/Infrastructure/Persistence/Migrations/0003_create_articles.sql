CREATE TABLE articles (
  id          UNIQUEIDENTIFIER  NOT NULL CONSTRAINT df_articles_id DEFAULT NEWID(),
  author_id   UNIQUEIDENTIFIER  NOT NULL,
  slug        NVARCHAR(80) COLLATE Latin1_General_100_BIN2 NOT NULL,
  title       NVARCHAR(200)     NOT NULL,
  description NVARCHAR(400)     NOT NULL,
  body        NVARCHAR(MAX)     NOT NULL,
  seq         BIGINT IDENTITY(1, 1) NOT NULL,
  created_at  DATETIMEOFFSET(3) NOT NULL CONSTRAINT df_articles_created_at DEFAULT SYSDATETIMEOFFSET(),
  updated_at  DATETIMEOFFSET(3) NOT NULL CONSTRAINT df_articles_updated_at DEFAULT SYSDATETIMEOFFSET(),
  CONSTRAINT pk_articles PRIMARY KEY (id),
  CONSTRAINT fk_articles_author FOREIGN KEY (author_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT ck_articles_slug CHECK (slug <> N'feed')
);

CREATE UNIQUE INDEX articles_slug_unique ON articles (slug);
CREATE INDEX articles_author ON articles (author_id);
CREATE INDEX articles_order ON articles (created_at DESC, seq DESC);

CREATE TABLE article_tags (
  article_id UNIQUEIDENTIFIER NOT NULL,
  [position] SMALLINT         NOT NULL,
  tag        NVARCHAR(60) COLLATE Latin1_General_100_BIN2 NOT NULL,
  CONSTRAINT pk_article_tags PRIMARY KEY (article_id, [position]),
  CONSTRAINT uq_article_tags_tag UNIQUE (article_id, tag),
  CONSTRAINT fk_article_tags_article FOREIGN KEY (article_id) REFERENCES articles (id) ON DELETE CASCADE
);

CREATE INDEX article_tags_tag ON article_tags (tag);

CREATE TABLE favorites (
  user_id    UNIQUEIDENTIFIER  NOT NULL,
  article_id UNIQUEIDENTIFIER  NOT NULL,
  created_at DATETIMEOFFSET(3) NOT NULL CONSTRAINT df_favorites_created_at DEFAULT SYSDATETIMEOFFSET(),
  CONSTRAINT pk_favorites PRIMARY KEY (user_id, article_id),
  CONSTRAINT fk_favorites_user FOREIGN KEY (user_id) REFERENCES users (id),
  CONSTRAINT fk_favorites_article FOREIGN KEY (article_id) REFERENCES articles (id) ON DELETE CASCADE
);

CREATE INDEX favorites_article ON favorites (article_id);
