using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PipelineCommon.Models.Webhook;
using Azure.Messaging.ServiceBus;
using PipelineCommon.Models.BusMessages;
using System.Text.Json;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Extensions.Azure;

namespace ScriptureRenderingPipeline
{
	public static class Webhook
	{
		[FunctionName("Webhook")]
		public static async Task<IActionResult> RunAsync(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhook")]HttpRequest req,
			[ServiceBus("WACSEvent", ServiceBusEntityType.Topic, Connection = "ServiceBusConnectionString")] IAsyncCollector<ServiceBusMessage> busMessages,
			ILogger log)
		{
			log.LogInformation("Starting webhook");
			var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
			var allowedDomain = Environment.GetEnvironmentVariable("AllowedDomain");
			WebhookEvent webhookEvent = null;
			try
			{
				webhookEvent = JsonSerializer.Deserialize(requestBody, PipelineJsonContext.Default.WebhookEvent);
			}
			catch (Exception ex)
			{
				log.LogError(ex, "Error deserializing webhook request");
			}

			// validate

			if (webhookEvent == null)
			{
				return new BadRequestObjectResult("Invalid webhook request");
			}

#if DEBUG
			var eventType = "push";
#else
			var eventType = "unknown";
#endif


			if (req.Headers?.ContainsKey("X-GitHub-Event") ?? false)
			{
				eventType = req.Headers["X-GitHub-Event"];
			}

			if (!string.IsNullOrEmpty(allowedDomain))
			{
				try
				{
					var url = new Uri(webhookEvent.repository.HtmlUrl);
					if (url.Host != allowedDomain)
					{
						log.LogError("Webhooks for {Domain} are not allowed", url.Host);
						return new BadRequestObjectResult("Webhooks for this domain are not allowed");
					}
				}
				catch (Exception ex)
				{
					log.LogError(ex, "Error validating domain");
					return new BadRequestObjectResult("Invalid url");
				}
			}

			log.LogInformation("Handling {Event}:{Action} for {RepoName}", eventType, webhookEvent.action, webhookEvent.repository.FullName);

			var message = new WACSMessage()
			{
				EventType = eventType,
				RepoHtmlUrl = webhookEvent.repository.HtmlUrl,
				Repo = webhookEvent.repository.Name,
				User = webhookEvent.repository.Owner.Username,
				RepoId = webhookEvent.repository.Id,
				Action = webhookEvent.action,
				DefaultBranch = webhookEvent.repository.default_branch
			};
			if (webhookEvent.commits != null && webhookEvent.commits.Length > 0)
			{
				message.LatestCommit = new SimplifiedCommit()
				{
					Hash = webhookEvent.commits[0].Id,
					Message = webhookEvent.commits[0].Message,
					Timestamp = webhookEvent.commits[0].Timestamp,
					Username = webhookEvent.commits[0].Committer.Username,
					Url = webhookEvent.commits[0].Url
				};
			}

			await busMessages.AddAsync(CreateMessage(message));

			return new OkResult();
		}

		private static ServiceBusMessage CreateMessage(WACSMessage input)
		{
			var json = JsonSerializer.Serialize(input, PipelineJsonContext.Default.WACSMessage);
			var message = new ServiceBusMessage(json)
			{
				ContentType = "application/json"
			};
			message.ApplicationProperties.Add("EventType", input.EventType);
			message.ApplicationProperties.Add("Action", input.Action);
			return message;
		}
	}
}
