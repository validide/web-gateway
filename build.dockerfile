FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build-container
WORKDIR /app
COPY Directory.Build.props .
COPY src/WebGatewayService/WebGatewayService.csproj .
RUN dotnet restore

COPY src/WebGatewayService .
RUN dotnet publish -c Release -o artifacts --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:7.0-bullseye-slim AS runtime


# Full PGO
ENV DOTNET_TieredPGO 1
ENV DOTNET_TC_QuickJitForLoops 1
ENV DOTNET_ReadyToRun 0
ENV DOTNET_EnableDiagnostics=0

WORKDIR /app
COPY --from=build-container /app/artifacts ./

EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "WebGateway.WebGatewayService.dll"]
# ENTRYPOINT ["sh"]
