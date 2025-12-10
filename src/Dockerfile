FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["CharonDataIngestor.sln", "."]
COPY ["CharonDataIngestor/CharonDataIngestor.csproj", "CharonDataIngestor/"]

# Restore dependencies
RUN dotnet restore "CharonDataIngestor/CharonDataIngestor.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/CharonDataIngestor"
RUN dotnet build "CharonDataIngestor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CharonDataIngestor.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create logs directory
RUN mkdir -p /app/logs

ENTRYPOINT ["dotnet", "CharonDataIngestor.dll"]

