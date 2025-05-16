using System;
using System.IO;
using System.Threading.Tasks;
using BTTWriterCatalog.Models.DataModel;
using Google.Protobuf.Compiler;
using Microsoft.Data.Sqlite;

namespace BTTWriterCatalog.Helpers;

public class SQLiteDbCreater: IDisposable, IAsyncDisposable
{
    private string _fileName;
    private SqliteConnection _connection;

    public SQLiteDbCreater(string fileName)
    {
        _fileName = fileName;
        _connection = new SqliteConnection($"Data Source={fileName}");
        _connection.Open();
    }
    
    public async Task Initialize()
    {
        // Create a table
        await using var command = _connection.CreateCommand();
        var initialScript = await File.ReadAllTextAsync("downloadSchema.sqlite");
        command.CommandText = initialScript;
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddSourceLanguage(string slug, string name, string direction)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "INSERT INTO source_language (slug, name, direction) VALUES (@slug, @name, @direction)";
        command.Parameters.AddWithValue("@slug", slug);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@direction", direction);
        await command.ExecuteNonQueryAsync();
    }
    public async Task AddTargetLanguage(string slug, string name, string direction, string anglicizedName, string region, bool isGateway)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "INSERT INTO target_language (slug, name, anglicized_name, direction, region, is_gateway )" 
                              + " VALUES (@slug, @name, @anglicized_name, @direction, @region, @is_gateway)";
        command.Parameters.AddWithValue("@slug", slug);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@direction", direction);
        command.Parameters.AddWithValue("@anglicized_name", anglicizedName);
        command.Parameters.AddWithValue("@region", region);
        command.Parameters.AddWithValue("@is_gateway", isGateway);
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddResource(ScriptureResourceModel resource)
    {
        
    }

    public void Dispose()
    {
        _connection.Close();
        _connection?.Dispose();
        
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}