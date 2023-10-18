// See https://aka.ms/new-console-template for more information

using Azure.Storage.Blobs;
using CommandLine;
using CreateVerseCountsFromRepo;
using System.Text.Json;
using PipelineCommon.Helpers;
using PipelineCommon.Models;
using USFMToolsSharp.Models.Markers;

await Parser.Default.ParseArguments<Options>(args).WithParsedAsync(async options =>
{
    var output = new CountDefinitions();
    var client = new HttpClient();
    var stream = await client.GetStreamAsync($"{options.RepoUrl}/archive/master.zip");
    var fileSystem = new ZipFileSystem(stream);
    var result = await Utils.LoadUsfmFromDirectoryAsync(fileSystem);
    foreach (var item in result)
    {
        var bookIdentifier = item.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
        var chapters = item.GetChildMarkers<CMarker>();
        var outputBook = new BookCountDefinitions()
        {
            ExpectedChapters = chapters.Count
        };
        foreach (var chapter in chapters)
        {
            outputBook.ExpectedChapterCounts.Add(chapter.Number, Utils.CountUniqueVerses(chapter));
        }
        output.Books.Add(bookIdentifier.ToLower(), outputBook);
    }

    var blobContainerClient = new BlobContainerClient(options.ConnectionString, "versecounts");
    await blobContainerClient.CreateIfNotExistsAsync();
    var blobClient = blobContainerClient.GetBlobClient($"{options.LanguageCode}.json");
    var deleteTask = blobClient.DeleteIfExistsAsync();
    var outputStream = new MemoryStream();
    await JsonSerializer.SerializeAsync(outputStream, output);
    outputStream.Position = 0;
    await deleteTask;
    await blobClient.UploadAsync(outputStream);

});