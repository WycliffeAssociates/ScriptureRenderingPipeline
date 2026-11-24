using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using PipelineCommon.Helpers;

namespace SRPTests;

/// <summary>
/// Tests for how ZipFileSystem looks up files and folders. These pin down ordering and prefix matching
/// because callers depend on both, and the lookups use an index that sorts paths internally.
/// </summary>
public class ZipFileSystemLookupTests
{
    /// <summary>
    /// Build a zip with entries in exactly the order given. Paths ending in a slash become folder entries
    /// the way Gitea writes them, everything else becomes a file with some content.
    /// </summary>
    /// <param name="entries">The entries to add in order</param>
    /// <returns>A ZipFileSystem over the new zip</returns>
    private static ZipFileSystem BuildZip(params string[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            foreach (var entry in entries)
            {
                var zipEntry = archive.CreateEntry(entry);
                if (entry.EndsWith("/"))
                {
                    continue;
                }
                using var writer = new StreamWriter(zipEntry.Open());
                writer.Write($"contents of {entry}");
            }
        }
        stream.Position = 0;
        return new ZipFileSystem(stream);
    }

    /// <summary>
    /// Verify that GetFiles returns files in zip order rather than sorted, since callers like the catalog's
    /// ConversionUtils add verses in the order they come back
    /// </summary>
    [Test]
    public void TestGetFilesKeepsZipOrder()
    {
        using var fileSystem = BuildZip("repo/", "repo/gen/", "repo/gen/01/",
            "repo/gen/01/10.md", "repo/gen/01/02.md", "repo/gen/01/01.md");
        CollectionAssert.AreEqual(new[] { "repo/gen/01/10.md", "repo/gen/01/02.md", "repo/gen/01/01.md" },
            fileSystem.GetFiles("repo/gen/01", ".md").ToList());
    }

    /// <summary>
    /// Verify that GetFolders returns child folders in zip order with each one only once
    /// </summary>
    [Test]
    public void TestGetFoldersKeepsZipOrderAndRemovesDuplicates()
    {
        using var fileSystem = BuildZip("repo/", "repo/gen/", "repo/gen/02/", "repo/gen/01/", "repo/gen/02/sub/",
            "repo/gen/02/01.md");
        CollectionAssert.AreEqual(new[] { "02", "01" }, fileSystem.GetFolders("repo/gen").ToList());
        CollectionAssert.AreEqual(new[] { "02", "01" }, fileSystem.GetFolders("repo/gen/").ToList());
    }

    /// <summary>
    /// Verify that the first top level folder is the first one in the zip since every trigger uses
    /// GetFolders().FirstOrDefault() as the base path of the repo
    /// </summary>
    [Test]
    public void TestGetFoldersTopLevelKeepsZipOrder()
    {
        using var fileSystem = BuildZip("zeta/", "zeta/manifest.yaml", "alpha/", "alpha/other.md");
        CollectionAssert.AreEqual(new[] { "zeta", "alpha" }, fileSystem.GetFolders().ToList());
        Assert.AreEqual("zeta", fileSystem.GetFolders().FirstOrDefault());
    }

    /// <summary>
    /// Verify that GetFiles includes files in folders below the one asked for
    /// </summary>
    [Test]
    public void TestGetFilesIncludesSubfolders()
    {
        using var fileSystem = BuildZip("repo/", "repo/articles/", "repo/articles/a.md", "repo/articles/deeper/",
            "repo/articles/deeper/b.md");
        CollectionAssert.AreEqual(new[] { "repo/articles/a.md", "repo/articles/deeper/b.md" },
            fileSystem.GetFiles("repo/articles", ".md").ToList());
    }

    private static IEnumerable<TestCaseData> PrefixCases()
    {
        yield return new TestCaseData("aaa", new string[0]).SetName("Prefix sorts before every entry");
        yield return new TestCaseData("zzz", new string[0]).SetName("Prefix sorts after every entry");
        yield return new TestCaseData("repo/exo", new string[0]).SetName("Prefix falls between entries");
        yield return new TestCaseData("", new[] { "repo/gen/01.md", "repo/lev/01.md", "repo/manifest.yaml" })
            .SetName("Empty path returns every file");
        yield return new TestCaseData("repo/gen/", new[] { "repo/gen/01.md" })
            .SetName("Trailing separator is the same directory");
        yield return new TestCaseData("repo/ge", new string[0])
            .SetName("Part of a directory name matches nothing");
        yield return new TestCaseData("repo/lev/01.md", new string[0])
            .SetName("A file path is not a directory so matches nothing");
    }

    /// <summary>
    /// Verify GetFiles at the edges of the directory lookup
    /// </summary>
    [TestCaseSource(nameof(PrefixCases))]
    public void TestGetFilesPrefixEdges(string prefix, string[] expected)
    {
        using var fileSystem = BuildZip("repo/", "repo/gen/", "repo/gen/01.md", "repo/lev/", "repo/lev/01.md",
            "repo/manifest.yaml");
        CollectionAssert.AreEqual(expected, fileSystem.GetFiles(prefix).ToList());
    }

    /// <summary>
    /// Verify that asking for chapter 1 doesn't also pick up chapter 10 just because the names share a prefix.
    /// Standard tN and tQ repos zero pad their chapters, but repos with unpadded chapters used to get later
    /// chapters' verses mixed in.
    /// </summary>
    [Test]
    public void TestGetFilesIgnoresSiblingsSharingAPrefix()
    {
        using var fileSystem = BuildZip("repo/", "repo/gen/", "repo/gen/1/", "repo/gen/1/01.md", "repo/gen/10/",
            "repo/gen/10/01.md");
        CollectionAssert.AreEqual(new[] { "repo/gen/1/01.md" }, fileSystem.GetFiles("repo/gen/1", ".md").ToList());
    }

    /// <summary>
    /// Verify that a sibling folder sharing the prefix isn't returned, which used to come back as an empty folder name
    /// </summary>
    [Test]
    public void TestGetFoldersIgnoresSiblingsSharingAPrefix()
    {
        using var fileSystem = BuildZip("repo/", "repo/gen/", "repo/gen/1/", "repo/gen/1/sub/", "repo/gen/10/");
        CollectionAssert.AreEqual(new[] { "sub" }, fileSystem.GetFolders("repo/gen/1").ToList());
    }

    /// <summary>
    /// Verify that asking for the folders under an empty path gives the top level folders
    /// </summary>
    [Test]
    public void TestGetFoldersWithEmptyPathReturnsTopLevel()
    {
        using var fileSystem = BuildZip("zeta/", "zeta/gen/", "alpha/");
        CollectionAssert.AreEqual(fileSystem.GetFolders().ToList(), fileSystem.GetFolders("").ToList());
    }

    /// <summary>
    /// Verify the edges of FileExists, which also matches folder entries
    /// </summary>
    [Test]
    public void TestFileExistsEdges()
    {
        using var fileSystem = BuildZip("repo/", "repo/folder/", "repo/folder/nested.txt");
        Assert.IsTrue(fileSystem.FileExists("repo/folder/nested.txt"));
        Assert.IsTrue(fileSystem.FileExists("repo/folder/"));
        Assert.IsFalse(fileSystem.FileExists("repo/folder"));
        Assert.IsFalse(fileSystem.FileExists("repo/folder/nest"));
        Assert.IsFalse(fileSystem.FileExists("REPO/folder/nested.txt"));
    }

    /// <summary>
    /// Verify that paths are matched exactly, so a precomposed character doesn't match the same character
    /// written as a base letter plus a combining accent
    /// </summary>
    [Test]
    public void TestGetFilesMatchesOrdinally()
    {
        using var fileSystem = BuildZip("repo/", "repo/café/", "repo/café/01.md");
        Assert.AreEqual(1, fileSystem.GetFiles("repo/café").Count());
        Assert.AreEqual(0, fileSystem.GetFiles("repo/café").Count());
    }

    /// <summary>
    /// Verify that changing what GetAllFiles hands back can't change the file system itself
    /// </summary>
    [Test]
    public void TestGetAllFilesCannotBeModified()
    {
        using var fileSystem = BuildZip("repo/", "repo/a.md", "repo/b.md");
        if (fileSystem.GetAllFiles() is IList<string> list)
        {
            Assert.Throws<System.NotSupportedException>(() => list[0] = "changed");
        }
        CollectionAssert.AreEqual(new[] { "repo/a.md", "repo/b.md" }, fileSystem.GetAllFiles().ToList());
    }
}
