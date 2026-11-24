using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PipelineCommon.Helpers;

namespace ScriptureRenderingPipelineWorker;

/// <summary>
/// A wrapper for the output interface that also logs the output. This is something we'll do commonly so it makes sense to have a wrapper for it.
/// </summary>
public class OutputAndLoggingWrapper
{
    private IOutputInterface Output { get; set; }
    private IRenderLogger? Logger { get; set; }
    public OutputAndLoggingWrapper(IOutputInterface output, IRenderLogger? logger)
    {
        Output = output;
        Logger = logger;
    }
    public void LogWarning(string message)
    {
        Logger?.LogWarning(message);
    }
    
    public void LogError(string message)
    {
        Logger?.LogError(message);
    }
    public void LogTitle(string item, string title)
    {
        Logger?.LogTitle(item, title);
    }
    
    public Task WriteAllTextAsync(string path, string content, Dictionary<string, object>? metadata = null)
    {
        return WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(content), metadata);
    }

    /// <summary>
    /// Serialize straight to UTF-8 so a large file like download.json never exists as a UTF-16 string as well
    /// </summary>
    public Task WriteJsonAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo, Dictionary<string, object>? metadata = null)
    {
        return WriteAllBytesAsync(path, JsonSerializer.SerializeToUtf8Bytes(value, typeInfo), metadata);
    }

    /// <summary>
    /// The logger and the output share the same bytes so each file is only encoded once
    /// </summary>
    private async Task WriteAllBytesAsync(string path, byte[] content, Dictionary<string, object>? metadata)
    {
        Logger?.LogFile(path, content, metadata);
        await Output.WriteAllBytesAsync(path, content);
    }


    public Task FinishAsync()
    {
        return Output.FinishAsync();
    }
}