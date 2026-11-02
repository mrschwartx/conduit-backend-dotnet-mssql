PROJECT = src/Conduit.Host
SOLUTION = ConduitBackend.slnx

.PHONY: env dev restore build run seed test test/unit test/architecture test/integration verify clean

env:
	@test -f .env || { \
		sed -e "s|^AUTH_SECRET=.*|AUTH_SECRET=$$(openssl rand -hex 32)|" .env.example > .env && echo "Created .env with a generated AUTH_SECRET"; \
	}

dev: env
	set -a; . ./.env; set +a; dotnet run --project $(PROJECT)

restore:
	dotnet restore $(SOLUTION)

build: restore
	dotnet build $(SOLUTION) -c Release --no-restore

run: build env
	set -a; . ./.env; set +a; dotnet run --project $(PROJECT) -c Release --no-build

seed: env
	set -a; . ./.env; set +a; dotnet run --project $(PROJECT) -- --seed

test/unit:
	dotnet test tests/Conduit.UnitTests

test/architecture:
	dotnet test tests/Conduit.ArchitectureTests

test/integration:
	dotnet test tests/Conduit.IntegrationTests

test: test/unit test/architecture test/integration

verify: build test

clean:
	dotnet clean $(SOLUTION)