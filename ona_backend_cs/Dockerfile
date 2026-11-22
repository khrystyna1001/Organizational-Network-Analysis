# Graph API image. The image is only produced if the unit + architecture tests pass.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
COPY --from=test /tests-passed /tests-passed
RUN dotnet publish src/Services/Graph/ONA.Graph.Presentation/ONA.Graph.Presentation.csproj \
      -c Release --no-restore -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "ONA.Graph.Presentation.dll"]
