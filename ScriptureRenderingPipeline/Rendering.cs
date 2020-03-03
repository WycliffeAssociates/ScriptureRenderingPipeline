using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using System.IO.Compression;
using USFMToolsSharp;
using USFMToolsSharp.Renderers.Docx;
using System.Collections.Generic;
using USFMToolsSharp.Models.Markers;
using System.Linq;
using BTTWriterLib;

namespace ScriptureRenderingPipeline
{
    public static class Rendering
    {
        [FunctionName("RenderDoc")]
        public static async Task<IActionResult> RenderDoc(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string[] validExensions = { ".usfm", ".txt" };
            USFMParser parser = new USFMParser(new List<string> { "s5", "fqa*", "fq*" });
            DocxConfig config = CreateConfig(req.Query);
            USFMDocument document = new USFMDocument();
            DocxRenderer renderer = new DocxRenderer(config);

            string url = req.Query["url"].ToString().TrimEnd('/') + "/archive/master.zip";

            if (url == null)
            {
                return new BadRequestObjectResult("URL is blank");
            }

            log.LogInformation($"Rendering {url}");

            string repoDir = GetRepoFiles(url, log);

            if (File.Exists(Path.Combine(repoDir, "manifest.json")))
            {
                document = BTTWriterLoader.CreateUSFMDocumentFromContainer(new FileSystemResourceContainer(repoDir), false);
            }
            else
            {
                foreach(var file in Directory.GetFiles(repoDir, "*.*", SearchOption.AllDirectories))
                {
                    if (validExensions.Contains(Path.GetExtension(file)))
                    {
                        document.Insert(parser.ParseFromString(File.ReadAllText(file)));
                    }
                }
            }


            var output = renderer.Render(document);
            string outputFilePath = Path.Join(repoDir, "output.docx");
            using (var stream = new FileStream(outputFilePath, FileMode.Create))
            {
                output.Write(stream);
            }
            var outputStream = File.OpenRead(outputFilePath);
            return new FileStreamResult(outputStream, "application/octet-stream")
            {
                FileDownloadName = "output.docx"
            };
        }
        private static DocxConfig CreateConfig(IQueryCollection query)
        {
            DocxConfig config = new DocxConfig();

            if (query.ContainsKey("columns"))
            {
                if (int.TryParse(query["columns"], out int columns))
                {
                    config.columnCount = columns;
                }
            }

            if (query.ContainsKey("lineSpacing"))
            {
                if(double.TryParse(query["lineSpacing"], out double lineSpacing))
                {
                    config.lineSpacing = lineSpacing;
                }
            }

            if (query.ContainsKey("direction"))
            {
                if(query["direction"] == "rtl")
                {
                    config.textDirection = NPOI.OpenXmlFormats.Wordprocessing.ST_TextDirection.tbRl;
                }
            }

            if (query.ContainsKey("align"))
            {
                switch (query["align"])
                {
                    case "left":
                        config.textAlign = NPOI.XWPF.UserModel.ParagraphAlignment.LEFT;
                        break;
                    case "right":
                        config.textAlign = NPOI.XWPF.UserModel.ParagraphAlignment.RIGHT;
                        break;
                    case "center":
                        config.textAlign = NPOI.XWPF.UserModel.ParagraphAlignment.CENTER;
                        break;
                }
            }

            if (query.ContainsKey("separateChapters"))
            {
                config.separateChapters = true;
            }

            if (query.ContainsKey("separateVerses"))
            {
                config.separateVerses = true;
            }

            if (query.ContainsKey("fontSize"))
            {
                if(int.TryParse(query["fontSize"], out int fontSize))
                {
                    config.fontSize = fontSize;
                }
            }

            return config;

        }

        public static string GetRepoFiles(string commitUrl, ILogger log)
        {
            string tempDir = CreateTempFolder();
            DownloadRepo(commitUrl, tempDir, log);
            string repoDir = Path.Join(tempDir, Guid.NewGuid().ToString());
            if (!Directory.Exists(repoDir))
            {
                repoDir = tempDir;
            }
            return repoDir;
        }
        public static string CreateTempFolder()
        {
            string path = Path.GetTempPath() + Guid.NewGuid();
            Directory.CreateDirectory(path);
            return path;
        }

        public static void DownloadRepo(string url, string repoDir, ILogger log)
        {
            string repoZipFile = Path.Join(Path.GetTempPath(), url.Substring(url.LastIndexOf("/")));

            if (File.Exists(repoZipFile))
            {
                File.Delete(repoZipFile);
            }

            using (WebClient client = new WebClient())
            {
                client.DownloadFile(new Uri(url), repoZipFile);
            }

            ZipFile.ExtractToDirectory(repoZipFile, repoDir);
        }
    }
}
