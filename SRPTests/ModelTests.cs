using NUnit.Framework;
using PipelineCommon.Models.BusMessages;

namespace SRPTests;

public class ModelTests
{
    private const string user = "user";
    private const string repo = "repo";
    private const int repoId = 233;
    
    [Test]
    public void TestVerseCountResultFromWACSMessage()
    {
        var message = new WACSMessage()
        {
            User = user,
            Repo = repo,
            RepoId = repoId,
        };
        var verseCountResult = new VerseCountingResult(message);
        Assert.AreEqual(user, verseCountResult.User);
        Assert.AreEqual(repo, verseCountResult.Repo);
        Assert.AreEqual(repoId, verseCountResult.RepoId);
    }

    [Test]
    public void TestRenderingResultFromWACSMessage()
    {
        var message = new WACSMessage()
        {
            User = user,
            Repo = repo,
            RepoId = repoId,
        };
        var renderingResult = new RenderingResultMessage(message);
        Assert.AreEqual(user, renderingResult.User);
        Assert.AreEqual(repo, renderingResult.Repo);
        Assert.AreEqual(repoId, renderingResult.RepoId);
    }

    [Test]
    public void TestRepoAnalysisResultFromWACSMessage()
    {
        var message = new WACSMessage()
        {
            User = user,
            Repo = repo,
            RepoId = repoId,
        };
        var analysisResult = new RepoAnalysisResult(message);
        Assert.AreEqual(user, analysisResult.User);
        Assert.AreEqual(repo, analysisResult.Repo);
        Assert.AreEqual(repoId, analysisResult.RepoId);
    }
}