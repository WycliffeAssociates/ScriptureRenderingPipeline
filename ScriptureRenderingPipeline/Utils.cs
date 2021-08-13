using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;

namespace ScriptureRenderingPipeline
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
    }
}
