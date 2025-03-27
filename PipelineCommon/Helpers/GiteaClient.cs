using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using PipelineCommon.Models.Webhook;

namespace PipelineCommon.Helpers;

public class GiteaClient
{
    private HttpClient _httpClient;
    public GiteaClient(string baseUrl, string user, string password)
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(baseUrl + "/api/v1/");
        // Add basic auth
        var byteArray = System.Text.Encoding.UTF8.GetBytes($"{user}:{password}");
        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
    }

    public async Task<Repository?> GetRepository(string user, string repo)
    {
        var response = await _httpClient.GetAsync($"/repos/{user}/{repo}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        return await response.Content.ReadFromJsonAsync<Repository>();
    }

    public async Task<Repository?> CreateRepository(string user, string repo)
    {
        var response = await _httpClient.PostAsJsonAsync($"user/repos", new {name = repo, description = "Created by a merge"});
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Repository>();
    }

    public async Task UploadMultipleFiles(string user, string repo, Dictionary<string, string> pathAndContent, string branch = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"repos/{user}/{repo}/contents", new UpdateMultipleFilesRequest
        {
            Branch = branch,
            Files = pathAndContent.Select(x => new UpdateMultipleFile
            {
                Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(x.Value)),
                Path = x.Key,
                Operation = "create"
            }).ToList()
        });
    }
}
internal class UpdateMultipleFilesRequest
{
    public string Branch { get; set; }
    public List<UpdateMultipleFile> Files { get; set; }
}

internal class UpdateMultipleFile
{
    public string Content { get; set; }
    public string Path { get; set; }
    public string Operation { get; set; }
}