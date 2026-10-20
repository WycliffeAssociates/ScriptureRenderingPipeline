using NUnit.Framework;
using ScriptureRenderingPipelineWorker;

namespace SRPTests;

/// <summary>
/// Tests for the Scripture Burrito ingredient metadata the merge builds. The checksum and size have to describe
/// the files as Gitea stores them (UTF-8), otherwise metadata.json disagrees with the repo.
/// </summary>
public class MergeHandlerTests
{
    [Test]
    public void TestIngredientChecksumAndSizeUseUtf8Bytes()
    {
        // Accented Latin, Greek, an em dash, and CJK so every UTF-8 sequence length shows up
        const string usfm = "\\id GEN\n\\h Génesis\n\\c 1\n\\v 1 Ἐν ἀρχῇ — 太初";

        var (md5Hash, size) = MergeTrigger.GetIngredientChecksumAndSize(usfm);

        Assert.AreEqual("a45e9386b8278ac18ec30032672b4e04", md5Hash);
        Assert.AreEqual(57u, size);
    }

    [Test]
    public void TestIngredientChecksumOfEmptyContent()
    {
        var (md5Hash, size) = MergeTrigger.GetIngredientChecksumAndSize("");

        Assert.AreEqual("d41d8cd98f00b204e9800998ecf8427e", md5Hash);
        Assert.AreEqual(0u, size);
    }
}
