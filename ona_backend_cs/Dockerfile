# One image per service: docker build --build-arg SERVICE=Graph|Analysis|Insights|Identity .
# The image is only produced if the unit + architecture tests pass.

# ---- build ----
# Base images come from Docker Hub (Ubuntu + apt) because MCR's west-Europe endpoint served a
# bad TLS certificate. To use the official images again, replace the two `FROM ubuntu` stages with
# mcr.microsoft.com/dotnet/sdk:10.0 and mcr.microsoft.com/dotnet/aspnet:10.0 and drop the apt lines.
FROM ubuntu:26.04 AS build
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
RUN apt-get update \
    && apt-get install -y --no-install-recommends dotnet-sdk-10.0 ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY . .
RUN dotnet restore ONA.sln
RUN dotnet build ONA.sln -c Release --no-restore

# ---- test (integration tests need Docker and run in CI instead) ----
FROM build AS test
RUN dotnet test ONA.sln -c Release --no-build \
      --filter "FullyQualifiedName!~IntegrationTests" \
    && touch /tests-passed

# ---- publish (depends on the test stage) ----
FROM build AS publish
ARG SERVICE=Graph
COPY --from=test /tests-passed /tests-passed
RUN dotnet publish src/Services/${SERVICE}/ONA.${SERVICE}.Presentation/ONA.${SERVICE}.Presentation.csproj \
      -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM ubuntu:26.04 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends aspnetcore-runtime-10.0 ca-certificates \
    && rm -rf /var/lib/apt/lists/*
ARG SERVICE=Graph
ENV SERVICE=${SERVICE}
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER ubuntu
ENTRYPOINT ["sh", "-c", "exec dotnet ONA.${SERVICE}.Presentation.dll"]
