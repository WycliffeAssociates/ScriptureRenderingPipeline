using System.Threading.Tasks;
using BTTWriterCatalog.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BTTWriterCatalog;

public class BuildIndexSqliteDb
{
    private readonly ILogger<BuildIndexSqliteDb> _logger;
    public BuildIndexSqliteDb(ILogger<BuildIndexSqliteDb> logger)
    {
        _logger = logger;
    }

    [Function("BuildIndexSqliteDb")]
    public async Task<IActionResult> ManualBuildAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        await using (var db = new SQLiteDbCreater("output.db"))
        {
            await db.Initialize();
        }
        return new OkResult();
    }
}