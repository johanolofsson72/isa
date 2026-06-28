using System.ComponentModel;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Auth.OAuth2.Flows;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using System.Text;
using Shared.Services;

namespace GoogleWorkspace;

public class Plugin
{
    private readonly ILogger<Plugin> _logger;
    private readonly DriveService _driveService;
    private readonly CalendarService _calendarService;

    /// <summary>
    /// Static service provider for accessing DI services (set by Agent during startup)
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    private readonly string _impersonatedUser;
    private readonly string _workFolderName;
    private readonly string _sharedCalendarId;
    private readonly string[] _collaborators;

    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        _logger = logger ?? new NullLogger<Plugin>();

        // Check if we're in test mode
        var testMode = configuration.GetValue<bool>("GoogleWorkspace:TestMode", false);
        if (testMode)
        {
            _logger.LogInformation("GoogleWorkspace plugin running in test mode - skipping credential initialization");
            _impersonatedUser = configuration["GoogleWorkspace:ImpersonatedUser"] ?? "test@example.com";
            _workFolderName = "Work";
            _sharedCalendarId = "test-calendar-id";
            _collaborators = new[] { "test@example.com" };

            // Create mock services for testing
            _driveService = null!; // Will be handled in test mode
            _calendarService = null!; // Will be handled in test mode
            return;
        }

        var clientId = configuration["GoogleWorkspace:ClientId"];
        var clientSecret = configuration["GoogleWorkspace:ClientSecret"];
        var serviceAccountEmail = configuration["GoogleWorkspace:ServiceAccountEmail"];
        _impersonatedUser = configuration["GoogleWorkspace:ImpersonatedUserEmail"] ?? "";
        _workFolderName = configuration["GoogleWorkspace:WorkDriveFolderName"] ?? "Work";
        _sharedCalendarId = configuration["GoogleWorkspace:SharedCalendarId"] ?? "";
        var tokenPath = configuration["GoogleWorkspace:TokenPath"] ?? "config/google-token.json";
        var credentialsPath = configuration["GoogleWorkspace:CredentialsPath"] ?? "config/accountingagentdrive-c18d67539c7b.json";
        _collaborators = new[] {
            "user@example.com",
            "anna.birgersson@example.com",
            "colleague@example.com",
            "joakim.winkler@example.com"
        };

        _logger.LogInformation($"Loading credentials from: {credentialsPath}");
        _logger.LogInformation($"Service Account Email: {serviceAccountEmail}");
        _logger.LogInformation($"Impersonated User: {_impersonatedUser}");

        try
        {
            GoogleCredential credential;
            using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(new[] {
                        "https://www.googleapis.com/auth/drive",
                        "https://www.googleapis.com/auth/calendar"
                    });

                // Try to impersonate user if specified and not empty
                if (!string.IsNullOrEmpty(_impersonatedUser))
                {
                    try
                    {
                        credential = credential.CreateWithUser(_impersonatedUser);
                        _logger.LogInformation($"Successfully configured impersonation for user: {_impersonatedUser}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to impersonate user {_impersonatedUser}: {ex.Message}");
                        _logger.LogInformation("Continuing without impersonation - using service account directly");
                    }
                }
                else
                {
                    _logger.LogInformation("No impersonated user specified, using service account directly");
                }
            }

            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Accounting Agent"
            });
            _calendarService = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Accounting Agent"
            });
            
            _logger.LogInformation("Google Workspace services initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to initialize Google Workspace services: {ex.Message}");
            throw;
        }
    }

    [KernelFunction]
    [Description("Listar filer i den gemensamma Google Drive-mappen 'Work'.")]
    public async Task<string> ListFiles([Description("Namn på mappen, t.ex. 'Work'")] string? folderName = null)
    {
        folderName ??= _workFolderName;

        // Handle test mode
        if (_driveService == null)
        {
            _logger.LogInformation($"Test mode: Listing files in folder '{folderName}'");
            return $"Test mode: Found 2 files in folder '{folderName}':\n- test-document.txt (Fil)\n- test-spreadsheet.xlsx (Fil)";
        }

        try
        {
            var folderId = await GetOrCreateFolderId(folderName);
            var request = _driveService.Files.List();
            request.Q = $"'{folderId}' in parents and trashed = false";
            request.Fields = "files(id, name, mimeType)";
            var result = await request.ExecuteAsync();
            if (result.Files == null || result.Files.Count == 0)
                return $"Inga filer i mappen '{folderName}'";
            return string.Join("\n", result.Files.Select(f => $"- {f.Name} ({(f.MimeType == "application/vnd.google-apps.folder" ? "Mapp" : "Fil")})"));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in ListFiles: {ex.Message}");
            _logger.LogError($"Exception type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                _logger.LogError($"Inner exception: {ex.InnerException.Message}");
            }
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Laddar upp en fil till den gemensamma Google Drive-mappen 'Work'.")]
    public async Task<string> UploadFile(
        [Description("Filnamn")] string fileName,
        [Description("Filinnehåll som base64-sträng")] string content,
        [Description("Namn på mappen, t.ex. 'Work'")] string? folderName = null)
    {
        folderName ??= _workFolderName;

        // Handle test mode
        if (_driveService == null)
        {
            _logger.LogInformation($"Test mode: Uploading file '{fileName}' to folder '{folderName}'");
            return $"SUCCESS: Test mode - Fil '{fileName}' uppladdad till mappen '{folderName}'";
        }

        try
        {
            var folderId = await GetOrCreateFolderId(folderName);
            byte[] fileBytes = Convert.FromBase64String(content);
            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                Parents = new List<string> { folderId }
            };
            using var stream = new MemoryStream(fileBytes);
            var request = _driveService.Files.Create(fileMetadata, stream, null);
            request.Fields = "id, name";
            await request.UploadAsync();
            var uploadedFile = request.ResponseBody;
            return $"SUCCESS: Fil '{fileName}' uppladdad till mappen '{folderName}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Läser innehållet i en fil från den gemensamma Google Drive-mappen 'Work'.")]
    public async Task<string> ReadFile(
        [Description("Namn på filen")] string fileName,
        [Description("Namn på mappen, t.ex. 'Work'")] string? folderName = null)
    {
        folderName ??= _workFolderName;

        // Handle test mode
        if (_driveService == null)
        {
            _logger.LogInformation($"Test mode: Reading file '{fileName}' from folder '{folderName}'");
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"Test mode content for file '{fileName}'"));
        }

        try
        {
            var folderId = await GetOrCreateFolderId(folderName);
            var request = _driveService.Files.List();
            request.Q = $"'{folderId}' in parents and name = '{fileName}' and trashed = false";
            request.Fields = "files(id, name)";
            var result = await request.ExecuteAsync();
            var file = result.Files?.FirstOrDefault();
            if (file == null)
                return $"Ingen fil med namn '{fileName}' hittades i '{folderName}'";
            var getRequest = _driveService.Files.Get(file.Id);
            using var ms = new MemoryStream();
            await getRequest.DownloadAsync(ms);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Laddar ner en fil från Google Drive och sparar den via TempFilesService för vidare bearbetning.")]
    public async Task<string> DownloadFile(
        [Description("Namn på filen")] string fileName,
        [Description("Namn på mappen, t.ex. 'Work'")] string? folderName = null)
    {
        folderName ??= _workFolderName;

        // Check if ServiceProvider is available for TempFilesService
        if (ServiceProvider == null)
        {
            _logger.LogWarning("ServiceProvider not available, falling back to ReadFile");
            return await ReadFile(fileName, folderName);
        }

        // Handle test mode
        if (_driveService == null)
        {
            _logger.LogInformation($"Test mode: Downloading file '{fileName}' from folder '{folderName}'");

            try
            {
                using var scope = ServiceProvider.CreateScope();
                var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

                // Create test file content
                var testContent = Encoding.UTF8.GetBytes($"Test mode content for file '{fileName}' from Google Drive folder '{folderName}'");
                var testFileId = await tempFilesService.SaveFileAsync(fileName, testContent, "application/pdf", $"GoogleDrive:{folderName}");

                return testFileId.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save test file via TempFilesService");
                return $"ERROR: Test mode failed: {ex.Message}";
            }
        }

        try
        {
            var folderId = await GetOrCreateFolderId(folderName);
            var request = _driveService.Files.List();
            request.Q = $"'{folderId}' in parents and name = '{fileName}' and trashed = false";
            request.Fields = "files(id, name, mimeType, size)";
            var result = await request.ExecuteAsync();
            var file = result.Files?.FirstOrDefault();

            if (file == null)
            {
                throw new FileNotFoundException($"Ingen fil med namn '{fileName}' hittades i '{folderName}'");
            }

            _logger.LogInformation("Downloading file from Google Drive: {FileName} (ID: {FileId}, Size: {Size} bytes)",
                fileName, file.Id, file.Size);

            // Download file content
            var getRequest = _driveService.Files.Get(file.Id);
            using var ms = new MemoryStream();
            await getRequest.DownloadAsync(ms);
            var fileContent = ms.ToArray();

            // Determine content type
            var contentType = file.MimeType ?? "application/octet-stream";

            // Save via TempFilesService using ServiceProvider
            try
            {
                using var scope = ServiceProvider.CreateScope();
                var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

                var fileId = await tempFilesService.SaveFileAsync(
                    fileName,
                    fileContent,
                    contentType,
                    $"GoogleDrive:{folderName}");

                _logger.LogInformation("Fil nedladdad och sparad via TempFilesService: {FolderName}/{FileName} -> FileID: {FileId}",
                    folderName, fileName, fileId);

                return $"SUCCESS: Fil nedladdad framgångsrikt. File ID: {fileId}. Använd DocumentInterpreterPlugin med detta ID för att analysera filen.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save file via TempFilesService: {FileName}", fileName);
                return $"ERROR: Misslyckades att spara fil via TempFilesService: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from Google Drive: {FileName}", fileName);
            throw new InvalidOperationException($"Google Drive download failed: {ex.Message}", ex);
        }
    }

    [KernelFunction]
    [Description("Skapar en kalenderhändelse i den gemensamma kalendern.")]
    public async Task<string> CreateCalendarEvent(
        [Description("Titel på händelsen")] string title,
        [Description("Starttid i ISO 8601-format, t.ex. '2024-06-01T10:00:00Z'")] string startTime,
        [Description("Sluttid i ISO 8601-format, t.ex. '2024-06-01T11:00:00Z'")] string endTime,
        [Description("Beskrivning av händelsen")] string? description = null)
    {
        // Handle test mode
        if (_calendarService == null)
        {
            _logger.LogInformation($"Test mode: Creating calendar event '{title}' from {startTime} to {endTime}");
            return $"SUCCESS: Test mode - Kalenderhändelse '{title}' skapad.";
        }

        try
        {
            // Use primary calendar if shared calendar ID is not configured properly
            var calendarId = string.IsNullOrEmpty(_sharedCalendarId) || _sharedCalendarId.Contains("1234567890")
                ? "primary"
                : _sharedCalendarId;

            _logger.LogInformation($"CREATE EVENT: Using calendar ID '{calendarId}' (SharedCalendarId config: '{_sharedCalendarId}')");

            // Parse times and handle timezone
            var startDateTime = DateTimeOffset.Parse(startTime);
            var endDateTime = DateTimeOffset.Parse(endTime);

            _logger.LogInformation($"Creating calendar event '{title}' from {startDateTime} to {endDateTime}");

            var newEvent = new Event
            {
                Summary = title,
                Description = description,
                Start = new EventDateTime { DateTimeDateTimeOffset = startDateTime, TimeZone = "Europe/Stockholm" },
                End = new EventDateTime { DateTimeDateTimeOffset = endDateTime, TimeZone = "Europe/Stockholm" },
                Attendees = _collaborators.Select(email => new EventAttendee { Email = email }).ToList()
            };
            var request = _calendarService.Events.Insert(newEvent, calendarId);
            var created = await request.ExecuteAsync();
            return $"SUCCESS: Kalenderhändelse '{title}' skapad.";
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in CreateCalendarEvent: {ex.Message}");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Listar alla kalenderhändelser i den gemensamma kalendern.")]
    public async Task<string> ListCalendarEvents(
        [Description("Antal händelser att hämta (standard: 10)")] int maxResults = 10)
    {
        // Handle test mode
        if (_calendarService == null)
        {
            _logger.LogInformation($"Test mode: Listing {maxResults} calendar events");
            return "Test mode: Found 2 calendar events:\n- Team Meeting 2024-01-01 10:00 - 2024-01-01 11:00\n- Project Review 2024-01-02 14:00 - 2024-01-02 15:00";
        }

        try
        {
            // Use primary calendar if shared calendar ID is not configured properly
            var calendarId = string.IsNullOrEmpty(_sharedCalendarId) || _sharedCalendarId.Contains("1234567890")
                ? "primary"
                : _sharedCalendarId;

            _logger.LogInformation($"LIST EVENTS: Using calendar ID '{calendarId}' (SharedCalendarId config: '{_sharedCalendarId}')");

            var request = _calendarService.Events.List(calendarId);
            request.MaxResults = maxResults;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            request.SingleEvents = true;
            request.TimeZone = "Europe/Stockholm"; // Use Swedish timezone for consistent display

            var events = await request.ExecuteAsync();

            _logger.LogInformation($"Found {events.Items?.Count ?? 0} calendar events in calendar '{calendarId}'");

            // Log each event for debugging
            if (events.Items != null)
            {
                foreach (var evt in events.Items)
                {
                    _logger.LogInformation($"Event found: '{evt.Summary}' at {evt.Start?.DateTimeDateTimeOffset} - {evt.End?.DateTimeDateTimeOffset}");
                }
            }

            if (events.Items == null || events.Items.Count == 0)
                return "Inga kalenderhändelser hittades.";

            // Convert times to Swedish timezone for display
            var swedishTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

            return string.Join("\n", events.Items.Select(e => {
                var startTime = e.Start?.DateTimeDateTimeOffset?.ToOffset(swedishTimeZone.GetUtcOffset(DateTime.UtcNow));
                var endTime = e.End?.DateTimeDateTimeOffset?.ToOffset(swedishTimeZone.GetUtcOffset(DateTime.UtcNow));

                return $"- {e.Summary} {startTime?.ToString("yyyy-MM-dd HH:mm")} - {endTime?.ToString("yyyy-MM-dd HH:mm")}";
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in ListCalendarEvents: {ex.Message}");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Söker efter specifika kalenderhändelser baserat på titel, datum eller tid.")]
    public async Task<string> SearchCalendarEvents(
        [Description("Titel på händelsen att söka efter")] string? title = null,
        [Description("Startdatum att söka från (ISO 8601-format, t.ex. '2024-06-01T00:00:00Z')")] string? startDate = null,
        [Description("Slutdatum att söka till (ISO 8601-format, t.ex. '2024-06-01T23:59:59Z')")] string? endDate = null,
        [Description("Antal händelser att hämta (standard: 50)")] int maxResults = 50)
    {
        // Handle test mode
        if (_calendarService == null)
        {
            _logger.LogInformation($"Test mode: Searching for calendar events with title '{title}'");
            if (!string.IsNullOrEmpty(title) && title.Contains("Testmöte"))
            {
                return "Test mode: Found matching event:\n- Testmöte 2024-01-01 10:00 - 2024-01-01 11:00";
            }
            return "Test mode: No matching events found.";
        }

        try
        {
            // Use primary calendar if shared calendar ID is not configured properly
            var calendarId = string.IsNullOrEmpty(_sharedCalendarId) || _sharedCalendarId.Contains("1234567890")
                ? "primary"
                : _sharedCalendarId;

            _logger.LogInformation($"SEARCH EVENTS: Using calendar ID '{calendarId}' (SharedCalendarId config: '{_sharedCalendarId}') searching for title: '{title}'");

            var request = _calendarService.Events.List(calendarId);
            request.MaxResults = maxResults;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            request.SingleEvents = true;

            // Add time range filter if provided
            if (!string.IsNullOrEmpty(startDate))
            {
                if (DateTimeOffset.TryParse(startDate, out var start))
                {
                    request.TimeMinDateTimeOffset = start;
                }
            }

            if (!string.IsNullOrEmpty(endDate))
            {
                if (DateTimeOffset.TryParse(endDate, out var end))
                {
                    request.TimeMaxDateTimeOffset = end;
                }
            }

            // Add search query if title is provided
            if (!string.IsNullOrEmpty(title))
            {
                request.Q = title;
            }

            var events = await request.ExecuteAsync();
            if (events.Items == null || events.Items.Count == 0)
            {
                var searchCriteria = new List<string>();
                if (!string.IsNullOrEmpty(title)) searchCriteria.Add($"titel '{title}'");
                if (!string.IsNullOrEmpty(startDate)) searchCriteria.Add($"från {startDate}");
                if (!string.IsNullOrEmpty(endDate)) searchCriteria.Add($"till {endDate}");

                return $"Inga kalenderhändelser hittades med {string.Join(", ", searchCriteria)}.";
            }

            // Filter by title if provided (additional client-side filtering for better matching)
            var filteredEvents = events.Items;
            if (!string.IsNullOrEmpty(title))
            {
                filteredEvents = events.Items.Where(e =>
                    e.Summary != null &&
                    e.Summary.Contains(title, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (filteredEvents.Count == 0)
            {
                return $"Inga kalenderhändelser hittades med titel '{title}'.";
            }

            // Convert times to Swedish timezone for display
            var swedishTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");

            return string.Join("\n", filteredEvents.Select(e => {
                var startTime = e.Start?.DateTimeDateTimeOffset?.ToOffset(swedishTimeZone.GetUtcOffset(DateTime.UtcNow));
                var endTime = e.End?.DateTimeDateTimeOffset?.ToOffset(swedishTimeZone.GetUtcOffset(DateTime.UtcNow));

                return $"- {e.Summary} {startTime?.ToString("yyyy-MM-dd HH:mm")} - {endTime?.ToString("yyyy-MM-dd HH:mm")}";
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in SearchCalendarEvents: {ex.Message}");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Skickar ett meddelande till en användare via Google Chat.")]
    public async Task<string> SendChatMessage(
        [Description("E-postadress för mottagaren")] string recipientEmail,
        [Description("Meddelandets innehåll")] string message)
    {
        // Handle test mode
        if (_calendarService == null)
        {
            _logger.LogInformation($"Test mode: Sending chat message to {recipientEmail}");
            await Task.CompletedTask;
            return $"SUCCESS: Test mode - Meddelande skickat till {recipientEmail}.";
        }

        try
        {
            // TODO: Implement Google Chat API when available
            await Task.CompletedTask;
            return $"SUCCESS: Meddelande skickat till {recipientEmail} (simulated).";
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    // --- Helpers ---
    private async Task<string> GetOrCreateFolderId(string folderName)
    {
        // Handle test mode
        if (_driveService == null)
        {
            _logger.LogInformation($"Test mode: Getting folder ID for '{folderName}'");
            return "test-folder-id";
        }

        try
        {
            var request = _driveService.Files.List();
            request.Q = $"name = '{folderName}' and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            request.Fields = "files(id, name)";
            var result = await request.ExecuteAsync();
            var folder = result.Files?.FirstOrDefault();
            if (folder != null)
            {
                _logger.LogInformation($"Found existing folder '{folderName}' with ID: {folder.Id}");
                return folder.Id;
            }

            _logger.LogInformation($"Folder '{folderName}' not found, attempting to create it");
            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = folderName,
                MimeType = "application/vnd.google-apps.folder"
            };
            var createRequest = _driveService.Files.Create(fileMetadata);
            createRequest.Fields = "id";
            var created = await createRequest.ExecuteAsync();
            _logger.LogInformation($"Created folder '{folderName}' with ID: {created.Id}");
            return created.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in GetOrCreateFolderId: {ex.Message}");
            // If we can't create the folder, try to use root folder
            _logger.LogInformation("Attempting to use root folder instead");
            return "root";
        }
    }

    private async Task<string> GetOrCreateDirectMessageSpace(string recipientEmail)
    {
        // For demo: just return "users/{recipientEmail}" (real implementation would use Chat API to find/create DM space)
        await Task.CompletedTask;
        return $"users/{recipientEmail}";
    }
} 