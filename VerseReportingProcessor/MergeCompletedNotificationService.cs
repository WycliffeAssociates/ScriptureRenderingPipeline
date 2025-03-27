using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using PipelineCommon.Models.BusMessages;

namespace VerseReportingProcessor;

public class MergeCompletedNotificationService: IHostedService
{
    private readonly ILogger<MergeCompletedNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ServiceBusProcessor _serviceBusProcessor;
    private const string Topic = "RepoMerged";
    private const string Subscription = "RepoMerged";
    private readonly ActivitySource _activitySource = new(nameof(MergeCompletedNotificationService));
    private readonly VerseProcessorMetrics _metrics;
    private readonly OrganizationServiceFactory _serviceFactory;
    private readonly OptionSetValue RepoActive = new(1);
    private readonly OptionSetValue RepoPrimary = new(953860000);
    
    public MergeCompletedNotificationService(ILogger<MergeCompletedNotificationService> logger, IConfiguration configuration, ServiceBusClient serviceBusClient, VerseProcessorMetrics metrics, OrganizationServiceFactory serviceFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _metrics = metrics;
        _serviceBusProcessor = serviceBusClient.CreateProcessor(Topic, Subscription);
        _serviceBusProcessor.ProcessMessageAsync += ProcessMessage;
        _serviceFactory = serviceFactory;
    }

    private async Task ProcessMessage(ProcessMessageEventArgs arg)
    {
        var parentActivityId = arg.Message.ApplicationProperties.TryGetValue("Diagnostic-Id", out var diagnosticId) ? diagnosticId.ToString() : null;
        using var activity = _activitySource.StartActivity("ProcessMessage", ActivityKind.Consumer, parentActivityId);
        var body = arg.Message.Body.ToString();
        var message = JsonSerializer.Deserialize<MergeResult>(body);
        if (message == null)
        {
            _logger.LogError("Invalid message received");
            return;
        }
        _logger.LogInformation("Processing message to notify for {User}", message.UserTriggered);
        var service = await _serviceFactory.GetServiceClientAsync();
        var messageText = "";
        var notificationText = "";
        Guid? newMergedRepoId = null;
        var tasks = new List<Task>();
        if (message.Success)
        {
            newMergedRepoId = await CreateRepoRecordInPORTForNewlyMerged(service, message);
            messageText = $"Your merge for {message.LanguageCode} is now complete you can find the result here <a href=\"{message.MergedUrl}\">{message.MergedUrl}</a>";
            notificationText = $"Your merge for {message.LanguageCode} is now complete.";
            if (newMergedRepoId != null)
            {
                tasks.Add(SwitchPrimaryForRepoInPORT(service, newMergedRepoId.Value, message.MergedRepoPORTIds));
            }
        }
        else
        {
            messageText = $"Your merge failed with the following message: {message.Message}";
            notificationText = $"Your merge failed with the following message {message.Message}";
        }
        tasks.Add(SendEmailNotification(service, message.UserTriggered, messageText,
            newMergedRepoId != null ? new EntityReference("wa_translationrepo", newMergedRepoId.Value) : null));
        tasks.Add(SendNotificationToPORT(service, message.UserTriggered, notificationText, message.Success ? message.MergedUrl : null));
        await Task.WhenAll(tasks);
    }

    private async Task SwitchPrimaryForRepoInPORT(IOrganizationServiceAsync service, Guid newRepoId, List<Guid> oldRepoIds)
    {
        // Mark the primary repo as primary then mark all the others as non-primary
        var tasks = new List<Task>
        {
            service.UpdateAsync(new Entity("wa_translationrepo", newRepoId)
            {
                ["statusreason"] = RepoPrimary
            })
        };
        tasks.AddRange(oldRepoIds.Select(oldRepoId => service.UpdateAsync(new Entity("wa_translationrepo", oldRepoId) { ["statusreason"] = RepoActive })));
        await Task.WhenAll(tasks);
    }
    private async Task SendEmailNotification(IOrganizationServiceAsync service, string targetUser, string message, EntityReference? repo = null)
    {
        var user = await GetUserFromUsername(service, targetUser);
        if (user == null)
        {
            _logger.LogError("User {User} not found", targetUser);
            return;
        }

        var email = new Entity("emailmessage")
        {
            ["subject"] = "Merge Completed",
            ["description"] = message,
            ["to"] = new EntityCollection(new[] { new Entity("activityparty") { ["partyid"] = user.Id } }),
        };
        if (repo != null)
        {
            email["regardingobjectid"] = repo;
        }
        var emailId = await service.CreateAsync(email);
        await service.ExecuteAsync(new SendEmailRequest()
        {
            EmailId = emailId,
            IssueSend = true,
        });
    }

    public async Task<Guid> CreateRepoRecordInPORTForNewlyMerged(IOrganizationServiceAsync service, MergeResult mergeResult)
    {
        var languageReference = await GetLanguageFromCode(service, mergeResult.LanguageCode);
        return await service.CreateAsync(new Entity("wa_translationrepo")
        {
            ["wa_name"] = $"WACS/{mergeResult.ResultUser}/{mergeResult.ResultRepo}",
            ["wa_language"] = languageReference,
            ["wa_source_system"] = "WACS",
            ["wa_url"] = mergeResult.MergedUrl,
            ["wa_user_id"] = mergeResult.ResultUser,
            ["wa_repo_id"] = mergeResult.ResultRepo,
            ["wa_wacsid"] = mergeResult.ResultRepoId,
            
        });
    }
    private async Task<EntityReference?> GetLanguageFromCode(IOrganizationServiceAsync service, string languageCode)
    {
        var query = new QueryExpression("wa_language")
        {
            ColumnSet = new ColumnSet("wa_languageid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("wa_ietftag", ConditionOperator.Equal, languageCode)
                }
            }
        };
        var language = (await service.RetrieveMultipleAsync(query)).Entities.FirstOrDefault();
        if (language == null)
        {
            _logger.LogError("Language {Language} not found", languageCode);
            return null;
        }

        return language.ToEntityReference();
    }

    private async Task SendNotificationToPORT(IOrganizationServiceAsync service, string targetUser, string message, string? url)
    {
        var user = await GetUserFromUsername(service, targetUser);
        if (user == null)
        {
            _logger.LogError("User {User} not found can't notify", targetUser);
            return;
        }

        var actions = new List<Entity>();
        if (url != null)
        {
            actions.Add(CreateOpenUrlAction("See Merged", url, UrlTarget.NewWindow));
        }
        await SendNotification(service, "Merge completed", message, user.ToEntityReference(), actions);
    }

    private async Task<Entity?> GetUserFromUsername(IOrganizationServiceAsync service, string username)
    {
        var userQuery = new QueryExpression("systemuser")
        {
            ColumnSet = new ColumnSet("systemuserid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("domainname", ConditionOperator.Equal, username)
                }
            }
        };
        var user = (await service.RetrieveMultipleAsync(userQuery)).Entities.FirstOrDefault();
        if (user != null)
        {
            return user;
        }
        _logger.LogError("User {User} not found", username);
        return null;

    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _serviceBusProcessor.StartProcessingAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _serviceBusProcessor.StopProcessingAsync(cancellationToken);
    }
    
    static async Task SendNotification(IOrganizationServiceAsync service, string title, string message, EntityReference targetUser, List<Entity>? actions = null )
    {
        var request = new OrganizationRequest("SendAppNotification")
        {
            Parameters = new ParameterCollection
            {
                ["Title"] = title,
                ["Body"] = message,
                ["Recipient"] = targetUser,
            }
        };
        if (actions is { Count: > 0 })
        {
            request["Actions"] = new Entity()
            {
                ["actions"] = new EntityCollection(actions)
            };
        }
        await service.ExecuteAsync(request);
    }
    static Entity CreateOpenUrlAction(string title, string url, UrlTarget target)
    {
        return new Entity()
        {
            ["title"] = title,
            ["data"] = new Entity()
            {
                ["type"] = "url",
                ["url"] = url,
                ["navigationTarget"] = target switch
                {
                    UrlTarget.Dialog => "dialog",
                    UrlTarget.Inline => "inline",
                    UrlTarget.NewWindow => "newWindow",
                    _ => throw new NotImplementedException()
                }
            }
        };
    }
}
internal enum UrlTarget
{
    Dialog,
    Inline,
    NewWindow
}
