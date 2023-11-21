using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BTTWriterLib;
using BTTWriterLib.Models;
using PipelineCommon.Models;
using PipelineCommon.Models.ResourceContainer;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using YamlDotNet.Serialization;

namespace PipelineCommon.Helpers
{
    public static class Utils
    {
        // This exists because HttpClient is meant to be reused because it reuses connections
        public static HttpClient httpClient = new HttpClient();
        
        /// <summary>
        /// Generates a download link for a given repository.
        /// </summary>
        /// <param name="htmlUrl">The HTML URL of the repository.</param>
        /// <param name="user">The username of the repository owner.</param>
        /// <param name="repo">The name of the repository.</param>
        /// <returns>A string representing the download link for the repository.</returns>
        public static string GenerateDownloadLink(string htmlUrl, string user, string repo)
        {
            var downloadUri = new Uri(htmlUrl);
            return $"{downloadUri.Scheme}://{downloadUri.Host}/api/v1/repos/{user}/{repo}/archive/master.zip";
        }
        
        public static void DownloadRepo(string url, string repoDir, ILogger log)
        {
            string repoZipFile = Path.Join(CreateTempFolder(), url.Substring(url.LastIndexOf("/")));

            if (File.Exists(repoZipFile))
            {
                File.Delete(repoZipFile);
            }

            using (WebClient client = new WebClient())
            {
                log.LogInformation("Downloading {Url} to {RepoZipFile}", url, repoZipFile);
                client.DownloadFile(new Uri(url), repoZipFile);
            }

            log.LogInformation("Unzipping {RepoZipFile} to {RepoDir}", repoZipFile, repoDir);
            ZipFile.ExtractToDirectory(repoZipFile, repoDir);
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

        /// <summary>
        /// Create a temporary folder that has a unique name
        /// </summary>
        /// <returns>A unique folder under the temporary directory</returns>
        public static string CreateTempFolder()
        {
            string path = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// A list of bible books in order
        /// </summary>
        public static List<string> BibleBookOrder = new List<string>() {
            "GEN",
            "EXO",
            "LEV",
            "NUM",
            "DEU",
            "JOS",
            "JDG",
            "RUT",
            "1SA",
            "2SA",
            "1KI",
            "2KI",
            "1CH",
            "2CH",
            "EZR",
            "NEH",
            "EST",
            "JOB",
            "PSA",
            "PRO",
            "ECC",
            "SNG",
            "ISA",
            "JER",
            "LAM",
            "EZK",
            "DAN",
            "HOS",
            "JOL",
            "AMO",
            "OBA",
            "JON",
            "MIC",
            "NAM",
            "HAB",
            "ZEP",
            "HAG",
            "ZEC",
            "MAL",
            "MAT",
            "MRK",
            "LUK",
            "JHN",
            "ACT",
            "ROM",
            "1CO",
            "2CO",
            "GAL",
            "EPH",
            "PHP",
            "COL",
            "1TH",
            "2TH",
            "1TI",
            "2TI",
            "TIT",
            "PHM",
            "HEB",
            "JAS",
            "1PE",
            "2PE",
            "1JN",
            "2JN",
            "3JN",
            "JUD",
            "REV"
        };
        /// <summary>
        /// A mapping between bible book abbreviations and their English names. Note that this shouldn't exist and only does because
        /// of lack of localization for translationNotes and translationQuestions
        /// </summary>
        public static Dictionary<string, string> bookAbbreviationMappingToEnglish = new Dictionary<string, string>()
        {
            ["GEN"] = "Genesis",
            ["EXO"] = "Exodus",
            ["LEV"] = "Leviticus",
            ["NUM"] = "Numbers",
            ["DEU"] = "Deuteronomy",
            ["JOS"] = "Joshua",
            ["JDG"] = "Judges",
            ["RUT"] = "Ruth",
            ["1SA"] = "1 Samuel",
            ["2SA"] = "2 Samuel",
            ["1KI"] = "1 Kings",
            ["2KI"] = "2 Kings",
            ["1CH"] = "1 Chronicles",
            ["2CH"] = "2 Chronicles",
            ["EZR"] = "Ezra",
            ["NEH"] = "Nehemiah",
            ["EST"] = "Esther",
            ["JOB"] = "Job",
            ["PSA"] = "Psalms",
            ["PRO"] = "Proverbs",
            ["ECC"] = "Ecclesiastes",
            ["SNG"] = "Song of Songs",
            ["ISA"] = "Isaiah",
            ["JER"] = "Jeremiah",
            ["LAM"] = "Lamentations",
            ["EZK"] = "Ezekiel",
            ["DAN"] = "Daniel",
            ["HOS"] = "Hosea",
            ["JOL"] = "Joel",
            ["AMO"] = "Amos",
            ["OBA"] = "Obadiah",
            ["JON"] = "Jonah",
            ["MIC"] = "Micah",
            ["NAM"] = "Nahum",
            ["HAB"] = "Habakkuk",
            ["ZEP"] = "Zephaniah",
            ["HAG"] = "Haggai",
            ["ZEC"] = "Zechariah",
            ["MAL"] = "Malachi",
            ["MAT"] = "Matthew",
            ["MRK"] = "Mark",
            ["LUK"] = "Luke",
            ["JHN"] = "John",
            ["ACT"] = "Acts",
            ["ROM"] = "Romans",
            ["1CO"] = "1 Corinthians",
            ["2CO"] = "2 Corinthians",
            ["GAL"] = "Galatians",
            ["EPH"] = "Ephesians",
            ["PHP"] = "Philippians",
            ["COL"] = "Colossians",
            ["1TH"] = "1 Thessalonians",
            ["2TH"] = "2 Thessalonians",
            ["1TI"] = "1 Timothy",
            ["2TI"] = "2 Timothy",
            ["TIT"] = "Titus",
            ["PHM"] = "Philemon",
            ["HEB"] = "Hebrews",
            ["JAS"] = "James",
            ["1PE"] = "1 Peter",
            ["2PE"] = "2 Peter",
            ["1JN"] = "1 John",
            ["2JN"] = "2 John",
            ["3JN"] = "3 John",
            ["JUD"] = "Jude",
            ["REV"] = "Revelation",
        };

        /// <summary>
        /// Get the book number for a specific abbreviation
        /// </summary>
        /// <param name="bookAbbreviation">The abbreviation to look up</param>
        /// <returns>The book number or 0 if it isn't a valid book</returns>
        /// <remarks>Book number 40 is the apocrypha and is unused so that is why Matthew is 41</remarks>
        public static int GetBookNumber(string bookAbbreviation)
        {
            bookAbbreviation = bookAbbreviation.ToUpper();
            if (!BibleBookOrder.Contains(bookAbbreviation))
            {
                return 0;
            }
            var index = BibleBookOrder.IndexOf(bookAbbreviation) + 1;
            if (index >= 40)
            {
                index++;
            }
            return index;
        }

        /// <summary>
        /// A list of identifiers for bible books
        /// </summary>
        public static readonly List<string> BibleIdentifiers = new List<string>()
        {
            "ulb",
            "reg",
            "udb",
            "cuv",
            "uhb",
            "ugnt",
            "blv",
            "f10",
            "nav",
            "ayt",
            "rlv",
            "ust",
        };

        public static Dictionary<string, RepoType> RepoTypeMapping = new Dictionary<string, RepoType>()
        {
            ["tn"] = RepoType.translationNotes,
            ["tw"] = RepoType.translationWords,
            ["tq"] = RepoType.translationQuestions,
            ["ta"] = RepoType.translationAcademy,
            ["tm"] = RepoType.translationAcademy,
            ["obs"] = RepoType.OpenBibleStories,
            ["bc"] = RepoType.BibleCommentary,
        };

        /// <summary>
        /// Figures out what type a resource is based on it's identifier
        /// </summary>
        /// <param name="resourceIdentifier">The identifer to look up</param>
        /// <returns>The resource type</returns>
        public static RepoType GetRepoType(string resourceIdentifier)
        {
            var bibleIdentifiersFromEnvironment = Environment.GetEnvironmentVariable("BibleIdentifiers");
            if (bibleIdentifiersFromEnvironment != null)
            {
                if (bibleIdentifiersFromEnvironment.Split(",").Select(i => i.Trim()).Any(i => i == resourceIdentifier))
                {
                    return RepoType.Bible;
                }
            }
            if (BibleIdentifiers.Contains(resourceIdentifier))
            {
                return RepoType.Bible;
            }

            if (RepoTypeMapping.ContainsKey(resourceIdentifier))
            {
                return RepoTypeMapping[resourceIdentifier];
            }
            return RepoType.Unknown;
        }
        /// <summary>
        /// Upload files to Azure storage
        /// </summary>
        /// <param name="log"></param>
        /// <param name="connectionString"></param>
        /// <param name="outputContainer"></param>
        /// <param name="sourceDir"></param>
        /// <param name="basePath"></param>
        /// <returns></returns>
        public static async Task UploadToStorage(ILogger log, string connectionString, string outputContainer, IOutputInterface outDir, string basePath)
        {
            var extentionToMimeTypeMatching = new Dictionary<string, string>()
            {
                [".html"] = "text/html",
                [".json"] = "application/json",
            };
            BlobContainerClient outputClient = new BlobContainerClient(connectionString, outputContainer);
            outputClient.CreateIfNotExists();
            List<Task> uploadTasks = new List<Task>();
            foreach (var file in outDir.ListFilesInDirectory("", "*.*", SearchOption.AllDirectories))
            {
                var relativePath = outDir.GetRelativePath(file);
                var extension = Path.GetExtension(relativePath);
                log.LogDebug("Uploading {Path}", relativePath);
                var tmp = outputClient.GetBlobClient(Path.Join(basePath, relativePath).Replace("\\", "/"));
                await tmp.DeleteIfExistsAsync();
                var contentType = extentionToMimeTypeMatching.TryGetValue(extension, out var value) ? value : "application/octet-stream";
                uploadTasks.Add(tmp.UploadAsync(outDir.OpenRead(file), new BlobUploadOptions() { HttpHeaders = new BlobHttpHeaders() { ContentType = contentType } }));
            }
            await Task.WhenAll(uploadTasks);
        }

        public static List<string> TranslationWordsValidSections = new List<string>()
        {
            "kt",
            "names",
            "other"
        };

        public static Dictionary<string, string> TranslationWordsTitleMapping = new Dictionary<string, string>()
        {
            ["kt"] = "Key Terms",
            ["names"] = "Names",
            ["other"] = "Other",
        };
        public static async Task<RepoIdentificationResult> GetRepoInformation(ILogger log, IZipFileSystem fileSystem, string basePath, string repo)
        {
            string languageName = string.Empty;
            string resourceName = string.Empty;
            string languageCode = string.Empty;
            string languageDirection = string.Empty;
            RepoType repoType = RepoType.Unknown;
            bool isBTTWriterProject = false;
            ResourceContainer resourceContainer = null;
            if (fileSystem.FileExists(fileSystem.Join(basePath, "manifest.yaml")))
            {
                log.LogInformation("Found manifest.yaml file");
                var reader = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                try
                {
                    resourceContainer =
                        reader.Deserialize<ResourceContainer>(
                            await fileSystem.ReadAllTextAsync(fileSystem.Join(basePath, "manifest.yaml")));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error loading manifest.yaml {ex.Message}");
                }

                if (resourceContainer == null)
                {
                    throw new Exception("Bad manifest file");
                }

                if (resourceContainer?.dublin_core?.identifier != null)
                {
                    languageName = resourceContainer?.dublin_core?.language?.title;
                    resourceName = resourceContainer?.dublin_core?.title;
                    languageCode = resourceContainer?.dublin_core?.language?.identifier;
                    languageDirection = resourceContainer?.dublin_core?.language?.direction;
                    repoType = Utils.GetRepoType(resourceContainer?.dublin_core?.identifier);
                }
            }
            else if (fileSystem.FileExists(fileSystem.Join(basePath, "manifest.json")))
            {
                isBTTWriterProject = true;
                log.LogInformation("Found BTTWriter project");
                BTTWriterManifest manifest;
                try
                {
                    manifest = BTTWriterLoader.GetManifest(new ZipFileSystemBTTWriterLoader(fileSystem, basePath));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error loading BTTWriter manifest: {ex.Message}", ex);
                }

                languageName = manifest?.target_language?.name;
                languageCode = manifest?.target_language?.id;
                resourceName = manifest?.resource?.name;
                languageDirection = manifest?.target_language?.direction;
                var resourceId = manifest?.resource?.id;
                if (string.IsNullOrEmpty(resourceName))
                {
                    resourceName = resourceId;
                }


                repoType = Utils.GetRepoType(resourceId);
            }
            else
            {
                // Attempt to figure out what this is based on the name of the repo
                var split = repo.Split('_');
                if (split.Length > 1)
                {
                    var retrieveLanguageTask =
                        TranslationDatabaseInterface.GetLangagueAsync("https://td.unfoldingword.org/exports/langnames.json",
                            split[0]);
                    repoType = Utils.GetRepoType(split[1]);
                    if (repoType == RepoType.Unknown)
                    {
                        if (Utils.BibleBookOrder.Contains(split[1].ToUpper()))
                        {
                            repoType = RepoType.Bible;
                        }
                    }

                    var language = await retrieveLanguageTask;
                    languageName = language?.LanguageName ?? split[0];
                    languageCode = language?.LanguageCode ?? split[0];
                    resourceName = repoType.ToString();
                    Enum.GetName(repoType);
                    languageDirection = language?.Direction;
                }
            }

            return new RepoIdentificationResult()
            {
                repoType = repoType,
                isBTTWriterProject = isBTTWriterProject,
                ResourceContainer = resourceContainer,
                languageCode = languageCode,
                languageDirection = languageDirection,
                languageName = languageName,
                resourceName = resourceName,
            };
        }
        
    public static async Task<List<USFMDocument>> LoadUsfmFromDirectoryAsync(ZipFileSystem directory)
    {
        var parser = new USFMParser(new List<string> { "s5" }, true);
        var output = new List<USFMDocument>();
        foreach (var f in directory.GetAllFiles(".usfm"))
        {
            var tmp = parser.ParseFromString(await directory.ReadAllTextAsync(f));
            // If we don't have an abbreviation then try to figure it out from the file name
            var tableOfContentsMarkers = tmp.GetChildMarkers<TOC3Marker>();
            if (tableOfContentsMarkers.Count == 0)
            {
                var bookAbbreviation = GetBookAbbreviationFromFileName(f);
                if (bookAbbreviation != null)
                {
                    tmp.Insert(new TOC3Marker() { BookAbbreviation = bookAbbreviation });
                }
            }
            else if (Utils.GetBookNumber(tableOfContentsMarkers[0].BookAbbreviation) == 0)
            {
                var bookAbbreviation = GetBookAbbreviationFromFileName(f);
                if (bookAbbreviation != null)
                {
                    tableOfContentsMarkers[0].BookAbbreviation = bookAbbreviation;
                }
            }
            output.Add(tmp);
        }
        return output;
    }
    public static int CountUniqueVerses(CMarker chapter)
    {
        var verseSelection = new HashSet<int>();
        var verses = chapter.GetChildMarkers<VMarker>();
        foreach (var verse in verses)
        {
            if (verse.StartingVerse == verse.EndingVerse)
            {
                verseSelection.Add(verse.StartingVerse);
                continue;
            }

            for (var i = verse.StartingVerse; i <= verse.EndingVerse; i++)
            {
                verseSelection.Add(i);
            }
        }

        return verseSelection.Count;
    }
    public static string GetBookAbbreviationFromFileName(string f)
    {
        string bookAbbreviation = null;
        var fileNameSplit = Path.GetFileNameWithoutExtension(f).Split('-');
        if (fileNameSplit.Length == 2)
        {
            if (Utils.BibleBookOrder.Contains(fileNameSplit[1].ToUpper()))
            {
                bookAbbreviation = fileNameSplit[1].ToUpper();
            }
        }
        else if (fileNameSplit.Length == 1)
        {
            if (Utils.BibleBookOrder.Contains(fileNameSplit[0].ToUpper()))
            {
                bookAbbreviation = fileNameSplit[0].ToUpper();
            }
        }

        return bookAbbreviation;
    }
    }
	
    
    public enum RepoType
    {
        Unknown,
        Bible,
        translationWords,
        translationAcademy,
        translationQuestions,
        translationNotes,
        OpenBibleStories,
        BibleCommentary,
    }
}