FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /build
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Conduit.Host/Conduit.Host.csproj src/Conduit.Host/
COPY src/Conduit.SharedKernel/Conduit.SharedKernel.csproj src/Conduit.SharedKernel/
COPY src/Modules/Users/Conduit.Modules.Users/Conduit.Modules.Users.csproj src/Modules/Users/Conduit.Modules.Users/
COPY src/Modules/Articles/Conduit.Modules.Articles/Conduit.Modules.Articles.csproj src/Modules/Articles/Conduit.Modules.Articles/
COPY src/Modules/Comments/Conduit.Modules.Comments/Conduit.Modules.Comments.csproj src/Modules/Comments/Conduit.Modules.Comments/
RUN dotnet restore src/Conduit.Host/Conduit.Host.csproj
COPY src ./src
RUN dotnet publish src/Conduit.Host/Conduit.Host.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runner
RUN apk add --no-cache icu-libs
ENV APP_HOST=0.0.0.0 \
    APP_PORT=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
WORKDIR /app
COPY --from=build --chown=app:app /out ./
USER app
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=60s --retries=3 \
  CMD wget -qO- http://127.0.0.1:8080/health || exit 1
ENTRYPOINT ["dotnet", "Conduit.Host.dll"]
