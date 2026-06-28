# WebChat Dockerfilex
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Build imageas
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file and project files for restorez
COPY ["AccountingAgent.sln", "./"]
COPY ["src/WebChat/WebChat.csproj", "src/WebChat/"]
COPY ["src/ServiceDefaults/ServiceDefaults.csproj", "src/ServiceDefaults/"]
COPY ["src/Shared/Shared.csproj", "src/Shared/"]

# Restore packages
RUN dotnet restore "src/WebChat/WebChat.csproj"

# Copy the source codex
COPY . .

# Build the WebChat project
RUN dotnet build "src/WebChat/WebChat.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "src/WebChat/WebChat.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app

# Create directory for persistent data
RUN mkdir -p /data/logs /data/config /config && \
    chmod 777 /config

# Copy published output
COPY --from=publish /app/publish .

# Set environment variable
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS="http://+:80;https://+:443" \
    ISA_LOGS_PATH=/app \
    ISA_CONFIG_PATH=/config

ENTRYPOINT ["dotnet", "WebChat.dll"]
