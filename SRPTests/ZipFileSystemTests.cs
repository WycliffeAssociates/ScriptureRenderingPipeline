using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using PipelineCommon.Helpers;

namespace SRPTests;

/// <summary>
/// Test for the ZipFileSystem
/// </summary>
public class ZipFileSystemTests
{
    private MemoryStream stream;
    private static string testFileName = "test.txt";
    private static string testFileContents = "test";
    private static string nestedFolder = "folder";
    private static string furtherNestedFolder = $"{nestedFolder}/folder";
    private static string nestedFileName = $"{nestedFolder}/nested.txt";
    private static string otherNestedFileName = $"{nestedFolder}/nested.oth";
    
    /// <summary>
    /// Initialize things for tests
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            AddFileToZip(archive, testFileName, testFileContents);
            AddFileToZip(archive, "test.log", "log contents");
            AddFileToZip(archive, nestedFileName, "nested file");
            AddFileToZip(archive, otherNestedFileName, "other nested file");
            AddFolderToZip(archive, nestedFolder);
            AddFolderToZip(archive, furtherNestedFolder);
        }
        stream.Position = 0;
    }

    /// <summary>
    /// Add a file to the zip
    /// </summary>
    /// <param name="archive">The archive to add it to</param>
    /// <param name="filePath">The path of the file to add</param>
    /// <param name="fileContents">The contents of the file</param>
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

    /// <summary>
    /// Add a folder to the zip
    /// </summary>
    /// <param name="archive">The archive to add it to</param>
    /// <param name="folderPath">The folder path to add</param>
    private void AddFolderToZip(ZipArchive archive, string folderPath)
    {
        var entry = archive.CreateEntry(folderPath);
        var folderStream = entry.Open();
        folderStream.Close();
    }

    /// <summary>
    /// Verify that GetAllFiles returns the correct files with and without a pattern
    /// </summary>
    [Test]
    public void TestGetAllFiles()
    {
        var fileSystem = new ZipFileSystem(stream);
        var allFiles = fileSystem.GetAllFiles().ToList();
        var allLogs = fileSystem.GetAllFiles(".log").ToList();
        Assert.AreEqual(4, allFiles.Count());
        Assert.AreEqual("test.txt", allFiles[0]);
        Assert.AreEqual(1, allLogs.Count);
        fileSystem.Close();
    }

    /// <summary>
    /// Verify that FileExists works correctly
    /// </summary>
    [Test]
    public void TestFileExists()
    {
        var fileSystem = new ZipFileSystem(stream);
        Assert.IsTrue(fileSystem.FileExists(testFileName));
        Assert.IsFalse(fileSystem.FileExists("RandomName.txt"));
    }

    /// <summary>
    /// Verify that GetFolders returns the correct number of folders
    /// </summary>
    [Test]
    public void TestGetFolders()
    {
        var fileSystem = new ZipFileSystem(stream);
        var folders = fileSystem.GetFolders().ToList();
        Assert.AreEqual(1, folders.Count);
        folders = fileSystem.GetFolders(nestedFolder + "/").ToList();
        Assert.AreEqual(1, folders.Count);
        folders = fileSystem.GetFolders(nestedFolder).ToList();
        Assert.AreEqual(1, folders.Count);
    }

    /// <summary>
    /// Test that joining paths works correctly
    /// </summary>
    [Test]
    public void TestJoin()
    {
        var fileSystem = new ZipFileSystem(stream);
        Assert.AreEqual("first/second.txt",fileSystem.Join("first", "second.txt"));
    }

    /// <summary>
    /// Verify that GetFiles returns the correct files with and without a pattern
    /// </summary>
    [Test]
    public void TestGetFiles()
    {
        var fileSystem = new ZipFileSystem(stream);
        Assert.AreEqual(2, fileSystem.GetFiles(nestedFolder).Count());
        Assert.AreEqual(1, fileSystem.GetFiles(nestedFolder, ".txt").Count());
    }
    
    [Test]
    public void TestDoublePeriodInPath()
    {
        var fileSystem = new ZipFileSystem(stream);
        Assert.AreEqual("test.txt", fileSystem.JoinPath("folder", "..", "test.txt"));
        Assert.AreEqual("folder/test.txt", fileSystem.JoinPath("folder", ".", "test.txt"));
    }

    [Test]
    public void TestReadAllText()
    {
        var fileSystem = new ZipFileSystem(stream);
        Assert.AreEqual(testFileContents, fileSystem.ReadAllText(testFileName));
        fileSystem.Close();
    }

    /// <summary>
    /// Verify that GetStream hands back a rewound copy of the underlying zip that is still a valid archive
    /// </summary>
    [Test]
    public void TestGetStream()
    {
        var fileSystem = new ZipFileSystem(stream);
        using var copy = fileSystem.GetStream();
        Assert.AreNotSame(stream, copy);
        Assert.AreEqual(0, copy.Position);
        Assert.AreEqual(stream.Length, copy.Length);

        // the copy needs to be readable as a zip in its own right
        using var copiedFileSystem = new ZipFileSystem(copy);
        CollectionAssert.AreEqual(fileSystem.GetAllFiles().ToList(), copiedFileSystem.GetAllFiles().ToList());
        Assert.AreEqual(testFileContents, copiedFileSystem.ReadAllText(testFileName));
        fileSystem.Close();
    }

    /// <summary>
    /// Verify that GetStream can be called after files have been read and more than once since it rewinds
    /// the underlying stream to make the copy
    /// </summary>
    [Test]
    public void TestGetStreamAfterReadingFiles()
    {
        var fileSystem = new ZipFileSystem(stream);
        Assert.AreEqual(testFileContents, fileSystem.ReadAllText(testFileName));

        using var first = fileSystem.GetStream();
        using var second = fileSystem.GetStream();
        Assert.AreEqual(first.Length, second.Length);

        // the file system itself has to still be usable afterwards
        Assert.AreEqual(testFileContents, fileSystem.ReadAllText(testFileName));
        fileSystem.Close();
    }

    /// <summary>
    /// Verify that disposing a stream from GetStream doesn't affect the file system it came from
    /// </summary>
    [Test]
    public void TestGetStreamCopyIsIndependent()
    {
        var fileSystem = new ZipFileSystem(stream);
        var copy = fileSystem.GetStream();
        copy.Dispose();
        Assert.AreEqual(testFileContents, fileSystem.ReadAllText(testFileName));
        fileSystem.Close();
    }
}