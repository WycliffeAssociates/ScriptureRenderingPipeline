using CommandLine;

namespace CreateVerseCountsFromRepo;

public class Options
{
    [Option("repo", HelpText = "The URL of the repo to process", Required = true)]
    public string RepoUrl { get; set; }
    [Option("language", HelpText = "The language code for the repo", Required = true)]
    public string LanguageCode { get; set; }
    
    [Option("connectionstring", HelpText = "The connection string for the blob storage", Required = true)]
    public string ConnectionString { get; set; }
}