CREATE TABLE comments (
  id         BIGINT IDENTITY(1, 1) NOT NULL,
  article_id UNIQUEIDENTIFIER      NOT NULL,
  author_id  UNIQUEIDENTIFIER      NOT NULL,
  body       NVARCHAR(2000)        NOT NULL,
  created_at DATETIMEOFFSET(3)     NOT NULL CONSTRAINT df_comments_created_at DEFAULT SYSDATETIMEOFFSET(),
  updated_at DATETIMEOFFSET(3)     NOT NULL CONSTRAINT df_comments_updated_at DEFAULT SYSDATETIMEOFFSET(),
  CONSTRAINT pk_comments PRIMARY KEY (id),
  CONSTRAINT fk_comments_article FOREIGN KEY (article_id) REFERENCES articles (id) ON DELETE CASCADE,
  CONSTRAINT fk_comments_author FOREIGN KEY (author_id) REFERENCES users (id)
);

CREATE INDEX comments_article ON comments (article_id, created_at DESC, id DESC);
