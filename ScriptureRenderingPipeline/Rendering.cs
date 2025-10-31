using System;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.IO.Compression;
using System.Linq;
using USFMToolsSharp;
using USFMToolsSharp.Renderers.Docx;
using System.Collections.Generic;
using USFMToolsSharp.Models.Markers;
using BTTWriterLib;
using USFMToolsSharp.Renderers.USFM;
using USFMToolsSharp.Renderers.Latex;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;

namespace ScriptureRenderingPipeline
{
    public class Rendering
    {
        [Function("RenderDoc")]
        public async Task<HttpResponseData> RenderDocAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "api/RenderDoc")] HttpRequestData req,
            FunctionContext context)
        {
            var log = context.GetLogger("RenderDoc");
            try
            {
                string[] validExtensions = { ".usfm", ".txt", ".sfm" };

                // default to docx if nobody gives us a file type
                string fileType = "docx";

                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                if (query["filetype"] != null)
                {
                    fileType = query["filetype"];
                }

                string url = BuildDownloadUrl(query);

                if (url == null)
                {
                    return await GenerateErrorAndLogAsync(req, "URL is blank", log);
                }

                string fileName = null;
                if (query["filename"] != null)
                {
                    fileName = query["filename"];
                }

                log.LogInformation("Rendering {Url}", url);

                string repoDir = await Utils.GetRepoFilesAsync(url, log);

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
                        if (validExtensions.Contains(Path.GetExtension(file)))
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
                    return await GenerateErrorAndLogAsync(req, "Doesn't look like this is a scripture repo. We were unable to find any USFM", log);
                }

                if (fileType == "docx")
                {
                    DocxConfig config = CreateDocxConfig(query);
                    DocxRenderer renderer = new DocxRenderer(config);
                    var output = renderer.Render(document);
                    string outputFilePath = Path.Join(repoDir, "output.docx");
                    using (var stream = new FileStream(outputFilePath, FileMode.Create))
                    {
                        output.Write(stream);
                    }
                    var outputStream = File.OpenRead(outputFilePath);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/octet-stream");
                    response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName ?? "output.docx"}\"");
                    using (var ms = new MemoryStream())
                    {
                        await outputStream.CopyToAsync(ms);
                        await response.WriteBytesAsync(ms.ToArray());
                    }
                    return response;
                }
                
                if (fileType == "usfm")
                {
                    string tempFolder = Utils.CreateTempFolder();
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
                            if (validExtensions.Contains(Path.GetExtension(file)))
                            {
                                File.Copy(file, Path.Join(tempFolder, Path.GetFileName(file)));
                            }
                        }
                    }
                    ZipFile.CreateFromDirectory(tempFolder, tempZipPath);
                    var outputStream = File.OpenRead(tempZipPath);
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/octet-stream");
                    response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName ?? "output.zip"}\"");
                    using (var ms = new MemoryStream())
                    {
                        await outputStream.CopyToAsync(ms);
                        await response.WriteBytesAsync(ms.ToArray());
                    }
                    return response;
                }

                if (fileType == "pdf")
                {
                    var latexConverterUrl = Environment.GetEnvironmentVariable("PDFConversionEndpoint");
                    var fontMappingUrl = Environment.GetEnvironmentVariable("FontLookupLocation");
                    if (string.IsNullOrEmpty(latexConverterUrl))
                    {
                        return await GenerateErrorAndLogAsync(req, $"PDF conversion was requested but converter url was not specified", log, 400);
                    }

                    if (string.IsNullOrEmpty(fontMappingUrl))
                    {
                        return await GenerateErrorAndLogAsync(req, $"PDF conversion was requested but font lookup file url was not specified", log, 400);
                    }

                    var renderer = await CreateLatexRendererAsync(fontMappingUrl, query, document, log);

                    var result = renderer.Render(document);
                    var pdfResult = await Utils.httpClient.PostAsync(latexConverterUrl, new StringContent(result));
                    if (!pdfResult.IsSuccessStatusCode)
                    {
                        return await GenerateErrorAndLogAsync(req, "Error rendering pdf", log);
                    }
                    var stream = await pdfResult.Content.ReadAsStreamAsync();
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/octet-stream");
                    response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName ?? "output.pdf"}\"");
                    using (var ms = new MemoryStream())
                    {
                        await stream.CopyToAsync(ms);
                        await response.WriteBytesAsync(ms.ToArray());
                    }
                    return response;
                }

                if (fileType == "latex")
                {
                    var fontMappingUrl = Environment.GetEnvironmentVariable("FontLookupLocation");
                    if (string.IsNullOrEmpty(fontMappingUrl))
                    {
                        return await GenerateErrorAndLogAsync(req, $"Latex conversion was requested but font lookup file url was not specified", log, 400);
                    }

                    var renderer = await CreateLatexRendererAsync(fontMappingUrl, query, document, log);

                    var result = renderer.Render(document);
                    var stream = new MemoryStream();
                    var writer = new StreamWriter(stream);
                    await writer.WriteAsync(result);
                    await writer.FlushAsync();
                    stream.Position = 0;
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    response.Headers.Add("Content-Type", "application/octet-stream");
                    response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName ?? "output.tex"}\"");
                    await response.WriteBytesAsync(stream.ToArray());
                    return response;
                }

                return await GenerateErrorAndLogAsync(req, $"Output type {fileType} is unsupported", log, 400);

            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Error rendering {ex.Message}");
                var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                response.Headers.Add("Content-Type", "text/html");
                await response.WriteStringAsync(GenerateErrorMessage(ex.Message));
                return response;
            }
        }
        private static async Task<LatexRenderer> CreateLatexRendererAsync(string fontMappingUrl, System.Collections.Specialized.NameValueCollection query, USFMDocument document, ILogger log)
        {
            var fonts = await GetFontsAsync(fontMappingUrl);
            var config = CreateLatexConfig(query);
            config.Font = SelectFontForDocument(document, fonts);
            log.LogInformation($"Selected {config.Font} for rendering");
            return new LatexRenderer(config);
        }

        private static async Task<HttpResponseData> GenerateErrorAndLogAsync(HttpRequestData req, string message, ILogger log, int errorCode = 500)
        {
            log.LogWarning(message);
            var response = req.CreateResponse((HttpStatusCode)errorCode);
            response.Headers.Add("Content-Type", "text/html");
            await response.WriteStringAsync(GenerateErrorMessage(message));
            return response;
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

        [Function("CheckRepoExists")]
        public async Task<HttpResponseData> CheckRepoAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "api/CheckRepoExists")] HttpRequestData req,
            FunctionContext context)
        {
            var log = context.GetLogger("CheckRepoExists");
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            if (query["url"] == null)
            {
                log.LogError("CheckRepoExists called without url");
                var errorResponse = req.CreateResponse(HttpStatusCode.OK);
                await errorResponse.WriteAsJsonAsync(false);
                return errorResponse;
            }
            var url = BuildDownloadUrl(query);
            var result = await Utils.httpClient.GetAsync(url);
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result.IsSuccessStatusCode);
            return response;
        }

        private static DocxConfig CreateDocxConfig(System.Collections.Specialized.NameValueCollection query)
        {
            DocxConfig config = new DocxConfig();

            if (query["columns"] != null)
            {
                if (int.TryParse(query["columns"], out int columns))
                {
                    config.columnCount = columns;
                }
            }

            if (query["lineSpacing"] != null)
            {
                if (double.TryParse(query["lineSpacing"], out double lineSpacing))
                {
                    config.lineSpacing = lineSpacing;
                }
            }

            if (query["direction"] != null)
            {
                if (query["direction"] == "rtl")
                {
                    config.rightToLeft = true;
                }
            }

            if (query["align"] != null)
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

            if (query["separateChapters"] != null)
            {
                config.separateChapters = query["separateChapters"] == "Y";
            }

            if (query["separateVerses"] != null)
            {
                config.separateVerses = query["separateVerses"] == "Y";
            }

            if (query["fontSize"] != null)
            {
                if (int.TryParse(query["fontSize"], out int fontSize))
                {
                    config.fontSize = fontSize;
                }
            }

            if (query["pageNumbers"] != null)
            {
                config.showPageNumbers = query["pageNumbers"] == "Y";
            }

            return config;

        }

        private static LatexRendererConfig CreateLatexConfig(System.Collections.Specialized.NameValueCollection query)
        {
            LatexRendererConfig config = new LatexRendererConfig();

            if (query["columns"] != null)
            {
                if (int.TryParse(query["columns"], out int columns))
                {
                    config.Columns = columns;
                }
            }

            if (query["lineSpacing"] != null)
            {
                if (double.TryParse(query["lineSpacing"], out double lineSpacing))
                {
                    config.LineSpacing = lineSpacing;
                }
            }

            if (query["separateChapters"] != null)
            {
                config.SeparateChapters = query["separateChapters"] == "Y";
            }

            if (query["separateVerses"] != null)
            {
                config.SeparateVerses = query["separateVerses"] == "Y";
            }

            return config;
        }

        /// <summary>
        /// Builds a download URL from query
        /// </summary>
        /// <param name="query">Incoming query from http request</param>
        /// <returns>The download url</returns>
        private static string BuildDownloadUrl(System.Collections.Specialized.NameValueCollection query)
        {
            if(query["url"] == null)
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
        static async Task<Dictionary<int,string>> GetFontsAsync(string url)
        {
            var result = await Utils.httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<Dictionary<string,string>>(result).ToDictionary(i=> int.Parse(i.Key), i=> i.Value);
        }
    }
}
