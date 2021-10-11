using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PipelineCommon.Helpers
{
    public static class Utils
    {
        public static void DownloadRepo(string url, string repoDir, ILogger log)
        {
            string repoZipFile = Path.Join(CreateTempFolder(), url.Substring(url.LastIndexOf("/")));

            if (File.Exists(repoZipFile))
            {
                File.Delete(repoZipFile);
            }

            using (WebClient client = new WebClient())
            {
                log.LogInformation($"Downloading {url} to {repoZipFile}");
                client.DownloadFile(new Uri(url), repoZipFile);
            }

            log.LogInformation($"unzipping {repoZipFile} to {repoDir}");
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

        public static string CreateTempFolder()
        {
            string path = Path.Join(Path.GetTempPath() ,Guid.NewGuid().ToString());
            Directory.CreateDirectory(path);
            return path;
        }

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
        public static Dictionary<string, string> bookAbbrivationMappingToEnglish = new Dictionary<string, string>()
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
            ["1CH"] = "1 Chornicles",
            ["2CH"] = "2 Chornicles",
            ["EZR"] = "Ezra",
            ["NEH"] = "Nehemiah",
            ["EST"] = "Esther",
            ["JOB"] = "Job",
            ["PSA"] = "Psalms",
            ["PRO"] = "Proverbs",
            ["ECC"] = "Ecclesiastes",
            ["SNG"] = "Song of Songs",
            ["ISA"] = "Isiah",
            ["JER"] = "Jerimiah",
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
            ["ZEP"] = "Zeph1niah",
            ["HAG"] = "Haggai",
            ["ZEC"] = "Malachi",
            ["MAL"] = "Malachi",
            ["MAT"] = "Matthew",
            ["MRK"] = "Mark",
            ["LUK"] = "Luke",
            ["JHN"] = "John",
            ["ACT"] = "Acts",
            ["ROM"] = "Romans",
            ["1CO"] = "1 Corinthians",
            ["2CO"] = "2 Corinthians",
            ["GAL"] = "Galations",
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
            ["REV"] = "Revalations",
        };
        public static int GetBookNumber(string bookName)
        {
            bookName = bookName.ToUpper();
            if (!BibleBookOrder.Contains(bookName))
            {
                return 0;
            }
            var index = BibleBookOrder.IndexOf(bookName) + 1;
            if (index >= 40)
            {
                index++;
            }
            return index;
        }
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
        public static RepoType GetRepoType(string resourceIdentifier)
        {
            if (BibleIdentifiers.Contains(resourceIdentifier))
            {
                return RepoType.Bible;
            }
            if (resourceIdentifier == "tn")
            {
                return RepoType.translationNotes;
            }
            if (resourceIdentifier == "tw")
            {
                return RepoType.translationWords;
            }
            if (resourceIdentifier == "tq")
            {
                return RepoType.translationQuestions;
            }
            if (resourceIdentifier == "ta")
            {
                return RepoType.translationAcademy;
            }
            if (resourceIdentifier == "obs")
            {
                return RepoType.OpenBibleStories;
            }
            return RepoType.Unknown;
        }
        public static async Task UploadToStorage(ILogger log, string connectionString, string outputContainer, string sourceDir, string basePath)
        {
            var extentionToMimeTypeMatching = new Dictionary<string, string>()
            {
                [".html"] = "text/html",
                [".json"] = "application/json",
            };
            BlobContainerClient outputClient = new BlobContainerClient(connectionString, outputContainer);
            outputClient.CreateIfNotExists();
            List<Task> uploadTasks = new List<Task>();
            foreach(var file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir, file);
                var extension = Path.GetExtension(relativePath);
                log.LogDebug($"Uploading {relativePath}");
                var tmp = outputClient.GetBlobClient(Path.Join(basePath,relativePath ).Replace("\\","/"));
                tmp.DeleteIfExists();
                string contentType = extentionToMimeTypeMatching.ContainsKey(extension) ? extentionToMimeTypeMatching[extension] : "application/octet-stream";
                uploadTasks.Add(tmp.UploadAsync(file, new BlobUploadOptions() { HttpHeaders = new BlobHttpHeaders() { ContentType = contentType } }));
            };
            await Task.WhenAll(uploadTasks);
        }
        public static async Task<List<string>> ListAllFilesUnderPath(BlobContainerClient outputClient, string prefix)
        {
            var output = new List<string>();
            var stack = new Stack<string>(new List<string>() { prefix});
            while(stack.Count > 0)
            {
                var directory = stack.Pop();
                await foreach (var file in outputClient.GetBlobsByHierarchyAsync(prefix: directory, delimiter: "/"))
                {
                    if (file.IsBlob)
                    {
                        output.Add(file.Blob.Name);
                        continue;
                    }
                    // otherwise this is folder
                    stack.Push(file.Prefix);

                }
            }
            return output;
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
    }
    public enum RepoType
    {
        Unknown,
        Bible,
        bttWriterProject,
        translationWords,
        translationAcademy,
        translationQuestions,
        translationNotes,
        OpenBibleStories,
    }
}
