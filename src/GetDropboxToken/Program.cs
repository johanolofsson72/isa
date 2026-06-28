using System.Net;
using System.Net.Http.Headers;
using System.Text;

var clientId = "REDACTED";
var clientSecret = "REDACTED";
var redirectUri = "http://localhost:5555/callback";
var port = 5555;

// 1. Starta enkel HTTP-server för att ta emot redirect
var listener = new HttpListener();
listener.Prefixes.Add($"http://localhost:{port}/callback/");
listener.Start();
Console.WriteLine($"🚀 Lyssnar på {redirectUri}");

// 2. Skapa URL för användarens inloggning
var authorizeUrl = $"https://www.dropbox.com/oauth2/authorize?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&token_access_type=offline";
Console.WriteLine("🌐 Öppnar browser...");
System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
{
    FileName = authorizeUrl,
    UseShellExecute = true
});

// 3. Vänta på inkommande request från Dropbox
var context = await listener.GetContextAsync();
var response = context.Response;
var code = context.Request.QueryString["code"];

if (string.IsNullOrEmpty(code))
{
    var error = context.Request.QueryString["error_description"] ?? "Okänt fel";
    Console.WriteLine($"❌ Inloggningen misslyckades: {error}");
    var failMessage = Encoding.UTF8.GetBytes("Dropbox-inloggning misslyckades.");
    response.OutputStream.Write(failMessage, 0, failMessage.Length);
    response.Close();
    return;
}

// 4. Svara i browsern
var responseMessage = Encoding.UTF8.GetBytes("Inloggning lyckades! Du kan stänga detta fönster.");
response.OutputStream.Write(responseMessage, 0, responseMessage.Length);
response.Close();
listener.Stop();

Console.WriteLine($"✅ Mottog auth code: {code}");

// 5. Skicka tokenförfrågan
using var http = new HttpClient();
var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/oauth2/token");

tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
{
    { "code", code },
    { "grant_type", "authorization_code" },
    { "redirect_uri", redirectUri },
    { "client_id", clientId },
    { "client_secret", clientSecret }
});

var tokenResponse = await http.SendAsync(tokenRequest);
var content = await tokenResponse.Content.ReadAsStringAsync();

if (!tokenResponse.IsSuccessStatusCode)
{
    Console.WriteLine("❌ Fel vid tokenutbyte:");
    Console.WriteLine(content);
    return;
}

Console.WriteLine("\n🔐 TOKENER:");
Console.WriteLine(content);
Console.WriteLine("\n👉 Kopiera refresh_token till din appsettings.json under \"Dropbox:RefreshToken\".");
