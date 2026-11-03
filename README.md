# Conduit Backend (.NET)

The backend of Conduit, the RealWorld blogging platform: users sign up and sign in, edit their profile, follow other authors, publish articles with tags, favorite articles and comment on them. It is a REST API built with ASP.NET Core and SQL Server, organised as a modular monolith with Domain-Driven Design and Clean Architecture. It is the .NET sibling of the Express backend and implements the same HTTP contract, described in `conduit-openapi/openapi.yml`.

The browser client is the separate Conduit React frontend that talks to this API.

**Stack:** .NET 10 · ASP.NET Core Minimal APIs · Microsoft.Data.SqlClient · SQL Server 2022 · versioned SQL migrations · JWT (Microsoft.IdentityModel) · bcrypt · xUnit · NetArchTest · Docker · GitHub Actions

## Contents

- [Quick start](#quick-start)
- [Features](#features)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [Database](#database)
- [API](#api)
- [Testing](#testing)
- [CI](#ci)
- [Limits](#limits)
- [Migrating from Taskly](#migrating-from-taskly)
- [Migrating from PostgreSQL](#migrating-from-postgresql)
- [References](#references)

## Quick start

Requires the .NET 10 SDK, ICU and Docker.

```sh
cd conduit-docker
make docker/dev-up
cd ../conduit-backend
make dev
```

`make dev` creates `.env` from `.env.example` with a generated `AUTH_SECRET` on the first run, then starts the API. The API runs at http://localhost:8080 and SQL Server at localhost:1433 (user `sa`, password `Dev_Pass123`, database `conduit_dev`). The health check of the development container creates the `conduit_dev` database when it is missing. With any other SQL Server, create the database named in `DATABASE_URL` first, the application creates the tables but not the database. Migrations run automatically on startup. Values in `.env` are read from the working directory, so run the commands from `conduit-backend`. Regular environment variables take precedence over `.env`.

| Goal                | Command                                   |
| ------------------- | ----------------------------------------- |
| Run in development  | `make dev`                                |
| Build               | `make build`                              |
| Run a release build | `make run`                                |
| Seed demo data      | `make seed`                               |
| Run all tests       | `make test`                               |
| Run with Docker     | `make docker/up` (from `conduit-docker`)  |
| Seed in Docker      | `make docker/seed` (from `conduit-docker`)|
| Stop Docker         | `make docker/down` (from `conduit-docker`)|

`make docker/up` creates `conduit-docker/.env` with a random database password and `AUTH_SECRET` on the first run, then starts SQL Server and the API and waits until both are healthy. `PORT` overrides the host port in the Docker setup. The container always listens on port 8080. Demo logins after seeding: `johndoe123@mail.com` / `password123` and `jane@mail.com` / `securepassword456`.

## Features

- **Authentication**: sign up and sign in, JWT sent in the `Authorization: Token <jwt>` header.
- **Profiles**: view a profile, follow and unfollow authors, update your own profile.
- **Articles**: create, view, update and delete articles with tags, slugs derived from the title, filters by tag, author and favorited user, pagination and a feed of followed authors.
- **Favorites**: favorite and unfavorite articles, with a favorites count.
- **Comments**: add, list and delete comments on an article.
- **Tags**: the twenty most used tags.
- **Platform**: versioned SQL migrations, request logging, graceful shutdown, a health check on `/health`.

## Configuration

| Variable                | Default                 | Purpose                                                  |
| ----------------------- | ----------------------- | -------------------------------------------------------- |
| `APP_NAME`              | `conduit-api`           | Application name                                         |
| `APP_HOST`              | `0.0.0.0`               | Bind address                                             |
| `APP_PORT`              | `8080`                  | Port the API listens on                                  |
| `APP_LEVEL`             | `development`           | Environment level, `production` blocks seeding by default|
| `DATABASE_URL`          | required                | `sqlserver://user:password@host:1433/db`, optionally with `?encrypt=true\|false\|strict&trustServerCertificate=true`, a SQL Server connection string is accepted too |
| `DATABASE_POOL_MAX`     | `10`                    | Maximum connections in the pool                          |
| `DATABASE_TIMEOUT`      | `30000`                 | Connection timeout in milliseconds                       |
| `AUTH_SECRET`           | required                | JWT secret, at least 32 characters                       |
| `AUTH_TOKEN_TTL`        | `7d`                    | Token lifetime: seconds, or a number with `s`, `m`, `h`, `d` or `w` |
| `BCRYPT_ROUNDS`         | `10`                    | bcrypt cost factor, 4 to 31                              |
| `CLIENT_URL`            | `http://localhost:3000` | Allowed CORS origin                                      |
| `LOG_LEVEL`             | `debug`                 | Log level of the Conduit code, `info` in production      |
| `SEED_ALLOW_PRODUCTION` | `false`                 | Allows seeding when `APP_LEVEL` is `production`          |

Docker only, in `conduit-docker/.env`:

| Variable      | Default   | Purpose                                 |
| ------------- | --------- | --------------------------------------- |
| `PORT`        | `8080`    | Host port used by Docker                |
| `DB_NAME`     | `conduit` | SQL Server database name                |
| `DB_PASSWORD` | required  | Password of the `sa` login, generated by make |

The frontend must set its API base URL to `http://localhost:8080/api/v1`.

Slug generation relies on Unicode normalization, which needs ICU. The host refuses to start without it, and the Docker image installs `icu-libs` and turns invariant globalization off.

## Architecture

### Project layout

```
conduit-backend/
  src/
    Conduit.Host/                           composition root: configuration, pipeline, seed command
    Conduit.SharedKernel/                   errors, pagination, ids, event bus, cross-module contracts, HTTP and persistence helpers
    Modules/
      Users/Conduit.Modules.Users/          accounts, authentication, profiles, follows
      Articles/Conduit.Modules.Articles/    articles, tags, favorites, feed
      Comments/Conduit.Modules.Comments/    comments on articles
  tests/
    Conduit.UnitTests/                      domain, use cases, JWT, bcrypt, middleware, configuration
    Conduit.IntegrationTests/               full HTTP flows and migrations on a real SQL Server
    Conduit.ArchitectureTests/              layer and module boundary rules
conduit-docker/
  conduit-backend-dotnet/Dockerfile
  docker-compose.local.yml
  docker-compose.dev.yml
  Makefile
conduit-openapi/openapi.yml                 the HTTP contract
.github/workflows/ci.yml
```

### Module layout

```
Conduit.Modules.<Module>/
  Domain/          entities, value objects, repository and service contracts
  Application/     use cases
  Infrastructure/  SQL Server, JWT and bcrypt adapters, SQL migrations
  Presentation/    Minimal API endpoints
  <Module>Module.cs  dependency injection and endpoint registration
```

### Dependency rules

- Dependencies point inward: presentation and infrastructure depend on application, application depends on domain.
- Modules never reference each other. Cross-module needs go through contracts that live in the shared kernel: `IAuthorDirectory` (implemented by the users module, used by articles and comments to resolve author profiles and followed authors), `IArticleCatalog` (implemented by the articles module, used by comments to resolve a slug) and the `ArticleDeleted` event on the in-process event bus, which makes the comments module remove the comments of a deleted article.
- Each module owns its tables, its SQL migrations and its endpoints. The host only wires modules together.
- Authentication is cross-cutting: endpoints are marked with `RequireAccessToken()` or `OptionalAccessToken()`, a middleware in the shared kernel reads the `Authorization: Token <jwt>` header, asks the registered `IRequestAuthenticator` (implemented by the users module) and exposes the user to endpoints. A malformed or invalid token is rejected with 401 even on optional endpoints.
- `Conduit.ArchitectureTests` fails the build when a layer or a module crosses these boundaries.

## Database

Schema changes live in each module as `Infrastructure/Persistence/Migrations/<number>_<name>.sql`, embedded in the assembly. `SqlMigrator` collects the scripts of all modules, sorts them by name and applies the pending ones on startup under a SQL Server application lock (`sp_getapplock`), one transaction per script, so every script is sent to the server as a single batch. Applied names are recorded in `schema_migrations`.

| Table          | Notes                                                                                             |
| -------------- | ------------------------------------------------------------------------------------------------- |
| `users`        | `id` UNIQUEIDENTIFIER, case-insensitive unique `email` and `username` through persisted lowercase columns, `password_hash`, nullable `bio` and `image`, timestamps |
| `follows`      | `(follower_id, followed_id)` primary key, both reference `users`, a user cannot follow itself    |
| `articles`     | `id` UNIQUEIDENTIFIER, `author_id`, unique `slug` (never `feed`), `title`, `description`, `body`, an identity `seq` used as a tie-breaker, timestamps |
| `article_tags` | `(article_id, position)` primary key keeps the tag order, unique `(article_id, tag)`              |
| `favorites`    | `(user_id, article_id)` primary key                                                              |
| `comments`     | `id` BIGINT IDENTITY, `article_id`, `author_id`, `body`, timestamps                              |

- Deleting an article removes its tags, favorites and comments through `ON DELETE CASCADE`.
- SQL Server rejects tables reachable through more than one cascade path, so `comments.author_id`, `favorites.user_id` and `follows.followed_id` are plain foreign keys. The `INSTEAD OF DELETE` trigger `users_delete_cascade` (migration `0005_users_delete_cascade.sql`) removes those rows when a user is deleted, which keeps the behaviour of the PostgreSQL schema.
- Text columns are `NVARCHAR` and sized at twice the length the domain validates, because SQL Server counts UTF-16 code units and the domain counts characters.
- `slug` and `tag` use the binary collation `Latin1_General_100_BIN2`, so they stay case-sensitive and sorted by code point whatever the collation of the database.
- Timestamps are `DATETIMEOFFSET(3)`, stored in UTC with millisecond precision.
- Listings are ordered by `created_at` and then by the identity column, so ties on the creation time still give a stable order.
- Timestamps are returned as ISO 8601 UTC strings with milliseconds, for example `2026-01-01T00:00:00.000Z`.

## API

All routes are under `/api/v1`. Authenticated requests send `Authorization: Token <jwt>`. Bodies are JSON and wrapped in an envelope (`{ "user": {...} }`, `{ "article": {...} }`, `{ "comment": {...} }`). The full contract is in `conduit-openapi/openapi.yml`.

| Method | Path                               | Auth     | Purpose                                              |
| ------ | ---------------------------------- | -------- | ---------------------------------------------------- |
| POST   | `/users`                           | none     | Register, returns 201                                |
| POST   | `/users/login`                     | none     | Sign in                                              |
| GET    | `/user`                            | required | Current user with a fresh token                      |
| PUT    | `/user`                            | required | Update the current user                              |
| GET    | `/profiles/{username}`             | optional | View a profile                                       |
| POST   | `/profiles/{username}/follow`      | required | Follow, idempotent                                   |
| DELETE | `/profiles/{username}/follow`      | required | Unfollow, idempotent                                 |
| GET    | `/articles`                        | optional | List with `tag`, `author`, `favorited`, `offset`, `limit` |
| GET    | `/articles/feed`                   | required | Articles of followed authors                         |
| POST   | `/articles`                        | required | Create an article, returns 201                       |
| GET    | `/articles/{slug}`                 | optional | Get an article                                       |
| PUT    | `/articles/{slug}`                 | required | Update your article                                  |
| DELETE | `/articles/{slug}`                 | required | Delete your article, returns 204                     |
| POST   | `/articles/{slug}/favorite`        | required | Favorite, idempotent                                 |
| DELETE | `/articles/{slug}/favorite`        | required | Unfavorite, idempotent                               |
| GET    | `/articles/{slug}/comments`        | optional | List comments, newest first                          |
| POST   | `/articles/{slug}/comments`        | required | Add a comment, returns 201                           |
| DELETE | `/articles/{slug}/comments/{id}`   | required | Delete your comment, returns 204                     |
| GET    | `/tags`                            | none     | The twenty most used tags                            |
| GET    | `/health` (outside `/api/v1`)      | none     | Plain text `OK` when the database answers            |

Errors are always `{ "errors": { "<field>": ["<message>"] } }`:

| Status | When                                                         | Example                                         |
| ------ | ------------------------------------------------------------ | ----------------------------------------------- |
| 400    | The body is not valid JSON                                   | `{ "errors": { "body": ["is not valid JSON"] } }` |
| 401    | Missing, malformed, invalid or expired token, bad credentials| `{ "errors": { "token": ["is missing"] } }`     |
| 403    | Editing or deleting something you do not own                 | `{ "errors": { "article": ["forbidden"] } }`    |
| 404    | Unknown route, profile, article or comment                   | `{ "errors": { "article": ["not found"] } }`    |
| 409    | Email or username already taken                              | `{ "errors": { "email": ["has already been taken"] } }` |
| 413    | The body is larger than 256 KB                               | `{ "errors": { "body": ["is too large"] } }`    |
| 415    | A body that is not `application/json`                        | `{ "errors": { "content-type": ["must be application/json"] } }` |
| 422    | Invalid input, reported for every field at once              | `{ "errors": { "title": ["can't be blank"] } }` |
| 500    | Unexpected failure, details are never leaked                 | `{ "errors": { "server": ["internal server error"] } }` |

Checks run in a fixed order: authentication (401), existence (404), ownership (403), validation (422). A malformed body or content type is reported as soon as the body is read, after authentication.

Rules worth knowing:

- Usernames are 3 to 30 characters of letters, digits, `_` and `-`. Passwords are 8 to 72 characters and at most 72 bytes. Emails are lowercased. `image` must be an `https` URL.
- Slugs come from the title (accents removed, lowercase, dashes) and get `-2`, `-3`, and so on when taken. Changing the title changes the slug.
- A tag list keeps its order, drops duplicates, allows at most 10 tags of at most 30 characters, with no whitespace or commas inside a tag.
- `offset` defaults to 0, `limit` defaults to 20 and is capped at 50. `PUT /articles/{slug}` and `PUT /user` need at least one known field.

## Testing

| Layer        | Command                  | What it proves                                                          |
| ------------ | ------------------------ | ----------------------------------------------------------------------- |
| Unit         | `make test/unit`         | Domain, use cases, JWT, bcrypt, middleware and configuration, no I/O    |
| Architecture | `make test/architecture` | Layers and modules respect the dependency rules                         |
| Integration  | `make test/integration`  | Full HTTP flows and migrations on a real SQL Server                     |

Integration tests need a SQL Server instance where the login can create and drop databases. They read `TEST_DATABASE_URL` and default to the local SQL Server from the quick start, always connecting through `master`. The suite creates its own temporary databases, runs the migrations and drops them when it finishes.

```sh
TEST_DATABASE_URL="sqlserver://sa:Dev_Pass123@localhost:1433/master?trustServerCertificate=true" dotnet test tests/Conduit.IntegrationTests
```

## CI

`.github/workflows/ci.yml` runs these jobs:

1. **Commitlint**: commit message linting on pull requests, configured in `.commitlintrc.json`.
2. **Build**: restores and compiles the solution with the .NET 10 SDK.
3. **Tests**: unit, architecture and integration suites against a SQL Server 2022 service.
4. **Docker**: builds the image, starts the Compose stack, then checks `/health`, registers a user, reads `/tags` and runs the seed.

## Limits

- The API needs a running SQL Server 2017 or newer, the queries use `STRING_AGG`, `OPENJSON` and `OFFSET ... FETCH`, which need compatibility level 130 or higher.
- Events are delivered in-process, so the app runs as a single instance.
- Logs go to the console only. Collect them from the container output.
- Access tokens are stateless and live for `AUTH_TOKEN_TTL`. A token of a deleted user is rejected as invalid on its next use because the user is checked on every request.
- There is no endpoint to delete an account, as the contract does not define one.

## Migrating from Taskly

This version replaces the Taskly task manager. The business, the HTTP contract and the schema changed, so nothing carries over:

- Use a fresh database. The migration `0001_create_users.sql` keeps its name but creates a different table, so a database that already ran the Taskly migrations is not upgraded. Drop it or point `DATABASE_URL` at a new one.
- Authentication moved from the `taskly_token` cookie to the `Authorization: Token <jwt>` header, and the user id moved from the `id` claim to the standard `sub` claim. Tokens issued by Taskly are rejected.
- `COOKIE_DOMAIN` and `PUBLIC_DIR` no longer exist, there are no avatar uploads or static files, and `AUTH_TOKEN_TTL` replaces the fixed one hour lifetime.
- The `/` route is gone. Use `/health`, which now answers `OK` as plain text.
- Errors changed from `{ "status", "message" }` to `{ "errors": { field: [messages] } }`.

## Migrating from PostgreSQL

This version replaces PostgreSQL with SQL Server. The HTTP contract is unchanged, the schema keeps the same tables and columns:

- Start with an empty SQL Server database. The migration scripts were rewritten in T-SQL and are not applied to an existing PostgreSQL database, and no tool moves the data.
- `DATABASE_URL` uses `sqlserver://` instead of `postgres://`. Local servers with a self-signed certificate need `?trustServerCertificate=true`.
- Npgsql was replaced by `Microsoft.Data.SqlClient`, `PostgresErrors` by `SqlServerErrors` and the `Postgres*Repository` classes by `SqlServer*Repository`.
- Migrations take an application lock instead of a PostgreSQL advisory lock.
- The Compose files in `conduit-docker` run SQL Server 2022 instead of PostgreSQL and pgAdmin is gone. The backend connects as `sa`, `DB_USER` no longer exists and `DB_PASSWORD` is the `sa` password, which `make docker/env` generates so that it meets the SQL Server password policy. The database itself is created by the health check of the SQL Server container.

## References

- [ASP.NET Core](https://learn.microsoft.com/aspnet/core)
- [Microsoft.Data.SqlClient](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [SQL Server](https://learn.microsoft.com/sql/sql-server/)
- [Microsoft.IdentityModel](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)
- [BCrypt.Net-Next](https://github.com/BcryptNet/bcrypt.net)
- [xUnit](https://xunit.net/)
- [NetArchTest](https://github.com/BenMorris/NetArchTest)
- [RealWorld](https://github.com/gothinkster/realworld)
- [Docker multi-stage builds](https://docs.docker.com/build/building/multi-stage/)
