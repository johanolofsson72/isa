using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Shared.Services;

/// <summary>
/// Service for reading user data from users.json file
/// </summary>
public class UserDataService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserDataService> _logger;
    private readonly string _usersJsonPath;

    public UserDataService(IConfiguration configuration, ILogger<UserDataService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Get users.json path from configuration or use default
        _usersJsonPath = _configuration["UsersJsonPath"] ?? "users.json";
        
        // If path is relative, make it relative to the Agent directory
        if (!Path.IsPathRooted(_usersJsonPath))
        {
            var agentPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Agent");
            _usersJsonPath = Path.Combine(agentPath, _usersJsonPath);
        }
        
        _logger.LogInformation("UserDataService initialized with path: {Path}", _usersJsonPath);
    }

    /// <summary>
    /// Get user data by email address
    /// </summary>
    /// <param name="email">Email address to search for</param>
    /// <returns>User data object or null if not found</returns>
    public async Task<UserData?> GetUserByEmailAsync(string email)
    {
        try
        {
            Console.WriteLine($"[UserDataService] DEBUG: GetUserByEmailAsync called with email: {email}");
            Console.WriteLine($"[UserDataService] DEBUG: Users.json path: {_usersJsonPath}");

            _logger.LogInformation("DEBUG: GetUserByEmailAsync called with email: {Email}", email);
            _logger.LogInformation("DEBUG: Users.json path: {Path}", _usersJsonPath);

            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Email address is null or empty");
                return null;
            }

            if (!File.Exists(_usersJsonPath))
            {
                Console.WriteLine($"[UserDataService] ERROR: Users.json file not found at path: {_usersJsonPath}");
                Console.WriteLine($"[UserDataService] DEBUG: Current directory: {Directory.GetCurrentDirectory()}");
                Console.WriteLine($"[UserDataService] DEBUG: AppContext.BaseDirectory: {AppContext.BaseDirectory}");

                _logger.LogError("Users.json file not found at path: {Path}", _usersJsonPath);
                _logger.LogInformation("DEBUG: Current directory: {CurrentDir}", Directory.GetCurrentDirectory());
                _logger.LogInformation("DEBUG: AppContext.BaseDirectory: {BaseDir}", AppContext.BaseDirectory);
                return null;
            }

            _logger.LogInformation("DEBUG: Users.json file exists, reading content...");

            var jsonContent = await File.ReadAllTextAsync(_usersJsonPath);
            _logger.LogInformation("DEBUG: JSON content length: {Length}", jsonContent.Length);

            var jsonDocument = JsonDocument.Parse(jsonContent);
            _logger.LogInformation("DEBUG: JSON parsed successfully");

            // Get the users array from the root object
            if (!jsonDocument.RootElement.TryGetProperty("users", out var usersArray))
            {
                _logger.LogError("No 'users' array found in JSON");
                return null;
            }

            if (usersArray.ValueKind != JsonValueKind.Array)
            {
                _logger.LogError("'users' property is not an array");
                return null;
            }

            // Search through all users in the array
            int userCount = 0;
            foreach (var userElement in usersArray.EnumerateArray())
            {
                userCount++;

                // Get the user's name
                var userName = userElement.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : $"User{userCount}";
                _logger.LogInformation("DEBUG: Checking user #{Count}: {UserName}", userCount, userName);

                // Check if this user has contacts with the specified email
                if (userElement.TryGetProperty("contacts", out var contacts))
                {
                    _logger.LogInformation("DEBUG: User {UserName} has contacts property", userName);

                    if (contacts.TryGetProperty("email", out var userEmail))
                    {
                        var emailValue = userEmail.GetString();
                        _logger.LogInformation("DEBUG: User {UserName} email: {UserEmail}", userName, emailValue);

                        if (string.Equals(emailValue, email, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Found user data for email: {Email}", email);
                            return ParseUserData(userName ?? "Unknown", contacts);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("DEBUG: User {UserName} has no email property in contacts", userName);
                    }
                }
                else
                {
                    _logger.LogInformation("DEBUG: User {UserName} has no contacts property", userName);
                }
            }

            _logger.LogInformation("DEBUG: Searched through {UserCount} users, no match found", userCount);

            _logger.LogWarning("No user found with email: {Email}", email);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading user data for email: {Email}", email);
            return null;
        }
    }

    /// <summary>
    /// Parse user data from JSON element
    /// </summary>
    private UserData ParseUserData(string userName, JsonElement contacts)
    {
        var userData = new UserData
        {
            UserName = userName,
            Email = contacts.TryGetProperty("email", out var email) ? email.GetString() : null,
            EmailPassword = contacts.TryGetProperty("email-password", out var emailPassword) ? emailPassword.GetString() : null,
            EmailServer = contacts.TryGetProperty("email-server", out var emailServer) ? emailServer.GetString() : null,
            EmailImapPort = contacts.TryGetProperty("email-imap-port", out var imapPort) ? imapPort.GetInt32() : 993,
            EmailUseSSL = contacts.TryGetProperty("email-use-ssl", out var useSSL) ? useSSL.GetBoolean() : true,
            EmailSmtpPort = contacts.TryGetProperty("email-smtp-port", out var smtpPort) ? smtpPort.GetInt32() : 587,
            Phone = contacts.TryGetProperty("phone", out var phone) ? phone.GetString() : null,
            Google = contacts.TryGetProperty("google", out var google) ? google.GetString() : null,
            GooglePassword = contacts.TryGetProperty("google-password", out var googlePassword) ? googlePassword.GetString() : null,
            Office365 = contacts.TryGetProperty("office365", out var office365) ? office365.GetString() : null
        };

        _logger.LogInformation("Parsed user data for {UserName}: Email={Email}, Server={Server}, Port={Port}, SSL={SSL}", 
            userData.UserName, userData.Email, userData.EmailServer, userData.EmailImapPort, userData.EmailUseSSL);

        return userData;
    }
}

/// <summary>
/// User data model
/// </summary>
public class UserData
{
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? EmailPassword { get; set; }
    public string? EmailServer { get; set; }
    public int EmailImapPort { get; set; }
    public bool EmailUseSSL { get; set; }
    public int EmailSmtpPort { get; set; }
    public string? Phone { get; set; }
    public string? Google { get; set; }
    public string? GooglePassword { get; set; }
    public string? Office365 { get; set; }
}
