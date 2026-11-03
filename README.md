# Conduit Backend (.NET, SQL Server)

The backend of Conduit, a Medium-style blogging platform built on the RealWorld API contract: sign up and sign in, edit your profile, follow other authors, publish articles with tags, favorite them, read a personal feed and discuss in comments. It is a REST API built with ASP.NET Core and SQL Server, organised as a modular monolith with Domain-Driven Design and Clean Architecture.

The browser client is the separate Conduit React frontend. The API follows `conduit-openapi/openapi.yml`, the same contract the frontend and the other Conduit backends are written against.

**Stack:** .NET 10 · ASP.NET Core Minimal APIs · Microsoft.Data.SqlClient · SQL Server 2022 · versioned SQL migrations · JWT (Microsoft.IdentityModel) · bcrypt · xUnit · NetArchTest · Docker · GitHub Actions

## Contents

- [Quick start](#quick-start)
- [Features](#features)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [Database](#database)
- [API](#api)
- [Contract decisions](#contract-decisions)
- [Testing](#testing)
- [CI](#ci)
- [Limits](#limits)
- [References](#references)

## Quick start

Requires the .NET 10 SDK, ICU and Docker.

```sh
cd conduit-docker
make docker/dev-up
cd ../conduit-backend
make dev
```

`make dev` creates `.env` from `.env.example` with a generated `AUTH_SECRET` on the first run, then starts the API. The API runs at http://localhost:8080 and SQL Server at localhost:1433 (user `sa`, password `Dev_Pass123`, database `conduit_dev`). The health check of the development container creates `conduit_dev` when it is missing. With any other SQL Server, create the database named in `DATABASE_URL` first, because the application creates the tables but not the database. Migrations run automatically on startup. Values in `.env` are read from the working directory, so run the commands from `conduit-backend`. Regular environment variables and command line arguments take precedence over `.env`.

| Goal                | Command                                    |
| ------------------- | ------------------------------------------ |
| Run in development  | `make dev`                                 |
| Restore packages    | `make restore`                             |
| Build               | `make build`                               |
| Run a release build | `make run`                                 |
| Seed demo data      | `make seed`                                |
| Run all tests       | `make test`                                |
| Build and test      | `make verify`                              |
| Clean build output  | `make clean`                               |
| Run with Docker     | `make docker/up` (from `conduit-docker`)   |
| Seed in Docker      | `make docker/seed` (from `conduit-docker`) |
| Stop Docker         | `make docker/down` (from `conduit-docker`) |

`make docker/up` creates `conduit-docker/.env` with a random database password and `AUTH_SECRET` on the first run, then starts SQL Server and the API and waits until both are healthy. `PORT` overrides the host port in the Docker setup. The container always listens on port 8080. Demo logins after seeding: `johndoe123@mail.com` / `password123` and `jane@mail.com` / `securepassword456`. Seeding skips quietly when the demo users already exist, and it refuses to run when `APP_LEVEL` is `production` unless `SEED_ALLOW_PRODUCTION` is `true`.

## Features

- **Accounts**: sign up and sign in, read and update the current user (username, email, password, bio, image).
- **Profiles**: public profiles, follow and unfollow.
- **Articles**: create, read, update and delete, with tags, slugs derived from the title, favorites, filtering by tag, author and favoriter, pagination and a feed of followed authors.
- **Comments**: list, add and delete comments on an article.
- **Tags**: the 20 most used tags.
- **Platform**: versioned SQL migrations, request logging to the console, graceful shutdown, `GET /health` that checks the database.

## Configuration

| Variable                | Default                 | Purpose                                                                                                                                                              |
| ----------------------- | ----------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `APP_NAME`              | `conduit-api`           | Application name                                                                                                                                                     |
| `APP_HOST`              | `0.0.0.0`               | Bind address                                                                                                                                                         |
| `APP_PORT`              | `8080`                  | Port the API listens on                                                                                                                                              |
| `APP_LEVEL`             | `development`           | Environment level, `production` blocks seeding by default                                                                                                            |
| `DATABASE_URL`          | required                | `sqlserver://user:password@host:1433/db`, optionally with `?encrypt=true\|false\|strict&trustServerCertificate=true`, a SQL Server connection string is accepted too |
| `DATABASE_POOL_MAX`     | `10`                    | Maximum connections in the pool                                                                                                                                      |
| `DATABASE_TIMEOUT`      | `30000`                 | Connection timeout in milliseconds                                                                                                                                   |
| `AUTH_SECRET`           | required                | JWT secret, at least 32 characters                                                                                                                                   |
| `AUTH_TOKEN_TTL`        | `7d`                    | Token lifetime: seconds, or a number with `s`, `m`, `h`, `d` or `w`, an invalid value stops the start                                                                |
| `BCRYPT_ROUNDS`         | `10`                    | bcrypt cost factor, 4 to 31                                                                                                                                          |
| `CLIENT_URL`            | `http://localhost:3000` | Allowed CORS origin                                                                                                                                                  |
| `LOG_LEVEL`             | `debug`                 | Log level of the Conduit code, `info` when `APP_LEVEL` is `production`                                                                                               |
| `SEED_ALLOW_PRODUCTION` | `false`                 | Allows seeding when `APP_LEVEL` is `production`                                                                                                                      |

Docker only, in `conduit-docker/.env`:

| Variable         | Default                 | Purpose                                       |
| ---------------- | ----------------------- | --------------------------------------------- |
| `PORT`           | `8080`                  | Host port used by Docker                      |
| `DB_NAME`        | `conduit`               | SQL Server database name                      |
| `DB_PASSWORD`    | required                | Password of the `sa` login, generated by make |
| `AUTH_SECRET`    | required                | JWT secret, generated by make                 |
| `AUTH_TOKEN_TTL` | `7d`                    | Lifetime of issued tokens                     |
| `APP_LEVEL`      | `development`           | Environment level                             |
| `CLIENT_URL`     | `http://localhost:3000` | Allowed CORS origin                           |
| `LOG_LEVEL`      | `info`                  | Log level                                     |

The frontend reaches the API through its own `/api` proxy (the Vite dev server or the nginx image), so no CORS setup is needed for the default stack. The backend still allows `CLIENT_URL` for direct cross-origin use.

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
.github/workflows/commitlint.yml
.commitlintrc.json
```

### Modules

```
Conduit.Modules.<Module>/
  Domain/            entities, value objects, repository and service contracts
  Application/       use cases
  Infrastructure/    SQL Server, JWT and bcrypt adapters, SQL migrations
  Presentation/      Minimal API endpoints
  <Module>Module.cs  dependency injection and endpoint registration
```

### Dependency rules

- Dependencies point inward: presentation and infrastructure depend on application, application depends on domain.
- Modules never reference each other. Cross-module needs go through contracts in the shared kernel: `IAuthorDirectory` (implemented by users, used by articles and comments to resolve author profiles and followed authors), `IArticleCatalog` (implemented by articles, used by comments to resolve a slug) and the `ArticleDeleted` event on the in-process event bus, which makes comments remove the comments of a deleted article.
- Each module owns its tables, its SQL migrations and its endpoints. The host only wires modules together.
- Authentication is cross-cutting: endpoints are marked with `RequireAccessToken()` or `OptionalAccessToken()`, and a middleware in the shared kernel reads the `Authorization: Token <jwt>` header, asks the registered `IRequestAuthenticator` (implemented by users) and exposes the user to endpoints.
- `Conduit.ArchitectureTests` fails the build when a layer or a module crosses these boundaries.

## Database

Schema changes live in each module as `Infrastructure/Persistence/Migrations/<number>_<name>.sql`, embedded in the assembly. `SqlMigrator` collects the scripts of all modules, sorts them by name and applies the pending ones on startup under a SQL Server application lock (`sp_getapplock`), one transaction per script. Applied names are recorded in `schema_migrations`.

| Table          | Notes                                                                                                                                                              |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `users`        | `id` UNIQUEIDENTIFIER, case-insensitive unique `email` and `username` through persisted lowercase columns, `password_hash`, nullable `bio` and `image`, timestamps |
| `follows`      | `(follower_id, followed_id)` primary key, a user cannot follow itself                                                                                              |
| `articles`     | `id` UNIQUEIDENTIFIER, `author_id`, unique `slug` (never `feed`), `title`, `description`, `body`, an identity `seq` used as a tie-breaker, timestamps              |
| `article_tags` | `(article_id, position)` primary key keeps the tag order, unique `(article_id, tag)`                                                                               |
| `favorites`    | `(user_id, article_id)` primary key                                                                                                                                |
| `comments`     | `id` BIGINT IDENTITY, `article_id`, `author_id`, `body`, timestamps                                                                                                |

- Deleting an article removes its tags, favorites and comments through `ON DELETE CASCADE`.
- SQL Server rejects tables reachable through more than one cascade path, so `comments.author_id`, `favorites.user_id` and `follows.followed_id` are plain foreign keys. The `INSTEAD OF DELETE` trigger `users_delete_cascade` (migration `0005_users_delete_cascade.sql`) removes those rows when a user is deleted.
- Text columns are `NVARCHAR` and sized at twice the length the domain validates, because SQL Server counts UTF-16 code units and the domain counts characters.
- `slug` and `tag` use the binary collation `Latin1_General_100_BIN2`, so they stay case sensitive and sorted by code point whatever the collation of the database.
- Listings are ordered by `created_at` and then by the identity column, so ties on the creation time still give a stable order.
- Timestamps are `DATETIMEOFFSET(3)`, stored in UTC with millisecond precision and returned as ISO 8601 UTC strings.

## API

All routes are under `/api/v1` and follow `conduit-openapi/openapi.yml`. Protected routes expect `Authorization: Token <jwt>`. Bodies are JSON and wrapped in an envelope (`{ "user": {...} }`, `{ "article": {...} }`, `{ "comment": {...} }`).

| Method | Path                             | Auth     | Purpose                                                   |
| ------ | -------------------------------- | -------- | --------------------------------------------------------- |
| POST   | `/users`                         | none     | Register, returns 201                                     |
| POST   | `/users/login`                   | none     | Sign in                                                   |
| GET    | `/user`                          | required | Current user with a fresh token                           |
| PUT    | `/user`                          | required | Update the current user                                   |
| GET    | `/profiles/{username}`           | optional | View a profile                                            |
| POST   | `/profiles/{username}/follow`    | required | Follow, idempotent                                        |
| DELETE | `/profiles/{username}/follow`    | required | Unfollow, idempotent                                      |
| GET    | `/articles`                      | optional | List with `tag`, `author`, `favorited`, `offset`, `limit` |
| GET    | `/articles/feed`                 | required | Articles of followed authors                              |
| POST   | `/articles`                      | required | Create an article, returns 201                            |
| GET    | `/articles/{slug}`               | optional | Get an article                                            |
| PUT    | `/articles/{slug}`               | required | Update your article                                       |
| DELETE | `/articles/{slug}`               | required | Delete your article, returns 204                          |
| POST   | `/articles/{slug}/favorite`      | required | Favorite, idempotent                                      |
| DELETE | `/articles/{slug}/favorite`      | required | Unfavorite, idempotent                                    |
| GET    | `/articles/{slug}/comments`      | optional | List comments, newest first                               |
| POST   | `/articles/{slug}/comments`      | required | Add a comment, returns 201                                |
| DELETE | `/articles/{slug}/comments/{id}` | required | Delete your comment, returns 204                          |
| GET    | `/tags`                          | none     | The twenty most used tags                                 |

`GET /health` (outside `/api/v1`) returns plain text `OK` when the database answers and `Unhealthy` otherwise.

Errors are always `{ "errors": { "<field>": ["<message>"] } }`:

| Status | When                                                          | Example                                                          |
| ------ | ------------------------------------------------------------- | ---------------------------------------------------------------- |
| 400    | The body is not valid JSON                                    | `{ "errors": { "body": ["is not valid JSON"] } }`                |
| 401    | Missing, malformed, invalid or expired token, bad credentials | `{ "errors": { "token": ["is missing"] } }`                      |
| 403    | Editing or deleting something you do not own                  | `{ "errors": { "article": ["forbidden"] } }`                     |
| 404    | Unknown route, profile, article or comment                    | `{ "errors": { "article": ["not found"] } }`                     |
| 409    | Email or username already taken                               | `{ "errors": { "email": ["has already been taken"] } }`          |
| 413    | The body is larger than 256 KB                                | `{ "errors": { "body": ["is too large"] } }`                     |
| 415    | A body that is not `application/json`                         | `{ "errors": { "content-type": ["must be application/json"] } }` |
| 422    | Invalid input, reported for every field at once               | `{ "errors": { "title": ["can't be blank"] } }`                  |
| 500    | Unexpected failure, details are never leaked                  | `{ "errors": { "server": ["internal server error"] } }`          |

## Contract decisions

Where the contract leaves room, the API behaves as follows.

- **Check order** on protected routes: authentication (401), existence (404), ownership (403), validation (422). A malformed body or content type is reported as soon as the body is read, after authentication.
- **Authentication**: only the `Token` scheme is accepted. A malformed or invalid token is rejected with `401` even on optional-auth routes. A token of a deleted user is rejected as invalid because the user is checked on every request.
- **Validation** trims text before measuring and counts Unicode characters. Passwords are not trimmed. All failing fields are reported together.
- **Registration conflicts** list every taken field, matched without regard to case. Emails are stored lowercase.
- **`PUT /user` and `PUT /articles/{slug}`** need at least one known field, otherwise `422` with `user` or `article` set to `can't be empty`. An absent field is left alone, and `bio` and `image` clear with `null` or an empty string.
- **Slugs** come from the title (accents removed, lowercase, dashes, at most 80 characters, `article` when nothing is left). Collisions get `-2`, `-3` and so on, `feed` is reserved, and a changed title regenerates the slug.
- **Tags** keep their order, drop duplicates, allow at most 10 tags of 1 to 30 characters with no whitespace or commas inside, and are case sensitive.
- **Pagination**: `offset` defaults to 0, `limit` defaults to 20 and is capped at 50. Non-integers, `offset` below 0 or `limit` below 1 answer `422`.
- **Following yourself** answers `422`.

| Field               | Rule                                             |
| ------------------- | ------------------------------------------------ |
| Request body        | up to 256 KB                                     |
| Username            | 3 to 30 characters, letters, digits, `_` and `-` |
| Email               | valid format, up to 254 characters               |
| Password            | 8 to 72 characters and at most 72 bytes          |
| Bio                 | up to 300 characters                             |
| Image               | `https` URL, up to 2048 characters               |
| Article title       | up to 100 characters                             |
| Article description | up to 200 characters                             |
| Article body        | up to 20000 characters                           |
| Comment body        | up to 1000 characters                            |

## Testing

| Layer        | Command                  | What it proves                                                       |
| ------------ | ------------------------ | -------------------------------------------------------------------- |
| Unit         | `make test/unit`         | Domain, use cases, JWT, bcrypt, middleware and configuration, no I/O |
| Architecture | `make test/architecture` | Layers and modules respect the dependency rules                      |
| Integration  | `make test/integration`  | Full HTTP flows and migrations on a real SQL Server                  |

Integration tests need a SQL Server instance where the login can create and drop databases. They read `TEST_DATABASE_URL` and default to the local SQL Server from the quick start, always connecting through `master`. The suite creates its own temporary databases, runs the migrations and drops them when it finishes.

```sh
TEST_DATABASE_URL="sqlserver://sa:Dev_Pass123@localhost:1433/master?trustServerCertificate=true" dotnet test tests/Conduit.IntegrationTests
```

## CI

`.github/workflows/ci.yml` runs on pushes to `main` and on pull requests, with three jobs in sequence:

1. **conduit code build**: restores and compiles the solution with the .NET 10 SDK.
2. **conduit code tests**: unit, architecture and integration suites against a SQL Server 2022 service.
3. **conduit docker container**: builds the image, starts the Compose stack, then checks `/health`, registers a user, reads `/tags` and runs the seed.

`.github/workflows/commitlint.yml` is a separate workflow that lints commit messages on pull requests, configured in `.commitlintrc.json`.

## Limits

- The API needs a running SQL Server 2017 or newer. The queries use `STRING_AGG`, `OPENJSON` and `OFFSET ... FETCH`, which need compatibility level 130 or higher.
- Events are delivered in-process, so the app runs as a single instance.
- Logs go to the console only. Collect them from the container output.
- Access tokens are stateless and live for `AUTH_TOKEN_TTL`. There is no revocation.
- There is no endpoint to delete an account, as the contract does not define one.
- Data from a PostgreSQL version of this backend is not migrated. Start with an empty SQL Server database, because the scripts are written in T-SQL and no tool moves data.
- The Compose files in `conduit-docker` run SQL Server 2022. The backend connects as `sa`, `DB_PASSWORD` is the `sa` password, and `make docker/env` generates it so that it meets the SQL Server password policy. The container health check creates the database.

## References

- [ASP.NET Core](https://learn.microsoft.com/aspnet/core)
- [Microsoft.Data.SqlClient](https://learn.microsoft.com/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace) · [SQL Server](https://learn.microsoft.com/sql/sql-server/)
- [Microsoft.IdentityModel](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet)
- [BCrypt.Net-Next](https://github.com/BcryptNet/bcrypt.net)
- [xUnit](https://xunit.net)
- [NetArchTest](https://github.com/BenMorris/NetArchTest)
- [RealWorld specification](https://realworld-docs.netlify.app)
- [Docker multi-stage builds](https://docs.docker.com/build/building/multi-stage/)
