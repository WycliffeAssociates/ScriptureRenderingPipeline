using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BTTWriterLib;
using BTTWriterLib.Models;
using System.Text.Json;

namespace PipelineCommon.Helpers
{
    public class ZipFileSystemBTTWriterLoader : IResourceContainer
    {
        private IZipFileSystem fileSystem;
        private string baseDir;
        private BTTWriterManifest manifest;
        public ZipFileSystemBTTWriterLoader(IZipFileSystem fileSystem, string baseDir)
        {
            this.fileSystem = fileSystem;
            this.baseDir = baseDir;
            try
            {
                manifest = JsonSerializer.Deserialize(fileSystem.ReadAllText(fileSystem.Join(baseDir, "manifest.json")),
                    HelpersJsonContext.Default.MinimalBTTWriterManifest).AsBTTWriterManifest();
            }
            catch (Exception e)
            {
                throw new Exception("Problem loading manifest", e);
            }
        }
        public string GetFile(string fileName)
        {
            fileName = fileName.Replace("-", "/") + ".txt";
            if (!fileSystem.FileExists(fileSystem.Join( baseDir, fileName)))
            {
                return null;
            }
            return fileSystem.ReadAllText(fileSystem.Join(baseDir, fileName));
        }

        public List<string> GetFiles(bool onlyFinished)
        {
            if (onlyFinished)
            {
                return manifest.finished_chunks.ToList();
            }
            return fileSystem.GetAllFiles(".txt").Select(e => $"{e.Split('/')[^2]}-{Path.GetFileNameWithoutExtension(e)}").ToList();
        }

        public BTTWriterManifest GetManifest()
        {
            return manifest;
        }
    }
}
