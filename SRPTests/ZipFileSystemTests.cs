using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using PipelineCommon.Helpers;

namespace SRPTests;

public class ZipFileSystemTests
{
    private MemoryStream stream;
    private static string testFileName = "test.txt";
    private static string testFileContents = "test";
    private static string nestedFolder = "folder";
    private static string nestedFileName = $"{nestedFolder}nested.txt";
    [SetUp]
    public void SetUp()
    {
        stream = new MemoryStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, true);
        AddFileToZip(archive, testFileName, testFileContents);
        AddFileToZip(archive, "test.log", "log contents");
        AddFileToZip(archive, nestedFileName, "nested file name");
        AddFolderToZip(archive, nestedFolder);
        stream.Position = 0;
    }

    private void AddFileToZip(ZipArchive archive, string filePath, string fileContents)
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), "tmp.txt");
        var file = File.Create(tmpPath);
        using var writer = new StreamWriter(file);
        writer.Write(fileContents);
        writer.Flush();
        writer.Close();
        var fileEntry = archive.CreateEntryFromFile(tmpPath, filePath);
        File.Delete(tmpPath);
    }

    private void AddFolderToZip(ZipArchive archive, string folderPath)
    {
        var entry = archive.CreateEntry(folderPath);
        var folderStream = entry.Open();
        folderStream.Close();
    }

    [Test]
    public void TestGetAllFiles()
    {
        var fileSystem = new ZipFileSystem(stream);
        var allFiles = fileSystem.GetAllFiles().ToList();
        var allLogs = fileSystem.GetAllFiles(".log").ToList();
        Assert.AreEqual(3, allFiles.Count());
        Assert.AreEqual("test.txt", allFiles[0]);
        Assert.AreEqual(1, allLogs.Count);
        fileSystem.Close();
    }

    [Test]
    public void TestFileExists()
    {
        var fileSystem = new ZipFileSystem(stream);
        Assert.IsTrue(fileSystem.FileExists(testFileName));
        Assert.IsFalse(fileSystem.FileExists("RandomName.txt"));
    }

    [Test]
    public void TestGetFolders()
    {
        var fileSystem = new ZipFileSystem(stream);
        var folders = fileSystem.GetFolders().ToList();
        Assert.AreEqual(1, folders.Count);
    }

    /*
     // I think there is a bug here in the zip archive in .net that is causing this to fail https://github.com/dotnet/runtime/issues/49580
    [Test]
    public void TestReadAllText()
    {
        var fileSystem = new ZipFileSystem(stream);
        Assert.AreEqual(testFileContents, fileSystem.ReadAllText(testFileName));
        fileSystem.Close();
    }
    */
}