using System;
using System.Collections.Generic;

namespace PipelineCommon.Models.BusMessages;

public class MergeResult
{
    public MergeResult()
    {
        MergedRepoPORTIds = [];
    }
    public MergeResult(bool success, string message, string userTriggered)
    {
        Success = success;
        if (success)
        {
            MergedUrl = message;
        }
        else
        {
            Message = message;
        }
        UserTriggered = userTriggered;
        MergedRepoPORTIds = [];
    }

    public MergeResult(bool success, string message, string userTriggered, string languageCode, string resultUser,
        string resultRepo, int resultRepoId, List<Guid> mergedRepoIds)
    {
        Success = success;
        if (success)
        {
            MergedUrl = message;
        }
        else
        {
            Message = message;
        }
        UserTriggered = userTriggered;
        MergedRepoPORTIds = mergedRepoIds;
        LanguageCode = languageCode;
        ResultUser = resultUser;
        ResultRepo = resultRepo;
        ResultRepoId = resultRepoId;
    }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string UserTriggered { get; set; }
    public string? MergedUrl { get; set; }
    public int? ResultRepoId { get; set; }
    public string? ResultRepo { get; set; }
    public string? ResultUser { get; set; }
    public string? LanguageCode { get; set; }
    public List<Guid> MergedRepoPORTIds { get; set; }
}