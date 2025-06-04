using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PipelineCommon.Models.Webhook;

namespace PipelineCommon.Helpers;

public class GiteaClient
{
    private readonly HttpClient _httpClient;
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
        var response = await _httpClient.GetAsync($"repos/{user}/{repo}");
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
    
    public async Task <Repository?> CreateRepositoryInOrganization(string organization, string repo)
    {
        var response = await _httpClient.PostAsJsonAsync($"orgs/{organization}/repos",
            new { name = repo, description = "Created by a merge" });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Repository>();
    }
    public async Task<bool> IsOrganization(string user)
    {
        var response = await _httpClient.GetAsync($"orgs/{user}");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (response.IsSuccessStatusCode)
        {
            return true;
        }
        throw new HttpRequestException($"Got an unexpected response from Gitea expected 200 or 404 but got {response.StatusCode}");
    }

    public async Task UploadMultipleFiles(string user, string repo, Dictionary<string, string> pathAndContent, string? branch = null)
    {

        var currentFiles = new List<string>();
        if (branch != null)
        {
            currentFiles = await GetAllFiles(user, repo);
        }
        
        var response = await _httpClient.PostAsJsonAsync($"repos/{user}/{repo}/contents", new UpdateMultipleFilesRequest
        {
            Branch = branch,
            Files = pathAndContent.Select(x => new UpdateMultipleFile
            {
                Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(x.Value)),
                Path = x.Key,
                Operation = currentFiles.Contains(x.Key) ? "update" : "create"
            }).ToList()
        });
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<string>> GetAllFiles(string user, string repo)
    {
        var response = await _httpClient.GetFromJsonAsync<List<Content>>($"repos/{user}/{repo}/contents");
        return response == null ? [] : response.Select(i => i.Path).ToList();
    }

    public async Task CreateBranch(string user, string repo, string branch)
    {
        var response = await _httpClient.PostAsJsonAsync($"repos/{user}/{repo}/branches",
            new NewBranchRequest() { NewBranchName = branch });
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> BranchExists(string user, string repo, string branch)
    {
        var response = await _httpClient.GetAsync($"repos/{user}/{repo}/branches/{branch}");
        
        // We don't want to say a repo isn't there if we got a 500 or something like that
        if (response.StatusCode != HttpStatusCode.NotFound && response.StatusCode != HttpStatusCode.OK)
        {
            throw new HttpRequestException($"Got an unexpected response from WACS expected 200 or 404 but got {response.StatusCode}");
        }
        return response.StatusCode == HttpStatusCode.OK;
    }
}

internal class Content
{
    public string Path { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
}

internal class NewBranchRequest
{
    [JsonPropertyName("new_branch_name")]
    public string NewBranchName { get; set; }
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