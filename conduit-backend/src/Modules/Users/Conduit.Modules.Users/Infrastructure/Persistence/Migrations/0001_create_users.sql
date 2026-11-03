CREATE TABLE users (
  id             UNIQUEIDENTIFIER  NOT NULL CONSTRAINT df_users_id DEFAULT NEWID(),
  username       NVARCHAR(60)      NOT NULL,
  email          NVARCHAR(508)     NOT NULL,
  password_hash  NVARCHAR(MAX)     NOT NULL,
  bio            NVARCHAR(600)     NULL,
  image          NVARCHAR(MAX)     NULL,
  created_at     DATETIMEOFFSET(3) NOT NULL CONSTRAINT df_users_created_at DEFAULT SYSDATETIMEOFFSET(),
  updated_at     DATETIMEOFFSET(3) NOT NULL CONSTRAINT df_users_updated_at DEFAULT SYSDATETIMEOFFSET(),
  email_lower    AS (LOWER(email) COLLATE Latin1_General_100_BIN2) PERSISTED,
  username_lower AS (LOWER(username) COLLATE Latin1_General_100_BIN2) PERSISTED,
  CONSTRAINT pk_users PRIMARY KEY (id)
);

CREATE UNIQUE INDEX users_email_unique ON users (email_lower);
CREATE UNIQUE INDEX users_username_unique ON users (username_lower);
