FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY PACE.Domain/PACE.Domain.csproj PACE.Domain/
COPY PACE.Application/PACE.Application.csproj PACE.Application/
COPY PACE.Infrastructure/PACE.Infrastructure.csproj PACE.Infrastructure/
COPY PACE.API/PACE.API.csproj PACE.API/
RUN dotnet restore PACE.API/PACE.API.csproj
COPY PACE.Domain/ PACE.Domain/
COPY PACE.Application/ PACE.Application/
COPY PACE.Infrastructure/ PACE.Infrastructure/
COPY PACE.API/ PACE.API/
RUN dotnet publish PACE.API/PACE.API.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends git ca-certificates \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Analysis__CloneRoot=/tmp/pace_repos
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "PACE.API.dll"]
