using System;
using System.IO;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using NPOI.HPSF;
using NUnit.Framework;
using PipelineCommon.Helpers;
using USFMToolsSharp.Models.Markers;

namespace SRPTests;

public class UtilsTests
{
   /// <summary>
   /// Verify that GetBookNumber returns the correct book number for a given book code.
   /// </summary>
   [Test]
   public void TestGetBookNumber()
   {
      Assert.AreEqual(0, Utils.GetBookNumber("unknown"));
      Assert.AreEqual(1, Utils.GetBookNumber("gen"));
      Assert.AreEqual(41, Utils.GetBookNumber("mat"));
   }

   /// <summary>
   /// Verify that CreateTempFolder creates a folder under the temporary path.
   /// </summary>
   [Test]
   public void TestCreateTempFolder()
   {
      var tmp = Utils.CreateTempFolder();
      Assert.IsTrue(tmp.StartsWith(Path.GetTempPath()));
      Directory.Delete(tmp);
   }

   [Test]
   public void TestGetRepoType()
   {
      Environment.SetEnvironmentVariable("BibleIdentifiers", "bib");
      Assert.AreEqual( RepoType.Bible, Utils.GetRepoType("ulb"));
      Assert.AreEqual( RepoType.translationNotes, Utils.GetRepoType("tn"));
      Assert.AreEqual( RepoType.translationWords, Utils.GetRepoType("tw"));
      Assert.AreEqual( RepoType.translationAcademy, Utils.GetRepoType("tm"));
      Assert.AreEqual( RepoType.BibleCommentary, Utils.GetRepoType("bc"));
      Assert.AreEqual( RepoType.translationQuestions, Utils.GetRepoType("tq"));
      Assert.AreEqual( RepoType.Unknown, Utils.GetRepoType("picturesofsheep"));
      Assert.AreEqual( RepoType.Bible, Utils.GetRepoType("bib"));
      Assert.AreEqual( RepoType.Unknown, Utils.GetRepoType(null));
      Assert.AreEqual( RepoType.Unknown, Utils.GetRepoType(string.Empty));
   }

   [Test]
   public void TestGetBookAbbreviationFromFileName()
   {
      Assert.AreEqual("GEN", Utils.GetBookAbbreviationFromFileName("01-GEN.usfm"));
      Assert.AreEqual("GEN", Utils.GetBookAbbreviationFromFileName("GEN.usfm"));
   }

   [Test]
   public void TestCountUniqueVerses()
   {
      var chapter = new CMarker();
      chapter.Contents.Add(new VMarker(){StartingVerse = 1, EndingVerse = 1});
      chapter.Contents.Add(new VMarker(){StartingVerse = 1, EndingVerse = 1});
      chapter.Contents.Add(new VMarker(){StartingVerse = 2, EndingVerse = 3});
      chapter.Contents.Add(new VMarker(){StartingVerse = 3, EndingVerse = 4});
      Assert.AreEqual(4, Utils.CountUniqueVerses(chapter));
      
      chapter = new CMarker();
      chapter.Contents.Add(new VMarker(){StartingVerse = 1, EndingVerse = 2});
      chapter.Contents.Add(new VMarker(){StartingVerse = 2, EndingVerse = 3});
      chapter.Contents.Add(new VMarker(){StartingVerse = 5, EndingVerse = 5});
      Assert.AreEqual(4, Utils.CountUniqueVerses(chapter));
      
      chapter = new CMarker();
      chapter.Contents.Add(new VMarker(){StartingVerse = 1, EndingVerse = 4});
      chapter.Contents.Add(new VMarker(){StartingVerse = 2, EndingVerse = 3});
      chapter.Contents.Add(new VMarker(){StartingVerse = 5, EndingVerse = 5});
      Assert.AreEqual(5, Utils.CountUniqueVerses(chapter));
      
      chapter = new CMarker();
      chapter.Contents.Add(new VMarker(){StartingVerse = 1, EndingVerse = 2});
      chapter.Contents.Add(new VMarker(){StartingVerse = 4, EndingVerse = 5});
      Assert.AreEqual(4, Utils.CountUniqueVerses(chapter));
   }
    [Test]
    public void TestGenerateDownloadLink()
    {
        // Arrange
        var htmlUrl = "https://github.com/user/repo";
        var user = "user";
        var repo = "repo";
        var branch = "master";

        // Act
        var result = Utils.GenerateDownloadLink(htmlUrl, user, repo, branch);

        // Assert
        Assert.AreEqual("https://github.com/api/v1/repos/user/repo/archive/master.zip", result);
    }

    [Test]
    public void TestGenerateDownloadLinkWhenHtmlUrlHasSubdirectory()
    {
        // Arrange
        var htmlUrl = "https://github.com/user/repo/subdirectory";
        var user = "user";
        var repo = "repo";
        var branch = "master";

        // Act
        var result = Utils.GenerateDownloadLink(htmlUrl, user, repo, branch);

        // Assert
        Assert.AreEqual("https://github.com/api/v1/repos/user/repo/archive/master.zip", result);
    }

    [Test]
    public void TestGenerateDownloadLinkThrowsExceptionForInvalidUrl()
    {
        // Arrange
        var htmlUrl = "invalid_url";
        var user = "user";
        var repo = "repo";
        var branch = "master";

        // Act & Assert
        Assert.Throws<UriFormatException>(() => Utils.GenerateDownloadLink(htmlUrl, user, repo, branch));
    }
    
   [Test]
   public void TestGetBlankVerses()
   {
      var chapter = new CMarker();
      chapter.Contents.Add(new VMarker(){StartingVerse = 1, EndingVerse = 1});
      chapter.Contents.Add(new VMarker(){StartingVerse = 1, EndingVerse = 1, Contents = new () { new TextBlock("He said")}});
      Assert.AreEqual(1, Utils.CountBlankVerses(chapter));
   }
   
}