# Basimage för körningx
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Byggstegen
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy MSBuild configuration files first
COPY ["Directory.Build.props", "./"]
COPY ["Directory.Packages.props", "./"]
COPY ["AccountingAgent.sln", "./"]
COPY ["src/Agent/Agent.csproj", "src/Agent/"]
COPY ["src/ServiceDefaults/ServiceDefaults.csproj", "src/ServiceDefaults/"]
COPY ["src/Shared/Shared.csproj", "src/Shared/"]

# Detta vill jag ska vara dynamiskt
COPY ["src/DateTimePlugin/DateTimePlugin.csproj", "src/DateTimePlugin/"]
COPY ["src/DropboxPlugin/DropboxPlugin.csproj", "src/DropboxPlugin/"]
COPY ["src/DocumentInterpreterPlugin/DocumentInterpreterPlugin.csproj", "src/DocumentInterpreterPlugin/"]
COPY ["src/FortnoxPlugin/FortnoxPlugin.csproj", "src/FortnoxPlugin/"]
COPY ["src/GoogleWorkspacePlugin/GoogleWorkspacePlugin.csproj", "src/GoogleWorkspacePlugin/"]
COPY ["src/GoogleSearchPlugin/GoogleSearchPlugin.csproj", "src/GoogleSearchPlugin/"]
COPY ["src/MailPlugin/MailPlugin.csproj", "src/MailPlugin/"]
COPY ["src/MicrosoftOfficePlugin/MicrosoftOfficePlugin.csproj", "src/MicrosoftOfficePlugin/"]
COPY ["src/OpenCagePlugin/OpenCagePlugin.csproj", "src/OpenCagePlugin/"]
COPY ["src/OpenMeteoPlugin/OpenMeteoPlugin.csproj", "src/OpenMeteoPlugin/"]
COPY ["src/SlackPlugin/SlackPlugin.csproj", "src/SlackPlugin/"]
COPY ["src/SmsPlugin/SmsPlugin.csproj", "src/SmsPlugin/"]
COPY ["src/WhatsAppPlugin/WhatsAppPlugin.csproj", "src/WhatsAppPlugin/"]

RUN dotnet restore "src/Agent/Agent.csproj"
COPY . .
RUN dotnet build "src/Agent/Agent.csproj" -c Release -o /app/build

# Publish-stegs
FROM build AS publish
RUN dotnet publish "src/Agent/Agent.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Slutlig images
FROM base AS final
WORKDIR /app

# Skapa mappar
RUN mkdir -p /config /workflows /tempfiles

# Kopiera app-filerx
COPY --from=publish /app/publish .

# Kopiera konfigurationsfiler
COPY ["src/Agent/systemprompt.txt", "/config/systemprompt.txt"]
COPY ["src/Agent/users.json", "/config/users.json"]
COPY ["src/Agent/plugins.json", "/config/plugins.json"]


# Skapa tom historikfil
RUN echo '[]' > /app/history.json

# Ge rätt ägarskap och skrivbehörighet till historik
RUN chmod 777 /app/history.json

# Environment variables — defaults for local container runs (DBs on /config).
# Production deploy compose overrides Sqlite__UseWal=false because /config is NFS.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS="http://+:80" \
    ISA_TEMPFILES_PATH=/tempfiles \
    ISA_WORKFLOWS_PATH=/workflows \
    ISA_LOGS_PATH=/app \
    ISA_CONFIG_PATH=/config \
    ISA_SYSTEM_PROMPT_PATH=/config/systemprompt.txt \
    ISA_HISTORY_PATH=/app/history.json \
    ConnectionStrings__DefaultConnection="Data Source=/config/agent.db" \
    ConnectionStrings__MemoryConnection="Data Source=/config/memory.db" \
    ConnectionStrings__FilesConnection="Data Source=/config/files.db"

# Healthcheck hits /health/db so the orchestrator notices when the DB connection pool
# is broken even though the process is still answering HTTP. wget is part of aspnet base.
HEALTHCHECK --interval=10s --timeout=3s --start-period=30s --retries=3 \
  CMD wget -qO- http://localhost:80/health/db || exit 1

ENTRYPOINT ["dotnet", "Agent.dll", "--mcp-server"]
