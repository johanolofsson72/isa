using System.Text;
using System.Text.Json;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace Fortnox;

public partial class Plugin
{
    [KernelFunction]
    [Description("Hämtar en lista med projekt från Fortnox. Lista projekt, visa projekt, sök projekt.")]
    public async Task<string> GetProjectsAsync()
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/projects";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode ? responseContent : $"ERROR: {responseContent}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching projects");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Hämtar ett projekt baserat på projektnummer. Visa projekt.")]
    public async Task<string> GetProjectAsync([Description("Projektnummer")] string projectNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/projects/{projectNumber}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode ? responseContent : $"ERROR: {responseContent}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when fetching project");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Skapar ett nytt projekt i Fortnox. Skapa projekt.")]
    public async Task<string> CreateProjectAsync([Description("Projektdata")] ProjectRequest project)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/projects";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var payload = JsonSerializer.Serialize(project, options);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode ? responseContent : $"ERROR: {responseContent}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when creating project");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Uppdaterar ett projekt i Fortnox baserat på projektnummer. Uppdatera projekt.")]
    public async Task<string> UpdateProjectAsync([Description("Projektnummer")] string projectNumber, [Description("Uppdaterad projektdata")] ProjectRequest project)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/projects/{projectNumber}";
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        var payload = JsonSerializer.Serialize(project, options);

        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode ? responseContent : $"ERROR: {responseContent}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when updating project");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Arkiverar ett projekt i Fortnox. Arkivera projekt.")]
    public async Task<string> ArchiveProjectAsync([Description("Projektnummer")] string projectNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/projects/{projectNumber}";

        // Minimal payload to set status to ARCHIVED
        var body = JsonSerializer.Serialize(new ProjectRequest
        {
            Project = new Project
            {
                ProjectNumber = projectNumber,
                Status = "ARCHIVED",
                Description = "" // Fortnox kräver beskrivning vid PUT – lämna tom om ok
            }
        });

        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode ? responseContent : $"ERROR: {responseContent}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when archiving project");
            return $"ERROR: {ex.Message}";
        }
    }

    [KernelFunction]
    [Description("Tar bort (inaktiverar) ett projekt i Fortnox baserat på projektnummer. Ta bort projekt.")]
    public async Task<string> DeleteProjectAsync([Description("Projektnummer")] string projectNumber)
    {
        var accessToken = await GetOrRefreshAccessTokenAsync();
        if (accessToken.StartsWith("ERROR"))
            return accessToken;

        var url = $"{_apiUrl}/projects/{projectNumber}";
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            return response.IsSuccessStatusCode ? responseContent : $"ERROR: {responseContent}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception when deleting project");
            return $"ERROR: {ex.Message}";
        }
    }
}