# syntax=docker/dockerfile:1.7-labs

# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY --parents ./src/**/*.csproj .
RUN dotnet restore src/TheHunt.Bot

# copy everything else and build app
COPY --parents ./src/**/ .
RUN dotnet publish -c Release -o /app --no-restore src/TheHunt.Bot

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "TheHunt.Bot.dll"]
