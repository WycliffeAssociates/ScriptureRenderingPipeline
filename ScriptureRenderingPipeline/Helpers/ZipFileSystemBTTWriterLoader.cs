using BTTWriterLib;
using BTTWriterLib.Models;
using PipelineCommon.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ScriptureRenderingPipeline.Helpers
{
    public class ZipFileSystemBTTWriterLoader : IResourceContainer
    {
        private ZipFileSystem fileSystem;
        private string baseDir;
        private BTTWriterManifest manifest;
        public ZipFileSystemBTTWriterLoader(ZipFileSystem fileSystem, string baseDir)
        {
            this.fileSystem = fileSystem;
            this.baseDir = baseDir;
            manifest = JsonSerializer.Deserialize<BTTWriterManifest>(fileSystem.ReadAllText(fileSystem.Join(baseDir, "manifest.json")));
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
