using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using MimeKit;
using Shared.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mail;

public class Plugin
{
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly string _sender;
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly ILogger<Plugin> _logger;
    private readonly bool _enableRealMessaging;
    private readonly IConfiguration _configuration;
    private readonly UserDataService _userDataService;

    /// <summary>
    /// Static service provider for accessing DI services (set by Agent during startup)
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Creates a new instance of MailjetPlugin
    /// </summary>
    /// <param name="apiKey">The Mailjet API key</param>
    /// <param name="apiSecret">The Mailjet API secret</param>
    /// <param name="sender">The sender email address</param>
    /// <param name="logger">Optional logger for the plugin</param>
    /// <param name="enableRealMessaging">Whether to send real emails or simulate</param>
    public Plugin(string apiKey, string apiSecret, string sender, ILogger<Plugin>? logger = null, bool enableRealMessaging = true)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _apiSecret = apiSecret ?? throw new ArgumentNullException(nameof(apiSecret));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? NullLogger<Plugin>.Instance;
        _smtpServer = "in-v3.mailjet.com";
        _smtpPort = 587; // TLS port
        _enableRealMessaging = enableRealMessaging;

        // Initialize with empty configuration for this constructor
        var emptyConfig = new ConfigurationBuilder().Build();
        _configuration = emptyConfig;
        _userDataService = new UserDataService(emptyConfig, NullLogger<UserDataService>.Instance);
    }

    /// <summary>
    /// Creates a new instance of MailjetPlugin from configuration
    /// </summary>
    /// <param name="configuration">Configuration containing Mailjet settings</param>
    /// <param name="logger">Optional logger for the plugin</param>
    public Plugin(IConfiguration configuration, ILogger<Plugin>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _logger = logger ?? NullLogger<Plugin>.Instance;
        _configuration = configuration;

        // Initialize UserDataService - use NullLogger for simplicity
        _userDataService = new UserDataService(configuration, NullLogger<UserDataService>.Instance);

        _apiKey =
            configuration["Mailjet:ApiKey"] ?? throw new ArgumentNullException("Mailjet:ApiKey");

        _apiSecret =
            configuration["Mailjet:ApiSecret"]
            ?? throw new ArgumentNullException("Mailjet:ApiSecret");

        _sender =
            configuration["Mailjet:Sender"] ?? throw new ArgumentNullException("Mailjet:Sender");

        _smtpServer = configuration["Mailjet:SmtpServer"] ?? "in-v3.mailjet.com";
        _smtpPort = configuration.GetValue<int>("Mailjet:SmtpPort", 587); // TLS port

        // Check communications configuration
        _enableRealMessaging = configuration.GetValue<bool>("Communications:EnableRealMessaging", false) ||
                              Environment.GetEnvironmentVariable("ACCOUNTINGAGENT_ENABLE_REAL_MESSAGING")?.ToLower() == "true";

        _logger.LogInformation(
            "MailPlugin initialized - Sender: {Sender}, SMTP: {SmtpServer}:{SmtpPort}, Real messaging: {EnableRealMessaging}",
            _sender, _smtpServer, _smtpPort, _enableRealMessaging);

        _logger.LogInformation("Mail configuration - ApiKey: {ApiKey}, ApiSecret: {ApiSecret}",
            string.IsNullOrEmpty(_apiKey) ? "[EMPTY]" : "[SET]",
            string.IsNullOrEmpty(_apiSecret) ? "[EMPTY]" : "[SET]");

        if (!_enableRealMessaging)
        {
            _logger.LogInformation("MailPlugin running in safe mode - real email sending disabled. Set Communications:EnableRealMessaging=true or ACCOUNTINGAGENT_ENABLE_REAL_MESSAGING=true to enable.");
        }
        else
        {
            _logger.LogInformation("MailPlugin initialized with real email sending enabled");
        }
    }

    /// <summary>
    /// Sends an email using the Mailjet API
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="body">Email body content (can include HTML)</param>
    /// <param name="isHtml">Whether the body contains HTML markup</param>
    /// <returns>A response message indicating success or failure</returns>
    [KernelFunction]
    [Description("Send an email using Mailjet with professional delivery assurance")]
    public async Task<string> SendEmailAsync(
        [Description("Recipient email address")] string to,
        [Description("Email subject")] string subject,
        [Description("Email body content (can include HTML)")] string body,
        [Description("Whether the body contains HTML markup")] bool isHtml = true
    )
    {
        try
        {
            _logger.LogInformation("SendEmailAsync called with recipient: {Recipient}, subject: {Subject}", to, subject);

            // Parameter validation
            if (string.IsNullOrEmpty(to))
                return "ERROR: Recipient email address is required";
            if (string.IsNullOrEmpty(subject))
                return "ERROR: Email subject is required";
            if (string.IsNullOrEmpty(body))
                return "ERROR: Email body is required";

            // Handle safe mode
            if (!_enableRealMessaging)
            {
                _logger.LogInformation(
                    "SAFE MODE: Simulating email to {Recipient} with subject: {Subject}",
                    to,
                    subject
                );
                await Task.CompletedTask;
                return $"SUCCESS: Safe mode - Email simulated to {to} with subject '{subject}'";
            }

            _logger.LogInformation(
                "Preparing to send email to {Recipient} with subject: {Subject}",
                to,
                subject
            );

            // Prepare email message using MailKit
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Isa", _sender));
            email.To.Add(new MailboxAddress("To", to));
            email.Subject = subject;

            email.Body = isHtml
                ? new TextPart("html") { Text = body }
                : new TextPart("plain") { Text = body };

            _logger.LogInformation(
                "Preparing to send email via Mailjet SMTP\nServer: {Server}:{Port}\nFrom: {From}\nTo: {To}\nSubject: {Subject}",
                _smtpServer,
                _smtpPort,
                _sender,
                to,
                subject
            );

            // Create a new SmtpClient instance
            using var smtpClient = new MailKit.Net.Smtp.SmtpClient();

            try
            {
                // Configure TLS explicitly
                smtpClient.ServerCertificateValidationCallback = (s, c, h, e) => true;

                _logger.LogInformation("Connecting to Mailjet SMTP server...");

                await smtpClient.ConnectAsync(
                    _smtpServer,
                    _smtpPort,
                    MailKit.Security.SecureSocketOptions.StartTls
                );

                _logger.LogInformation("Connected. Authenticating...");

                await smtpClient.AuthenticateAsync(_apiKey, _apiSecret);

                _logger.LogInformation("Authenticated. Sending message...");

                await smtpClient.SendAsync(email);

                _logger.LogInformation("Message sent successfully!");

                await smtpClient.DisconnectAsync(true);

                return $"Successfully sent email to {to}";
            }
            catch (Exception ex)
            {
                var error = $"Failed to send email: {ex.Message}";
                if (ex.InnerException != null)
                {
                    error += $"\nInner error: {ex.InnerException.Message}";
                }

                _logger.LogError(ex, "Failed to send email via Mailjet SMTP");
                return $"ERROR: {error}";
            }
            finally
            {
                if (smtpClient.IsConnected)
                {
                    try
                    {
                        await smtpClient.DisconnectAsync(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during SMTP disconnect");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email");
            return $"ERROR: {ex.GetType().Name} - {ex.Message}";
        }
    }

    /// <summary>
    /// Sends an email with file attachment using the Mailjet API
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="body">Email body content (can include HTML)</param>
    /// <param name="attachmentPath">Full path to the file to attach</param>
    /// <param name="attachmentName">Name for the attachment (optional, uses filename if not provided)</param>
    /// <param name="isHtml">Whether the body contains HTML markup</param>
    /// <returns>A response message indicating success or failure</returns>
    [KernelFunction]
    [Description("STEG 5: Skickar e-post med bifogad fil (Excel-rapport) med professionell leveransgaranti. Använd denna funktion EFTER FinishReportAsync när du har en färdig Excel-rapport som ska skickas till användaren. Hanterar automatiskt namn-till-e-post-konvertering från users.json. Om användaren säger 'skicka till Anna Andersson', konvertera namnet till korrekt e-postadress.")]
    public async Task<string> SendEmailWithAttachmentAsync(
        [Description("Mottagarens e-postadress eller namn (konverteras automatiskt från users.json)")] string to,
        [Description("E-postämne")] string subject,
        [Description("E-postinnehåll (kan innehålla HTML)")] string body,
        [Description("Fullständig sökväg till filen som ska bifogas (från FinishReportAsync)")] string attachmentPath,
        [Description("Namn på bilagan (valfritt, använder filnamn om inte angivet)")] string? attachmentName = null,
        [Description("Om innehållet innehåller HTML-markup")] bool isHtml = true
    )
    {
        try
        {
            _logger.LogInformation("SendEmailWithAttachmentAsync called with recipient: {Recipient}, subject: {Subject}, attachment: {AttachmentPath}",
                to, subject, attachmentPath);

            // Parameter validation
            if (string.IsNullOrEmpty(to))
                return "ERROR: Recipient email address is required";
            if (string.IsNullOrEmpty(subject))
                return "ERROR: Email subject is required";
            if (string.IsNullOrEmpty(body))
                return "ERROR: Email body is required";
            if (string.IsNullOrEmpty(attachmentPath))
                return "ERROR: Attachment path is required";

            // Validate attachment file exists
            if (!File.Exists(attachmentPath))
                return $"ERROR: Attachment file not found: {attachmentPath}";

            // Handle safe mode
            if (!_enableRealMessaging)
            {
                _logger.LogInformation(
                    "SAFE MODE: Simulating email with attachment to {Recipient} with subject: {Subject}, attachment: {AttachmentPath}",
                    to, subject, attachmentPath);
                await Task.CompletedTask;
                return $"SUCCESS: Safe mode - Email with attachment simulated to {to} with subject '{subject}' and attachment '{Path.GetFileName(attachmentPath)}'";
            }

            _logger.LogInformation(
                "Preparing to send email with attachment to {Recipient} with subject: {Subject}",
                to, subject);

            // Prepare email message using MailKit
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Isa", _sender));
            email.To.Add(new MailboxAddress("To", to));
            email.Subject = subject;

            // Create multipart message for body + attachment
            var multipart = new Multipart("mixed");

            // Add body part
            var bodyPart = isHtml
                ? new TextPart("html") { Text = body }
                : new TextPart("plain") { Text = body };
            multipart.Add(bodyPart);

            // Add attachment
            var attachment = new MimePart()
            {
                Content = new MimeContent(File.OpenRead(attachmentPath)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = attachmentName ?? Path.GetFileName(attachmentPath)
            };
            multipart.Add(attachment);

            email.Body = multipart;

            _logger.LogInformation(
                "Preparing to send email with attachment via Mailjet SMTP\nServer: {Server}:{Port}\nFrom: {From}\nTo: {To}\nSubject: {Subject}\nAttachment: {AttachmentName}",
                _smtpServer, _smtpPort, _sender, to, subject, attachment.FileName);

            // Create a new SmtpClient instance
            using var smtpClient = new MailKit.Net.Smtp.SmtpClient();

            try
            {
                // Configure TLS explicitly
                smtpClient.ServerCertificateValidationCallback = (s, c, h, e) => true;

                _logger.LogInformation("Connecting to Mailjet SMTP server...");

                await smtpClient.ConnectAsync(
                    _smtpServer,
                    _smtpPort,
                    MailKit.Security.SecureSocketOptions.StartTls
                );

                _logger.LogInformation("Connected. Authenticating...");

                await smtpClient.AuthenticateAsync(_apiKey, _apiSecret);

                _logger.LogInformation("Authenticated. Sending message with attachment...");

                await smtpClient.SendAsync(email);

                _logger.LogInformation("Message with attachment sent successfully!");

                await smtpClient.DisconnectAsync(true);

                return $"Successfully sent email with attachment '{attachment.FileName}' to {to}";
            }
            catch (Exception ex)
            {
                var error = $"Failed to send email with attachment: {ex.Message}";
                if (ex.InnerException != null)
                {
                    error += $"\nInner error: {ex.InnerException.Message}";
                }

                _logger.LogError(ex, "Failed to send email with attachment via Mailjet SMTP");
                return $"ERROR: {error}";
            }
            finally
            {
                if (smtpClient.IsConnected)
                {
                    try
                    {
                        await smtpClient.DisconnectAsync(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during SMTP disconnect");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending email with attachment");
            return $"ERROR: {ex.GetType().Name} - {ex.Message}";
        }
    }

    /// <summary>
    /// Read unread emails from a mail account. ALWAYS get all parameters from users.json for the specified user.
    /// </summary>
    [KernelFunction]
    [Description("Read unread emails.")]
    public async Task<ReadUnreadEmailResponse> ReadUnreadEmailsAsync(
        [Description("Email address from users.json")] string emailAddress,
        [Description("Username (same as emailAddress)")] string username,
        [Description("Password from email-password field in users.json")] string password,
        [Description("Protocol (IMAP or POP3)")] string incomingProtocol = "IMAP",
        [Description("Server from email-server field")] string incomingServer = "imap.gmail.com",
        [Description("Port from email-imap-port field")] int incomingPort = 587,
        [Description("SSL from email-use-ssl field")] bool useSSL = true,
        [Description("Maximum number of messages to fetch")] int maxMessages = 10,
        [Description("Whether to fetch attachments")] bool fetchAttachments = true
    )
    {
        _logger.LogInformation("ReadUnreadEmailsAsync called with parameters:");
        _logger.LogInformation("  EmailAddress: {EmailAddress}", emailAddress);
        _logger.LogInformation("  Username: {Username}", username);
        _logger.LogInformation("  Password: {Password}", string.IsNullOrEmpty(password) ? "[EMPTY]" : "[SET]");
        _logger.LogInformation("  Protocol: {Protocol}", incomingProtocol);
        _logger.LogInformation("  Server: {Server}", incomingServer);
        _logger.LogInformation("  Port: {Port}", incomingPort);
        _logger.LogInformation("  UseSSL: {UseSSL}", useSSL);
        _logger.LogInformation("  MaxMessages: {MaxMessages}", maxMessages);
        _logger.LogInformation("  FetchAttachments: {FetchAttachments}", fetchAttachments);

        _logger.LogInformation("DEBUG: Starting email reading process...");

        // Get user data from users.json based on email address
        var userData = await _userDataService.GetUserByEmailAsync(emailAddress);
        if (userData == null)
        {
            _logger.LogError("No user data found for email: {EmailAddress}", emailAddress);
            return new ReadUnreadEmailResponse
            {
                Success = false,
                Message = $"No user configuration found for email: {emailAddress}",
                UnreadCount = 0,
                Emails = new List<EmailMessage>()
            };
        }

        // Override parameters with data from users.json
        var actualPassword = userData.EmailPassword ?? password;
        var actualServer = userData.EmailServer ?? incomingServer;
        var actualPort = userData.EmailImapPort;
        var actualUseSSL = userData.EmailUseSSL;

        _logger.LogInformation("Using user data from users.json:");
        _logger.LogInformation("  EmailAddress: {EmailAddress}", emailAddress);
        _logger.LogInformation("  Server: {Server}", actualServer);
        _logger.LogInformation("  Port: {Port}", actualPort);
        _logger.LogInformation("  UseSSL: {UseSSL}", actualUseSSL);
        _logger.LogInformation("  Password length: {PasswordLength}", actualPassword?.Length ?? 0);

        _logger.LogInformation("DEBUG: About to enter try block with user data...");

        try
        {
            var emails = new List<EmailMessage>();

            if (incomingProtocol.ToUpper() == "IMAP")
            {
                using var client = new ImapClient();
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                _logger.LogInformation(
                    "Connecting to IMAP server {Server}:{Port}",
                    actualServer,
                    actualPort
                );

                // Connect to the server
                _logger.LogInformation("Attempting to connect to IMAP server {Server}:{Port} with SSL={UseSSL}",
                    actualServer, actualPort, actualUseSSL);

                // For port 143, use STARTTLS instead of direct SSL
                var secureSocketOptions = actualUseSSL
                    ? MailKit.Security.SecureSocketOptions.SslOnConnect
                    : (actualPort == 143
                        ? MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable
                        : MailKit.Security.SecureSocketOptions.None);

                _logger.LogInformation("Using SecureSocketOptions: {SecureSocketOptions}", secureSocketOptions);

                await client.ConnectAsync(actualServer, actualPort, secureSocketOptions);
                _logger.LogInformation("Successfully connected to IMAP server");

                // Login with data from users.json
                _logger.LogInformation("Attempting to authenticate with username: {Username}", emailAddress);
                _logger.LogInformation("Password length from users.json: {PasswordLength}", actualPassword?.Length ?? 0);
                _logger.LogInformation("Password first 5 chars: {PasswordStart}", actualPassword?.Length > 5 ? actualPassword.Substring(0, 5) : actualPassword ?? "NULL");
                _logger.LogInformation("Password last 5 chars: {PasswordEnd}", actualPassword?.Length > 5 ? actualPassword.Substring(actualPassword.Length - 5) : actualPassword ?? "NULL");

                // Use full email address as Loopia requires it
                _logger.LogInformation("Attempting authentication with email from users.json: {Email}", emailAddress);
                await client.AuthenticateAsync(emailAddress, actualPassword);
                _logger.LogInformation("Successfully authenticated to IMAP server");

                // Open inbox
                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadWrite);
                _logger.LogInformation("Opened INBOX folder - Total messages: {Total}, Unread: {Unread}",
                    inbox.Count, inbox.Unread);

                // Search for unread messages ONLY
                var uids = await inbox.SearchAsync(SearchQuery.NotSeen);
                _logger.LogInformation("Found {Count} unread email messages with UIDs: {UIDs}",
                    uids.Count, string.Join(", ", uids.Take(10)));

                // Do NOT fallback to read messages - only process truly unread emails
                if (uids.Count == 0)
                {
                    _logger.LogInformation("No unread messages found - returning empty result (this is correct behavior)");
                }

                // Limit messages to max value
                var messagesToFetch = uids.Take(maxMessages).ToList();
                int totalUnread = uids.Count;

                _logger.LogInformation("Will fetch {FetchCount} messages out of {TotalCount} found",
                    messagesToFetch.Count, totalUnread);

                if (messagesToFetch.Count == 0)
                {
                    _logger.LogInformation("No messages to fetch - returning empty result");
                    return new ReadUnreadEmailResponse
                    {
                        Success = true,
                        Message = $"No unread emails found in {emailAddress}",
                        UnreadCount = 0,
                        Emails = new List<EmailMessage>()
                    };
                }

                // Fetch and process each message
                foreach (var uid in messagesToFetch)
                {
                    var message = await inbox.GetMessageAsync(uid);

                    // Convert to our EmailMessage model
                    var emailMessage = await ConvertToEmailMessageAsync(message, fetchAttachments);
                    emails.Add(emailMessage);

                    // Mark as read
                    await inbox.SetFlagsAsync(uid, MessageFlags.Seen, true);

                    _logger.LogInformation(
                        "Read email from {From} with subject {Subject}",
                        emailMessage.From,
                        emailMessage.Subject
                    );
                }

                await client.DisconnectAsync(true);

                var senders = emails.Select(e => e.From).Distinct().Take(3);
                var subjects = emails.Select(e => e.Subject).Take(3);
                var attachmentCount = emails.Sum(e => e.Attachments?.Count ?? 0);

                var summary = $"Läste {emails.Count} emails från: {string.Join(", ", senders)}{(emails.Select(e => e.From).Distinct().Count() > 3 ? "..." : "")}. " +
                             $"Ämnen: {string.Join(", ", subjects)}{(emails.Count > 3 ? "..." : "")}. " +
                             $"{(attachmentCount > 0 ? $"{attachmentCount} bilagor sparade i TempFiles. " : "")}";

                return new ReadUnreadEmailResponse
                {
                    Success = true,
                    Message = summary,
                    UnreadCount = totalUnread,
                    Emails = emails  // Returnera de riktiga email-objekten med alla uppgifter
                };
            }
            else if (incomingProtocol.ToUpper() == "POP3")
            {
                using var client = new Pop3Client();
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                _logger.LogInformation(
                    "Connecting to POP3 server {Server}:{Port}",
                    incomingServer,
                    incomingPort
                );

                // Connect to server
                _logger.LogInformation("Attempting to connect to POP3 server {Server}:{Port} with SSL={UseSSL}",
                    incomingServer, incomingPort, useSSL);
                await client.ConnectAsync(incomingServer, incomingPort, useSSL);
                _logger.LogInformation("Successfully connected to POP3 server");

                // Login
                _logger.LogInformation("Attempting to authenticate with username: {Username}", username);
                await client.AuthenticateAsync(username, password);
                _logger.LogInformation("Successfully authenticated to POP3 server");

                // Get message count
                int messageCount = client.Count;

                _logger.LogInformation(
                    "Found {Count} email messages (POP3 cannot distinguish read/unread)",
                    messageCount
                );

                // Limit messages to max value
                int fetchCount = Math.Min(messageCount, maxMessages);

                // Get latest messages first (assume higher index is newer)
                for (
                    int i = messageCount - 1, fetched = 0;
                    i >= 0 && fetched < fetchCount;
                    i--, fetched++
                )
                {
                    var message = await client.GetMessageAsync(i);

                    // Convert to our EmailMessage model
                    var emailMessage = await ConvertToEmailMessageAsync(message, fetchAttachments);
                    emails.Add(emailMessage);

                    _logger.LogInformation(
                        "Read email from {From} with subject {Subject}",
                        emailMessage.From,
                        emailMessage.Subject
                    );
                }

                await client.DisconnectAsync(true, CancellationToken.None);

                var senders = emails.Select(e => e.From).Distinct().Take(3);
                var subjects = emails.Select(e => e.Subject).Take(3);
                var attachmentCount = emails.Sum(e => e.Attachments?.Count ?? 0);

                var summary = $"Läste {emails.Count} emails med POP3 från: {string.Join(", ", senders)}{(emails.Select(e => e.From).Distinct().Count() > 3 ? "..." : "")}. " +
                             $"Ämnen: {string.Join(", ", subjects)}{(emails.Count > 3 ? "..." : "")}. " +
                             $"{(attachmentCount > 0 ? $"{attachmentCount} bilagor sparade i TempFiles. " : "")}";

                return new ReadUnreadEmailResponse
                {
                    Success = true,
                    Message = summary,
                    UnreadCount = messageCount,
                    Emails = emails  // Returnera de riktiga email-objekten med alla uppgifter
                };
            }
            else
            {
                return new ReadUnreadEmailResponse
                {
                    Success = false,
                    Message = $"Protocol {incomingProtocol} is not supported. Use IMAP or POP3.",
                    UnreadCount = 0,
                    Emails = emails
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation("EXCEPTION CAUGHT: Error reading email from {EmailAddress} using {Protocol} on {Server}:{Port}",
                emailAddress, incomingProtocol, incomingServer, incomingPort);
            _logger.LogInformation("EXCEPTION TYPE: {ExceptionType}", ex.GetType().Name);
            _logger.LogInformation("EXCEPTION MESSAGE: {ExceptionMessage}", ex.Message);
            _logger.LogInformation("STACK TRACE: {StackTrace}", ex.StackTrace);

            _logger.LogError(ex, "Error reading email from {EmailAddress} using {Protocol} on {Server}:{Port}",
                emailAddress, incomingProtocol, incomingServer, incomingPort);
            _logger.LogError("Exception type: {ExceptionType}", ex.GetType().Name);
            _logger.LogError("Exception message: {ExceptionMessage}", ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);

            if (ex.InnerException != null)
            {
                _logger.LogError("Inner exception: {InnerExceptionType} - {InnerExceptionMessage}",
                    ex.InnerException.GetType().Name, ex.InnerException.Message);
            }

            return new ReadUnreadEmailResponse
            {
                Success = false,
                Message = $"Error reading email: {ex.Message}",
                UnreadCount = 0,
                Emails = new List<EmailMessage>()
            };
        }
    }

    /// <summary>
    /// Converts a MimeMessage to our EmailMessage model
    /// </summary>
    private async Task<EmailMessage> ConvertToEmailMessageAsync(MimeMessage message, bool fetchAttachments)
    {
        // Prepare Body values before creating EmailMessage to satisfy required property
        string body = "(No content)";
        bool isHtml = false;

        // Handle body - prioritize plain text, use MailKit's HTML-to-text conversion if needed
        var textBody = message.GetTextBody(MimeKit.Text.TextFormat.Text);
        var plainTextFromHtml = message.GetTextBody(MimeKit.Text.TextFormat.Plain);

        // Prioritera ren text, annars använd MailKit's HTML-till-text konvertering
        if (!string.IsNullOrEmpty(textBody))
        {
            body = textBody;
        }
        else if (!string.IsNullOrEmpty(plainTextFromHtml))
        {
            body = plainTextFromHtml;  // MailKit konverterar automatiskt HTML till ren text
        }
        else
        {
            body = "(No content)";
        }

        isHtml = false;  // Vi vill alltid ha ren text, inte HTML

        var email = new EmailMessage
        {
            From = message.From.ToString(),
            To = message.To.ToString(),
            Subject = message.Subject ?? "(No subject)",
            Body = body,
            IsHtml = isHtml,
            DateReceived = message.Date.DateTime,
            MessageId = message.MessageId
        };

        // Handle attachments if requested
        if (fetchAttachments && message.BodyParts.OfType<MimePart>().Any(x => !string.IsNullOrEmpty(x.FileName)))
        {
            email.Attachments = new List<EmailAttachment>();

            foreach (var mimePart in message.BodyParts.OfType<MimePart>().Where(x => !string.IsNullOrEmpty(x.FileName)))
            {
                using (var memoryStream = new MemoryStream())
                {
                    mimePart.Content.DecodeTo(memoryStream);
                    memoryStream.Position = 0;

                    var bytes = memoryStream.ToArray();
                    var base64Content = Convert.ToBase64String(bytes);
                    var fileName = mimePart.FileName ?? "unknown";
                    var contentType = mimePart.ContentType?.MediaType ?? "application/octet-stream";

                    Guid? tempFileId = null;

                    // Save attachment via TempFilesService if ServiceProvider is available
                    if (ServiceProvider != null)
                    {
                        try
                        {
                            using var scope = ServiceProvider.CreateScope();
                            var tempFilesService = scope.ServiceProvider.GetRequiredService<ITempFilesService>();

                            tempFileId = await tempFilesService.SaveFileAsync(
                                fileName,
                                bytes,
                                contentType,
                                $"Email:{email.From}");

                            _logger.LogInformation("Email attachment saved via TempFilesService: {FileName} -> FileID: {FileId}",
                                fileName, tempFileId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to save email attachment via TempFilesService: {FileName}", fileName);
                            // Continue without TempFileId - attachment will still have base64 content
                        }
                    }

                    email.Attachments.Add(
                        new EmailAttachment
                        {
                            FileName = fileName,
                            ContentType = contentType,
                            Content = tempFileId.HasValue ? $"Saved as TempFile: {tempFileId}" : "Failed to save to TempFiles",
                            Size = bytes.Length,
                            TempFileId = tempFileId
                        }
                    );
                }
            }
        }

        return email;
    }
}

/// <summary>
/// Modell för att begära läsning av e-post
/// </summary>
public class ReadUnreadEmailRequest
{
    /// <summary>
    /// E-postadress för kontot
    /// </summary>
    [Required]
    public required string EmailAddress { get; set; }

    /// <summary>
    /// Användarnamn för att logga in (kan vara samma som e-postadressen)
    /// </summary>
    [Required]
    public required string Username { get; set; }

    /// <summary>
    /// Lösenordet för kontot (ofta ett app-specifikt lösenord)
    /// </summary>
    [Required]
    public required string Password { get; set; }

    /// <summary>
    /// Protokollet att använda för att hämta e-post (IMAP eller POP3)
    /// </summary>
    [Required]
    public required string IncomingProtocol { get; set; } = "IMAP";

    /// <summary>
    /// Servern för inkommande mail
    /// </summary>
    [Required]
    public required string IncomingServer { get; set; }

    /// <summary>
    /// Porten för inkommande e-post
    /// </summary>
    [Required]
    public required int IncomingPort { get; set; }

    /// <summary>
    /// Om SSL ska användas för anslutningen
    /// </summary>
    public bool UseSSL { get; set; } = true;

    /// <summary>
    /// Maximal mängd e-postmeddelanden att hämta (standard är 1)
    /// </summary>
    public int MaxMessages { get; set; } = 1;

    /// <summary>
    /// Om bilagor ska hämtas (kan vara stora)
    /// </summary>
    public bool FetchAttachments { get; set; } = true;
}

/// <summary>
/// Representerar en bilaga i ett e-postmeddelande
/// </summary>
public class EmailAttachment
{
    /// <summary>
    /// Filnamnet för bilagan
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Innehållet i bilagan i Base64-format
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// MIME-typen för bilagan
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// Storleken på bilagan i bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// TempFiles file ID for accessing the attachment via TempFilesService
    /// </summary>
    public Guid? TempFileId { get; set; }
}

/// <summary>
/// Ett e-postmeddelande som hämtats
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Avsändarens e-postadress
    /// </summary>
    public required string From { get; set; }

    /// <summary>
    /// Mottagarens e-postadress
    /// </summary>
    public required string To { get; set; }

    /// <summary>
    /// Ämnesraden för e-postmeddelandet
    /// </summary>
    public required string Subject { get; set; }

    /// <summary>
    /// Innehållet i e-postmeddelandet
    /// </summary>
    public required string Body { get; set; }

    /// <summary>
    /// Om innehållet är i HTML-format
    /// </summary>
    public bool IsHtml { get; set; }

    /// <summary>
    /// Datum och tid när meddelandet mottogs
    /// </summary>
    public DateTime DateReceived { get; set; }

    /// <summary>
    /// Lista med bilagor
    /// </summary>
    public List<EmailAttachment>? Attachments { get; set; }

    /// <summary>
    /// Unikt ID för e-postmeddelandet
    /// </summary>
    public string? MessageId { get; set; }
}

/// <summary>
/// Svar från e-postläsningen
/// </summary>
public class ReadUnreadEmailResponse
{
    /// <summary>
    /// Om operationen lyckades
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Eventuellt meddelande om resultatet
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Antal olästa meddelanden som fanns tillgängliga
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// De hämtade e-postmeddelandena
    /// </summary>
    public List<EmailMessage>? Emails { get; set; }
}
