using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net;
using System.IO.Compression;
using USFMToolsSharp;
using USFMToolsSharp.Renderers.Docx;
using System.Collections.Generic;
using USFMToolsSharp.Models.Markers;
using System.Linq;
using BTTWriterLib;
using USFMToolsSharp.Renderers.USFM;
using USFMToolsSharp.Renderers.Latex;
using System.Text.Json;

namespace ScriptureRenderingPipeline
{
    public static class Rendering
    {
        [FunctionName("RenderDoc")]
        public static async Task<IActionResult> RenderDoc(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            try
            {
                string[] validExensions = { ".usfm", ".txt", ".sfm" };

                // default to docx if nobody gives us a file type
                string fileType = "docx";

                if (req.Query.ContainsKey("filetype"))
                {
                    fileType = req.Query["filetype"];
                }

                string url = BuildDownloadUrl(req.Query);

                if (url == null)
                {
                    return GenerateErrorAndLog("URL is blank", log);
                }

                string fileName = null;
                if (req.Query.ContainsKey("filename"))
                {
                    fileName = req.Query["filename"];
                }

                log.LogInformation($"Rendering {url}");

                string repoDir = GetRepoFiles(url, log);

                USFMParser parser = new USFMParser(new List<string> { "s5", "fqa*", "fq*" });
                USFMDocument document = new USFMDocument();
                bool isBTTWriter = false;

                if (File.Exists(Path.Combine(repoDir, "manifest.json")))
                {
                    isBTTWriter = true;
                    document = BTTWriterLoader.CreateUSFMDocumentFromContainer(new FileSystemResourceContainer(repoDir), false);
                }
                else
                {
                    foreach (var file in Directory.GetFiles(repoDir, "*.*", SearchOption.AllDirectories))
                    {
                        if (validExensions.Contains(Path.GetExtension(file)))
                        {
                            try
                            {
                                document.Insert(parser.ParseFromString(File.ReadAllText(file)));
                            }
                            catch (Exception ex)
                            {
                                throw new Exception($"Error parsing {file}", ex);
                            }
                        }
                    }
                }
                
                if(document.Contents.Count == 0)
                {
                    return GenerateErrorAndLog("Doesn't look like this is a scripture repo. We were unable to find any USFM", log);
                }

                if (fileType == "docx")
                {
                    DocxConfig config = CreateDocxConfig(req.Query);
                    DocxRenderer renderer = new DocxRenderer(config);
                    var output = renderer.Render(document);
                    string outputFilePath = Path.Join(repoDir, "output.docx");
                    using (var stream = new FileStream(outputFilePath, FileMode.Create))
                    {
                        output.Write(stream);
                    }
                    var outputStream = File.OpenRead(outputFilePath);
                    return new FileStreamResult(outputStream, "application/octet-stream")
                    {
                        FileDownloadName = fileName ?? "output.docx",
                    };
                }
                
                if (fileType == "usfm")
                {
                    string tempFolder = CreateTempFolder();
                    string tempZipPath = Path.Join(repoDir, "output.zip");
                    if (isBTTWriter)
                    {
                        USFMRenderer renderer = new USFMRenderer();
                        var output = renderer.Render(document);
                        var idMarkers = document.GetChildMarkers<IDMarker>();
                        string usfmFileName = idMarkers.Count == 0 ? "document.usfm" : idMarkers[0].TextIdentifier + ".usfm";
                        File.WriteAllText(Path.Join(tempFolder, usfmFileName), output);
                    }
                    else
                    {
                        foreach (var file in Directory.GetFiles(repoDir, "*.*", SearchOption.AllDirectories))
                        {
                            if (validExensions.Contains(Path.GetExtension(file)))
                            {
                                File.Copy(file, Path.Join(tempFolder, Path.GetFileName(file)));
                            }
                        }
                    }
                    ZipFile.CreateFromDirectory(tempFolder, tempZipPath);
                    var outputStream = File.OpenRead(tempZipPath);
                    return new FileStreamResult(outputStream, "application/octet-stream")
                    {
                        FileDownloadName = fileName ?? "output.zip",
                    };
                }

                if (fileType == "pdf")
                {
                    var latexConverterUrl = Environment.GetEnvironmentVariable("PDFConversionEndpoint");
                    var fontMappingUrl = Environment.GetEnvironmentVariable("FontLookupLocation");
                    if (string.IsNullOrEmpty(latexConverterUrl))
                    {
                        return GenerateErrorAndLog($"PDF conversion was requested but converter url was not specified", log, 400);
                    }

                    if (string.IsNullOrEmpty(fontMappingUrl))
                    {
                        return GenerateErrorAndLog($"PDF conversion was requested but font lookup file url was not specified", log, 400);
                    }

                    // Find out which font we should use in the pdf
                    var fonts = await GetFonts(fontMappingUrl);
                    var config = CreateLatexConfig(req.Query);
                    config.Font = SelectFontForDocument(document, fonts);
                    log.LogInformation($"Selected {config.Font} for rendering");

                    LatexRenderer renderer = new LatexRenderer(config);
                    var result = renderer.Render(document);
                    HttpClient client = new HttpClient();
                    var pdfResult = await client.PostAsync(latexConverterUrl, new StringContent(result));
                    if (!pdfResult.IsSuccessStatusCode)
                    {
                        return GenerateErrorAndLog("Error rendering pdf", log);
                    }
                    var stream = await pdfResult.Content.ReadAsStreamAsync();
                    return new FileStreamResult(stream, "application/octet-stream")
                    {
                        FileDownloadName = fileName ?? "output.pdf"
                    };
                }

                if (fileType == "latex")
                {
                    var fontMappingUrl = Environment.GetEnvironmentVariable("FontLookupLocation");
                    if (string.IsNullOrEmpty(fontMappingUrl))
                    {
                        return GenerateErrorAndLog($"Latex conversion was requested but font lookup file url was not specified", log, 400);
                    }

                    // Find out which font we should use in the latex file
                    var fonts = await GetFonts(fontMappingUrl);
                    var config = CreateLatexConfig(req.Query);
                    config.Font = SelectFontForDocument(document, fonts);
                    log.LogInformation($"Selected {config.Font} for rendering");

                    LatexRenderer renderer = new LatexRenderer(config);
                    var result = renderer.Render(document);
                    var stream = new MemoryStream();
                    var writer = new StreamWriter(stream);
                    writer.Write(result);
                    writer.Flush();
                    stream.Position = 0;
                    return new FileStreamResult(stream, "application/octet-stream")
                    {
                        FileDownloadName = fileName ?? "output.tex"
                    };
                }

                return GenerateErrorAndLog($"Output type {fileType} is unsupported", log, 400);

            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Error rendering {ex.Message}");
                return new ContentResult() { Content = GenerateErrorMessage(ex.Message), ContentType = "text/html", StatusCode = 500 };
            }
        }

        private static ContentResult GenerateErrorAndLog(string message, ILogger log, int errorCode = 500)
        {
            log.LogWarning(message);
            return new ContentResult() { Content = GenerateErrorMessage(message), ContentType = "text/html", StatusCode = errorCode };
        }
        private static string GenerateErrorMessage(string message)
        {
            return "<html>" +
            "<head>" +
            "<title>A problem occured</title>" +
            "</head>" +
            "<body>" +
            "<h1> A problem occured in rendering</h1>" +
            $"<dif>Details: {message}</div>" +
            "</body>" +
            "</html>";
        }

        [FunctionName("CheckRepoExists")]
        public static async Task<IActionResult> CheckRepo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            if (!req.Query.ContainsKey("url"))
            {
                log.LogError("CheckRepoExists called without url");
                return new OkObjectResult(false);
            }
            var url = BuildDownloadUrl(req.Query);
            HttpClient client = new HttpClient();
            var result = client.GetAsync(url);
            if (!result.Result.IsSuccessStatusCode)
            {
                return new OkObjectResult(false);
            }
            return new OkObjectResult(true);
        }

        private static DocxConfig CreateDocxConfig(IQueryCollection query)
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
                if (double.TryParse(query["lineSpacing"], out double lineSpacing))
                {
                    config.lineSpacing = lineSpacing;
                }
            }

            if (query.ContainsKey("direction"))
            {
                if (query["direction"] == "rtl")
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
                config.separateChapters = query["separateChapters"] == "Y";
            }

            if (query.ContainsKey("separateVerses"))
            {
                config.separateVerses = query["separateVerses"] == "Y";
            }

            if (query.ContainsKey("fontSize"))
            {
                if (int.TryParse(query["fontSize"], out int fontSize))
                {
                    config.fontSize = fontSize;
                }
            }

            if (query.ContainsKey("pageNumbers"))
            {
                config.showPageNumbers = query["pageNumbers"] == "Y";
            }

            return config;

        }

        private static LatexRendererConfig CreateLatexConfig(IQueryCollection query)
        {
            LatexRendererConfig config = new LatexRendererConfig();

            if (query.ContainsKey("columns"))
            {
                if (int.TryParse(query["columns"], out int columns))
                {
                    config.Columns = columns;
                }
            }

            if (query.ContainsKey("lineSpacing"))
            {
                if (double.TryParse(query["lineSpacing"], out double lineSpacing))
                {
                    config.LineSpacing = lineSpacing;
                }
            }

            if (query.ContainsKey("separateChapters"))
            {
                config.SeparateChapters = query["separateChapters"] == "Y";
            }

            if (query.ContainsKey("separateVerses"))
            {
                config.SeparateVerses = query["separateVerses"] == "Y";
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
            // Need to grab the first dir out of the zip
            return Directory.EnumerateDirectories(repoDir).First();
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

        /// <summary>
        /// Builds a download URL from query
        /// </summary>
        /// <param name="query">Incoming query from http request</param>
        /// <returns>The download url</returns>
        private static string BuildDownloadUrl(IQueryCollection query)
        {
            if((string)query["url"] == null)
            {
                return null;
            }
            string url = query["url"].ToString().TrimEnd('/');
            if (url.EndsWith(".git"))
            {
                url = url.Substring(0, url.Length - 4);
            }

            url += "/archive/master.zip";

            return url;
        }

        /// <summary>
        /// Selects a font for a USFM document based on a provided mapping
        /// </summary>
        /// <param name="document">USFM document to search</param>
        /// <param name="fonts">Dictionary of char to font mapping</param>
        /// <returns></returns>
        static string SelectFontForDocument(USFMDocument document, Dictionary<int,string> fonts)
        {
            char[] ignoredChars = new[] { ' ' };
            int[] ignoredCharsAsInt = ignoredChars.Select(i => (int)i).ToArray();
            List<int> chars = new List<int>(50);

            foreach (var block in document.GetChildMarkers<TextBlock>())
            {
                foreach (var i in block.Text)
                {
                    if (i < 32 || ignoredChars.Contains(i))
                    {
                        continue;
                    }
                    if (chars.Contains(i))
                    {
                        continue;
                    }
                    chars.Add(i);
                }
            }

            var result = CalculateFontCounts(chars, fonts);

            if (result.Count == 0)
            {
                return null;
            }

            return result.OrderByDescending(r => r.Value).First().Key;
        }

        /// <summary>
        /// Calculates a count of number of chars in a font
        /// </summary>
        /// <param name="chars">List of chars in document</param>
        /// <param name="fonts">Mapping of chars to fonts</param>
        /// <returns></returns>
        static Dictionary<string, int> CalculateFontCounts(List<int> chars, Dictionary<int,string> fonts)
        {
            Dictionary<string, int> output = new Dictionary<string, int>();
            foreach(var i in chars)
            {
                if (!fonts.ContainsKey(i))
                {
                    continue;
                }

                string selectedFont = fonts[i];
                if (!output.ContainsKey(selectedFont))
                {
                    output.Add(selectedFont, 1);
                    continue;
                }

                output[selectedFont]++;
            }

            return output;
        }
        /// <summary>
        /// Loads a char/font list from a url
        /// </summary>
        /// <param name="url">Path to the mapping file</param>
        /// <returns>A dictionary of char number to font</returns>
        static async Task<Dictionary<int,string>> GetFonts(string url)
        {
            HttpClient client = new HttpClient();
            var result = await client.GetStringAsync(url);
            return JsonSerializer.Deserialize<Dictionary<string,string>>(result).ToDictionary(i=> int.Parse(i.Key), i=> i.Value);
        }
    }
}
