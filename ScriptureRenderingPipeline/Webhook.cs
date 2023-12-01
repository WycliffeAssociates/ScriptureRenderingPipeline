using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PipelineCommon.Models.Webhook;
using Azure.Messaging.ServiceBus;
using PipelineCommon.Models.BusMessages;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ScriptureRenderingPipeline
{
	public static class Webhook
	{
		[FunctionName("Webhook")]
		public static async Task<IActionResult> RunAsync(
				[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhook")] HttpRequest req,
				[ServiceBus("WACSEvent", Connection = "ServiceBusConnectionString")]IAsyncCollector<ServiceBusMessage> outputBus,
				ILogger log)
		{
			var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
			WebhookEvent webhookEvent = null;
			try
			{
				webhookEvent = JsonConvert.DeserializeObject<WebhookEvent>(requestBody);
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
			

			log.LogInformation("Starting webhook for {RepoName}", webhookEvent.repository.FullName);

			var message = new WACSMessage()
			{
				EventType = eventType,
				RepoHtmlUrl = webhookEvent.repository.HtmlUrl,
				Repo = webhookEvent.repository.Name,
				User = webhookEvent.repository.Owner.Username,
				RepoId = webhookEvent.repository.Id,
				Action = webhookEvent.action,
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

			await outputBus.AddAsync(CreateMessage(message));

			return new OkResult();
		}
		
		private static ServiceBusMessage CreateMessage(WACSMessage input)
		{
			var json = JsonSerializer.Serialize(input);
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
