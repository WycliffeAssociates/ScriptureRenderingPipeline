using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PipelineCommon.Helpers
{
    public static class CloudStorageUtils
    {
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

        /// <summary>
        /// Upload files to Azure storage
        /// </summary>
        /// <param name="log"></param>
        /// <param name="connectionString"></param>
        /// <param name="outputContainer"></param>
        /// <param name="sourceDir"></param>
        /// <param name="basePath"></param>
        /// <returns></returns>
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
                log.LogDebug("Uploading {Path}", relativePath);
                var tmp = outputClient.GetBlobClient(Path.Join(basePath,relativePath ).Replace("\\","/"));
                tmp.DeleteIfExists();
                string contentType = extentionToMimeTypeMatching.TryGetValue(extension, out var value) ? value : "application/octet-stream";
                uploadTasks.Add(tmp.UploadAsync(file, new BlobUploadOptions() { HttpHeaders = new BlobHttpHeaders() { ContentType = contentType } }));
            }
            await Task.WhenAll(uploadTasks);
        }
    }
}
