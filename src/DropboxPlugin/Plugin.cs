using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Dropbox.Api;
using Dropbox.Api.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Shared.Models;
using Shared.Services;

namespace Dropbox;

/// <summary>
/// Dropbox plugin för filhantering
/// </summary>
public class Plugin
{
    private readonly ILogger<Plugin> _logger;
    private readonly DropboxClient _dropboxClient;

    /// <summary>
    /// Static service provider for accessing DI services (set by Agent during startup)
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Creates a new instance of DropboxPlugin from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing Dropbox settings</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger = logger ?? NullLogger<Plugin>.Instance;

        var appKey =
            configuration["Dropbox:AppKey"]
            ?? throw new ArgumentNullException("Dropbox:AppKey configuration is missing");
        var appSecret =
            configuration["Dropbox:AppSecret"]
            ?? throw new ArgumentNullException("Dropbox:AppSecret configuration is missing");
        var refreshToken =
            configuration["Dropbox:RefreshToken"]
            ?? throw new ArgumentNullException("Dropbox:RefreshToken configuration is missing");

        var dropboxConfig = new DropboxClientConfig("AccountingAgent/1.0") { HttpClient = new HttpClient() };

        _dropboxClient = new DropboxClient(refreshToken, appKey, appSecret, dropboxConfig);

        _logger.LogInformation("Dropbox plugin initialized with AppKey/Secret/RefreshToken");
    }

    /// <summary>
    /// Listar filer i en mapp
    /// </summary>
    /// <param name="path">Sökväg till mappen (t.ex. /Documents)</param>
    /// <returns>Lista över filer</returns>
    [KernelFunction]
    [Description("Listar filer i en Dropbox-mapp")]
    public async Task<string> ListFiles(
        [Description("Sökväg till mappen (t.ex. /Documents)")] string path = ""
    )
    {
        try
        {
            _logger.LogInformation("Listar filer i Dropbox-mapp: {Path}", path);

            var folderPath = string.IsNullOrEmpty(path) ? "" : path;
            var list = await _dropboxClient.Files.ListFolderAsync(folderPath);

            if (list.Entries.Count > 0)
            {
                var fileList = list.Entries.Select(entry =>
                {
                    var type = entry.IsFile ? "Fil" : "Mapp";
                    var size = entry.IsFile ? $" ({entry.AsFile.Size} bytes)" : "";
                    return $"- {entry.Name} ({type}){size}";
                });

                var response =
                    $"SUCCESS: Hittade {list.Entries.Count} objekt:\n"
                    + string.Join("\n", fileList);
                _logger.LogInformation("Listade {Count} objekt framgångsrikt", list.Entries.Count);
                return response;
            }
            else
            {
                _logger.LogInformation("Inga filer hittades");
                return "SUCCESS: Inga filer hittades i mappen";
            }
        }
        catch (Dropbox.Api.AuthException authEx)
            when (authEx.Message.Contains("expired_access_token"))
        {
            _logger.LogError(authEx, "Dropbox access token har gått ut");
            return "ERROR: Dropbox access token har gått ut. "
                + "För att få ett nytt token: "
                + "1. Gå till https://www.dropbox.com/developers/apps "
                + "2. Välj din app "
                + "3. Gå till 'Settings' > 'OAuth 2' > 'Generated access token' "
                + "4. Klicka 'Generate' för att få ett nytt token "
                + "5. Uppdatera appsettings.json med det nya tokenet";
        }
        catch (Dropbox.Api.AuthException authEx)
        {
            _logger.LogError(authEx, "Dropbox autentiseringsfel");
            return $"ERROR: Dropbox autentiseringsfel: {authEx.Message}. "
                + "Kontrollera att access token är korrekt i appsettings.json.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid listning av filer");
            return $"ERROR: Fel vid listning av filer: {ex.Message}";
        }
    }

    /// <summary>
    /// Laddar upp en fil till Dropbox
    /// </summary>
    /// <param name="fileName">Namn på filen</param>
    /// <param name="content">Filinnehåll som base64-sträng</param>
    /// <param name="path">Sökväg där filen ska sparas (t.ex. /Documents/)</param>
    /// <returns>Resultat av uppladdningen</returns>
    [KernelFunction]
    [Description("Laddar upp en fil till Dropbox")]
    public async Task<string> UploadFile(
        [Description("Namn på filen")] string fileName,
        [Description("Filinnehåll som base64-sträng")] string content,
        [Description("Sökväg där filen ska sparas (t.ex. /Documents/)")] string path = "/"
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "ERROR: Filnamn saknas";
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return "ERROR: Filinnehåll saknas";
            }

            _logger.LogInformation("Laddar upp fil till Dropbox: {FileName}", fileName);

            var fileBytes = Convert.FromBase64String(content);
            var fullPath = $"{path.TrimEnd('/')}/{fileName}";

            using var stream = new MemoryStream(fileBytes);
            var uploaded = await _dropboxClient.Files.UploadAsync(
                fullPath,
                WriteMode.Overwrite.Instance,
                body: stream
            );

            _logger.LogInformation("Fil uppladdad framgångsrikt: {Path}", uploaded.PathDisplay);
            return $"SUCCESS: Fil '{fileName}' uppladdad framgångsrikt till {uploaded.PathDisplay}";
        }
        catch (Dropbox.Api.AuthException authEx)
            when (authEx.Message.Contains("expired_access_token"))
        {
            _logger.LogError(authEx, "Dropbox access token har gått ut");
            return "ERROR: Dropbox access token har gått ut. "
                + "För att få ett nytt token: "
                + "1. Gå till https://www.dropbox.com/developers/apps "
                + "2. Välj din app "
                + "3. Gå till 'Settings' > 'OAuth 2' > 'Generated access token' "
                + "4. Klicka 'Generate' för att få ett nytt token "
                + "5. Uppdatera appsettings.json med det nya tokenet";
        }
        catch (Dropbox.Api.AuthException authEx)
        {
            _logger.LogError(authEx, "Dropbox autentiseringsfel");
            return $"ERROR: Dropbox autentiseringsfel: {authEx.Message}. "
                + "Kontrollera att access token är korrekt i appsettings.json.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid uppladdning av fil");
            return $"ERROR: Fel vid uppladdning av fil: {ex.Message}";
        }
    }

    /// <summary>
    /// Laddar ner en fil från Dropbox och sparar den i TempFiles för vidare bearbetning
    /// </summary>
    /// <param name="filePath">Fullständig sökväg till filen</param>
    /// <returns>Fil-ID för den nedladdade filen i TempFiles-systemet</returns>
    [KernelFunction]
    [Description("Laddar ner en fil från Dropbox och sparar den i TempFiles för vidare bearbetning")]
    public async Task<string> DownloadFile(
        [Description("Fullständig sökväg till filen (t.ex. /Documents/file.txt)")] string filePath
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "ERROR: Filsökväg saknas";
            }

            _logger.LogInformation("Laddar ner fil från Dropbox: {FilePath}", filePath);

            using var response = await _dropboxClient.Files.DownloadAsync(filePath);
            using var contentStream = await response.GetContentAsStreamAsync();

            var fileName = Path.GetFileName(filePath);

            // Use TempFilesService to save file with automatic cleanup and database tracking
            _logger.LogInformation("ServiceProvider status: {Status}", ServiceProvider != null ? "Available" : "NULL");
            if (ServiceProvider != null)
            {
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

                    var fileId = await tempFilesService.SaveFileAsync(
                        fileName,
                        contentStream,
                        GetContentTypeFromExtension(Path.GetExtension(fileName)),
                        "Dropbox",
                        filePath
                    );

                    _logger.LogInformation("Fil nedladdad och sparad via TempFilesService: {FilePath} -> FileID: {FileId}",
                        filePath, fileId);

                    return $"SUCCESS: Fil nedladdad framgångsrikt. File ID: {fileId}. Använd DocumentInterpreterPlugin med detta ID för att analysera filen.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save file via TempFilesService: {FilePath}", filePath);
                    return $"ERROR: Misslyckades att spara fil via TempFilesService: {ex.Message}";
                }
            }
            else
            {
                _logger.LogError("ServiceProvider not available, cannot use TempFilesService");
                return "ERROR: TempFilesService inte tillgänglig";
            }
        }
        catch (Dropbox.Api.AuthException authEx)
            when (authEx.Message.Contains("expired_access_token"))
        {
            _logger.LogError(authEx, "Dropbox access token har gått ut");
            return "ERROR: Dropbox access token har gått ut. "
                + "För att få ett nytt token: "
                + "1. Gå till https://www.dropbox.com/developers/apps "
                + "2. Välj din app "
                + "3. Gå till 'Settings' > 'OAuth 2' > 'Generated access token' "
                + "4. Klicka 'Generate' för att få ett nytt token "
                + "5. Uppdatera appsettings.json med det nya tokenet";
        }
        catch (Dropbox.Api.AuthException authEx)
        {
            _logger.LogError(authEx, "Dropbox autentiseringsfel");
            return $"ERROR: Dropbox autentiseringsfel: {authEx.Message}. "
                + "Kontrollera att access token är korrekt i appsettings.json.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid nedladdning av fil");
            return $"ERROR: Fel vid nedladdning av fil: {ex.Message}";
        }
    }

    /// <summary>
    /// Skapar en ny mapp i Dropbox
    /// </summary>
    /// <param name="folderPath">Fullständig sökväg för den nya mappen (t.ex. /Documents/NewFolder)</param>
    /// <returns>Resultat av skapandet</returns>
    [KernelFunction]
    [Description("Skapar en ny mapp i Dropbox")]
    public async Task<string> CreateFolder(
        [Description("Fullständig sökväg för den nya mappen (t.ex. /Documents/NewFolder)")]
            string folderPath
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return "ERROR: Mappsökväg saknas";
            }

            _logger.LogInformation("Skapar mapp i Dropbox: {FolderPath}", folderPath);

            var folder = await _dropboxClient.Files.CreateFolderV2Async(folderPath);

            _logger.LogInformation(
                "Mapp skapad framgångsrikt: {Path}",
                folder.Metadata.PathDisplay
            );
            return $"SUCCESS: Mapp skapad framgångsrikt: {folder.Metadata.PathDisplay}";
        }
        catch (Dropbox.Api.AuthException authEx)
            when (authEx.Message.Contains("expired_access_token"))
        {
            _logger.LogError(authEx, "Dropbox access token har gått ut");
            return "ERROR: Dropbox access token har gått ut. "
                + "För att få ett nytt token: "
                + "1. Gå till https://www.dropbox.com/developers/apps "
                + "2. Välj din app "
                + "3. Gå till 'Settings' > 'OAuth 2' > 'Generated access token' "
                + "4. Klicka 'Generate' för att få ett nytt token "
                + "5. Uppdatera appsettings.json med det nya tokenet";
        }
        catch (Dropbox.Api.AuthException authEx)
        {
            _logger.LogError(authEx, "Dropbox autentiseringsfel");
            return $"ERROR: Dropbox autentiseringsfel: {authEx.Message}. "
                + "Kontrollera att access token är korrekt i appsettings.json.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid skapande av mapp");
            return $"ERROR: Fel vid skapande av mapp: {ex.Message}";
        }
    }

    /// <summary>
    /// Läser innehållet i en fil från Dropbox (alias för DownloadFile)
    /// </summary>
    /// <param name="filePath">Fullständig sökväg till filen</param>
    /// <returns>Filinnehåll som base64-sträng</returns>
    [KernelFunction]
    [Description("Läser innehållet i en fil från Dropbox")]
    public async Task<string> ReadFile(
        [Description("Fullständig sökväg till filen (t.ex. /Documents/file.txt)")] string filePath
    )
    {
        return await DownloadFile(filePath);
    }

    /// <summary>
    /// Raderar en fil eller mapp från Dropbox
    /// </summary>
    /// <param name="path">Fullständig sökväg till filen eller mappen</param>
    /// <returns>Resultat av raderingen</returns>
    [KernelFunction]
    [Description("Raderar en fil eller mapp från Dropbox")]
    public async Task<string> DeleteFile(
        [Description("Fullständig sökväg till filen eller mappen")] string path
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "ERROR: Sökväg saknas";
            }

            _logger.LogInformation("Raderar fil/mapp från Dropbox: {Path}", path);

            var deleted = await _dropboxClient.Files.DeleteV2Async(path);

            _logger.LogInformation(
                "Fil/mapp raderad framgångsrikt: {Path}",
                deleted.Metadata.PathDisplay
            );
            return $"SUCCESS: Fil/mapp raderad framgångsrikt: {deleted.Metadata.PathDisplay}";
        }
        catch (Dropbox.Api.AuthException authEx)
            when (authEx.Message.Contains("expired_access_token"))
        {
            _logger.LogError(authEx, "Dropbox access token har gått ut");
            return "ERROR: Dropbox access token har gått ut. "
                + "För att få ett nytt token: "
                + "1. Gå till https://www.dropbox.com/developers/apps "
                + "2. Välj din app "
                + "3. Gå till 'Settings' > 'OAuth 2' > 'Generated access token' "
                + "4. Klicka 'Generate' för att få ett nytt token "
                + "5. Uppdatera appsettings.json med det nya tokenet";
        }
        catch (Dropbox.Api.AuthException authEx)
        {
            _logger.LogError(authEx, "Dropbox autentiseringsfel");
            return $"ERROR: Dropbox autentiseringsfel: {authEx.Message}. "
                + "Kontrollera att access token är korrekt i appsettings.json.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid radering av fil/mapp: {Path}", path);

            // FALLBACK: Skapa audit log-fil när radering misslyckas
            var fallbackResult = await CreateDropboxDeletionAuditLog(path, ex.Message);

            return $"ERROR: Radering misslyckades för '{path}'. {fallbackResult}";
        }
    }

    /// <summary>
    /// Skapar en audit log-fil när radering misslyckas (fallback) för Dropbox
    /// </summary>
    private async Task<string> CreateDropboxDeletionAuditLog(string targetPath, string errorMessage)
    {
        try
        {
            // Försök hämta information om filen som skulle raderas
            string fileName = System.IO.Path.GetFileName(targetPath) ?? "okänd fil";
            string fileType = "okänd";
            long fileSize = 0;

            try
            {
                var metadata = await _dropboxClient.Files.GetMetadataAsync(targetPath);
                if (metadata.IsFile)
                {
                    fileName = metadata.AsFile.Name;
                    fileType = "file";
                    fileSize = (long)metadata.AsFile.Size;
                }
                else if (metadata.IsFolder)
                {
                    fileName = metadata.AsFolder.Name;
                    fileType = "folder";
                }
            }
            catch
            {
                // Om vi inte kan hämta metadata, använd det vi har
            }

            // Skapa audit log data enligt standard JSON-format
            var auditData = new
            {
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                operation = "DELETE_FAILED",
                target_path = targetPath,
                target_name = fileName,
                target_type = fileType,
                target_size = fileSize,
                error_message = errorMessage,
                attempted_by = "Dropbox Plugin Service Account",
                service = "Dropbox Plugin",
                reason = "Deletion operation failed - creating audit trail for troubleshooting",
            };

            var auditJson = System.Text.Json.JsonSerializer.Serialize(
                auditData,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
            );

            // Skapa audit log-filnamn enligt standard
            var auditFileName = $"{fileName}.deletion-log.{DateTime.Now:yyyyMMdd-HHmmss}.json";
            var auditPath = System.IO.Path.GetDirectoryName(targetPath) ?? "/";
            if (!auditPath.EndsWith("/"))
                auditPath += "/";
            var fullAuditPath = auditPath + auditFileName;

            // Ladda upp audit log-filen till samma mapp som den misslyckade filen
            var auditContentBytes = System.Text.Encoding.UTF8.GetBytes(auditJson);
            using var stream = new MemoryStream(auditContentBytes);

            var uploaded = await _dropboxClient.Files.UploadAsync(
                fullAuditPath,
                Dropbox.Api.Files.WriteMode.Add.Instance,
                autorename: true,
                body: stream
            );

            _logger.LogInformation(
                "Audit log skapad för misslyckad Dropbox-radering: {AuditPath}",
                uploaded.PathDisplay
            );

            return $"Audit log skapad: '{uploaded.PathDisplay}' med information om den misslyckade raderingen.";
        }
        catch (Exception auditEx)
        {
            _logger.LogError(auditEx, "Kunde inte skapa audit log för misslyckad Dropbox-radering");
            return "Dessutom misslyckades skapandet av audit log.";
        }
    }

    /// <summary>
    /// Flyttar eller kopierar en fil eller mapp till en ny plats
    /// </summary>
    /// <param name="fromPath">Nuvarande sökväg till filen eller mappen</param>
    /// <param name="toPath">Ny sökväg för filen eller mappen</param>
    /// <returns>Resultat av flytten</returns>
    [KernelFunction]
    [Description("Flyttar en fil eller mapp till en ny plats i Dropbox")]
    public async Task<string> MoveFile(
        [Description("Nuvarande sökväg till filen eller mappen")] string fromPath,
        [Description("Ny sökväg för filen eller mappen")] string toPath
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fromPath))
            {
                return "ERROR: Från-sökväg saknas";
            }

            if (string.IsNullOrWhiteSpace(toPath))
            {
                return "ERROR: Till-sökväg saknas";
            }

            _logger.LogInformation(
                "Flyttar fil/mapp i Dropbox: {FromPath} till {ToPath}",
                fromPath,
                toPath
            );

            var moved = await _dropboxClient.Files.MoveV2Async(fromPath, toPath);

            _logger.LogInformation(
                "Fil/mapp flyttad framgångsrikt från {FromPath} till {ToPath}",
                fromPath,
                moved.Metadata.PathDisplay
            );
            return $"SUCCESS: Fil/mapp flyttad framgångsrikt från {fromPath} till {moved.Metadata.PathDisplay}";
        }
        catch (Dropbox.Api.AuthException authEx)
            when (authEx.Message.Contains("expired_access_token"))
        {
            _logger.LogError(authEx, "Dropbox access token har gått ut");
            return "ERROR: Dropbox access token har gått ut. "
                + "För att få ett nytt token: "
                + "1. Gå till https://www.dropbox.com/developers/apps "
                + "2. Välj din app "
                + "3. Gå till 'Settings' > 'OAuth 2' > 'Generated access token' "
                + "4. Klicka 'Generate' för att få ett nytt token "
                + "5. Uppdatera appsettings.json med det nya tokenet";
        }
        catch (Dropbox.Api.AuthException authEx)
        {
            _logger.LogError(authEx, "Dropbox autentiseringsfel");
            return $"ERROR: Dropbox autentiseringsfel: {authEx.Message}. "
                + "Kontrollera att access token är korrekt i appsettings.json.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid flytt av fil/mapp");
            return $"ERROR: Fel vid flytt av fil/mapp: {ex.Message}";
        }
    }

    /// <summary>
    /// Byter namn på en fil eller mapp (genom att flytta till samma mapp med nytt namn)
    /// </summary>
    /// <param name="currentPath">Nuvarande sökväg till filen eller mappen</param>
    /// <param name="newName">Nytt namn (utan sökväg)</param>
    /// <returns>Resultat av namnbytet</returns>
    [KernelFunction]
    [Description("Byter namn på en fil eller mapp i Dropbox")]
    public async Task<string> RenameFile(
        [Description("Nuvarande sökväg till filen eller mappen")] string currentPath,
        [Description("Nytt namn (utan sökväg)")] string newName
    )
    {
        try
        {
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return "ERROR: Nuvarande sökväg saknas";
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                return "ERROR: Nytt namn saknas";
            }

            _logger.LogInformation(
                "Byter namn på fil/mapp i Dropbox: {CurrentPath} till {NewName}",
                currentPath,
                newName
            );

            // Extrahera mapp-delen från den nuvarande sökvägen
            var lastSlashIndex = currentPath.LastIndexOf('/');
            var parentPath = lastSlashIndex > 0 ? currentPath.Substring(0, lastSlashIndex) : "";
            var newPath = string.IsNullOrEmpty(parentPath)
                ? $"/{newName}"
                : $"{parentPath}/{newName}";

            var moved = await _dropboxClient.Files.MoveV2Async(currentPath, newPath);

            _logger.LogInformation(
                "Fil/mapp har bytt namn framgångsrikt till: {NewPath}",
                moved.Metadata.PathDisplay
            );
            return $"SUCCESS: Fil/mapp har bytt namn framgångsrikt till: {moved.Metadata.PathDisplay}";
        }
        catch (Dropbox.Api.AuthException authEx)
            when (authEx.Message.Contains("expired_access_token"))
        {
            _logger.LogError(authEx, "Dropbox access token har gått ut");
            return "ERROR: Dropbox access token har gått ut. "
                + "För att få ett nytt token: "
                + "1. Gå till https://www.dropbox.com/developers/apps "
                + "2. Välj din app "
                + "3. Gå till 'Settings' > 'OAuth 2' > 'Generated access token' "
                + "4. Klicka 'Generate' för att få ett nytt token "
                + "5. Uppdatera appsettings.json med det nya tokenet";
        }
        catch (Dropbox.Api.AuthException authEx)
        {
            _logger.LogError(authEx, "Dropbox autentiseringsfel");
            return $"ERROR: Dropbox autentiseringsfel: {authEx.Message}. "
                + "Kontrollera att access token är korrekt i appsettings.json.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel vid namnbyte av fil/mapp");
            return $"ERROR: Fel vid namnbyte av fil/mapp: {ex.Message}";
        }
    }

    /// <summary>
    /// Diagnostisk funktion för att troubleshoota Dropbox-anslutning
    /// </summary>
    /// <returns>Fullständig diagnostisk rapport</returns>
    [KernelFunction]
    [Description("Kör en komplett diagnostisk test av Dropbox-anslutningen för felsökning")]
    public async Task<string> DiagnoseDropboxConnection()
    {
        try
        {
            _logger.LogInformation("Startar Dropbox diagnostisk test");

            var diagnosticResults = new List<string> { "=== DROPBOX DIAGNOSTISK RAPPORT ===\n" };

            // Test 1: Basic connection
            try
            {
                var account = await _dropboxClient.Users.GetCurrentAccountAsync();

                diagnosticResults.Add($"✅ ANSLUTNING OK");
                diagnosticResults.Add($"   Användare: {account.Name.DisplayName}");
                diagnosticResults.Add($"   Email: {account.Email}");
                diagnosticResults.Add($"   Konto-typ: {account.AccountType}");
                diagnosticResults.Add($"   Land: {account.Country ?? "Okänt"}");
                diagnosticResults.Add("");
            }
            catch (Dropbox.Api.AuthException authEx)
                when (authEx.Message.Contains("expired_access_token"))
            {
                diagnosticResults.Add($"❌ ACCESS TOKEN HAR GÅTT UT");
                diagnosticResults.Add($"   Fel: {authEx.Message}");
                diagnosticResults.Add(
                    $"   Lösning: Generera nytt token från Dropbox Developer Console"
                );
                diagnosticResults.Add("");
                return GenerateTokenRenewalInstructions(diagnosticResults);
            }
            catch (Exception ex)
            {
                diagnosticResults.Add($"❌ ANSLUTNINGSFEL: {ex.Message}\n");
                return string.Join("\n", diagnosticResults);
            }

            // Test 2: List root files
            try
            {
                var rootFiles = await _dropboxClient.Files.ListFolderAsync("");

                diagnosticResults.Add($"📁 ROOT-FILER ({rootFiles.Entries.Count} st):");
                if (rootFiles.Entries.Count > 0)
                {
                    foreach (var entry in rootFiles.Entries.Take(5))
                    {
                        var type = entry.IsFile ? "Fil" : "Mapp";
                        var size = entry.IsFile ? $" ({entry.AsFile.Size} bytes)" : "";
                        diagnosticResults.Add($"   - {entry.Name} ({type}){size}");
                    }
                    if (rootFiles.Entries.Count > 5)
                    {
                        diagnosticResults.Add($"   ... och {rootFiles.Entries.Count - 5} till");
                    }
                }
                else
                {
                    diagnosticResults.Add("   📂 Inga filer i root-mappen");
                }
                diagnosticResults.Add("");
            }
            catch (Exception ex)
            {
                diagnosticResults.Add($"❌ KUNDE INTE LISTA ROOT-FILER: {ex.Message}\n");
            }

            // Test 3: Check space usage
            try
            {
                var spaceUsage = await _dropboxClient.Users.GetSpaceUsageAsync();

                diagnosticResults.Add($"💾 LAGRINGSUTRYMME:");
                diagnosticResults.Add($"   Använt: {spaceUsage.Used} bytes");
                if (spaceUsage.Allocation.IsIndividual)
                {
                    // Note: Individual allocation details may vary by API version
                    diagnosticResults.Add($"   Konto-typ: Individual");
                }
                else if (spaceUsage.Allocation.IsTeam)
                {
                    diagnosticResults.Add($"   Konto-typ: Team");
                }
                diagnosticResults.Add("");
            }
            catch (Exception ex)
            {
                diagnosticResults.Add($"❌ KUNDE INTE HÄMTA LAGRINGSINFORMATION: {ex.Message}\n");
            }

            diagnosticResults.Add("=== REKOMMENDATIONER ===");
            diagnosticResults.Add("1. Anslutningen fungerar korrekt");
            diagnosticResults.Add("2. Access token är giltigt och fungerar");
            diagnosticResults.Add("3. För att ladda upp filer: Använd UploadFile-funktionen");
            diagnosticResults.Add("4. För att skapa mappar: Använd CreateFolder-funktionen");

            _logger.LogInformation("Dropbox diagnostisk test slutförd framgångsrikt");
            return string.Join("\n", diagnosticResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fel under Dropbox diagnostisk test");
            return $"ERROR: Diagnostisk test misslyckades: {ex.Message}";
        }
    }

    /// <summary>
    /// Genererar instruktioner för att förnya Dropbox access token
    /// </summary>
    private string GenerateTokenRenewalInstructions(List<string> existingResults)
    {
        existingResults.AddRange(
            new[]
            {
                "=== LÖSNING: FÖRNYA ACCESS TOKEN ===",
                "",
                "Ditt Dropbox access token har gått ut. Följ dessa steg:",
                "",
                "1. Gå till Dropbox Developer Console:",
                "   https://www.dropbox.com/developers/apps",
                "",
                "2. Logga in med ditt Dropbox-konto",
                "",
                "3. Välj din app (eller skapa en ny om du inte har någon)",
                "",
                "4. Gå till 'Settings' > 'OAuth 2'",
                "",
                "5. Under 'Generated access token', klicka 'Generate'",
                "",
                "6. Kopiera det nya tokenet",
                "",
                "7. Uppdatera appsettings.json:",
                "   \"Dropbox\": {",
                "     \"AccessToken\": \"DIN_NYA_TOKEN_HÄR\"",
                "   }",
                "",
                "8. Starta om applikationen",
                "",
                "OBS: Access tokens har begränsad livslängd av säkerhetsskäl.",
                "Du kan behöva upprepa detta process regelbundet.",
            }
        );

        return string.Join("\n", existingResults);
    }

    /// <summary>
    /// Gets the MIME content type from file extension
    /// </summary>
    /// <param name="extension">File extension (with or without dot)</param>
    /// <returns>MIME content type</returns>
    private static string GetContentTypeFromExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return "application/octet-stream";

        extension = extension.ToLowerInvariant();
        if (!extension.StartsWith("."))
            extension = "." + extension;

        return extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }


}
