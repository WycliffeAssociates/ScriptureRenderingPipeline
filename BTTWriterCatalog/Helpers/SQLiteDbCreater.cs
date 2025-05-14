using Microsoft.Data.Sqlite;

namespace BTTWriterCatalog.Helpers;

public class SQLiteDbCreater
{
    public void Create(string fileName)
    {
        // Create the sqlite db
        using var connection = new SqliteConnection($"Data Source={fileName}");
        connection.Open();

        // Create a table
        using var command = connection.CreateCommand();
        command.CommandText = "";
        command.ExecuteNonQuery();
    }
}