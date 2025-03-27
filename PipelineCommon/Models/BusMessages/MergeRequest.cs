using System;

namespace PipelineCommon.Models.BusMessages;

public class MergeRequest
{
   public string RequestingUserName { get; set; } 
   public MergeRequestRepo[] ReposToMerge { get; set; }
}

public class MergeRequestRepo
{
   public string HtmlUrl { get; set; }
   public string User { get; set; }
   public string Repo { get; set; }
   public Guid RepoPortId { get; set; }
}