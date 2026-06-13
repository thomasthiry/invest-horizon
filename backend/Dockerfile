FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY InvestHorizon.sln .
COPY src/InvestHorizon.Domain/InvestHorizon.Domain.csproj src/InvestHorizon.Domain/
COPY src/InvestHorizon.Application/InvestHorizon.Application.csproj src/InvestHorizon.Application/
COPY src/InvestHorizon.Infrastructure/InvestHorizon.Infrastructure.csproj src/InvestHorizon.Infrastructure/
COPY src/InvestHorizon.Api/InvestHorizon.Api.csproj src/InvestHorizon.Api/
COPY tests/InvestHorizon.Tests/InvestHorizon.Tests.csproj tests/InvestHorizon.Tests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/InvestHorizon.Api/InvestHorizon.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "InvestHorizon.Api.dll"]
