using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog
{
    public static class UnfoldingWordCatalogGenerator
    {
        [FunctionName("UWCatalogManualBuild")]
        public static IActionResult ManualBuild([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
        {
            return new OkResult();
        }
    }
}
