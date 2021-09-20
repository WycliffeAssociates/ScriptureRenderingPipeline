using BTTWriterCatalog.Models;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.Text;

namespace BTTWriterCatalog.ContentConverters
{
    public class TranslationWords
    {
        public static void Convert(ZipFileSystem fileSystem, string basePath, string outputPath, ResourceContainer resourceContainer, Dictionary<string,List<Door43Chunk>> chunks)
        {
            var projectPath = resourceContainer.projects[0].path;
            //var categories = LoadWords(fileSystem, fileSystem.Join(basePath, projectPath));
        }
    }
}
