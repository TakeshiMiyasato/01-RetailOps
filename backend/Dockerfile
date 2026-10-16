# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the dependency layer is cached across code-only changes.
COPY RetailOps.slnx ./
COPY src/RetailOps.Domain/RetailOps.Domain.csproj src/RetailOps.Domain/
COPY src/RetailOps.Application/RetailOps.Application.csproj src/RetailOps.Application/
COPY src/RetailOps.Infrastructure/RetailOps.Infrastructure.csproj src/RetailOps.Infrastructure/
COPY src/RetailOps.Api/RetailOps.Api.csproj src/RetailOps.Api/
RUN dotnet restore src/RetailOps.Api/RetailOps.Api.csproj

COPY src/ src/
RUN dotnet publish src/RetailOps.Api/RetailOps.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "RetailOps.Api.dll"]
