# syntax=docker/dockerfile:1
# Build from the repository root:
#   docker build -f deploy/docker/Dockerfile -t shop:dev .
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /source

# Props first for better layer caching (global.json intentionally not copied:
# the image SDK version may differ from the developer pin).
COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/

RUN dotnet restore "src/Host/Shop.Host/Shop.Host.csproj"
RUN dotnet publish "src/Host/Shop.Host/Shop.Host.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Shop.Host.dll"]
