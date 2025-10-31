using System;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using Microsoft.Extensions.Logging;
using PipelineCommon.Models.Webhook;
using Azure.Messaging.ServiceBus;
using PipelineCommon.Models.BusMessages;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace ScriptureRenderingPipeline
{
	public class Webhook
	{
		private readonly ServiceBusClient _serviceBusClient;

		public Webhook()
		{
			var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
			_serviceBusClient = new ServiceBusClient(connectionString);
		}

		[Function("Webhook")]
		public async Task<HttpResponseData> RunAsync(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhook")] HttpRequestData req,
			FunctionContext context)
		{
			var log = context.GetLogger("Webhook");
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
				var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
				await badResponse.WriteStringAsync("Invalid webhook request");
				return badResponse;
			}

#if DEBUG
			var eventType = "push";
#else
			var eventType = "unknown";
#endif


			if (req.Headers.TryGetValues("X-GitHub-Event", out var eventValues))
			{
				eventType = eventValues.FirstOrDefault() ?? eventType;
			}

			if (!string.IsNullOrEmpty(allowedDomain))
			{
				try
				{
					var url = new Uri(webhookEvent.repository.HtmlUrl);
					if (url.Host != allowedDomain)
					{
						log.LogError("Webhooks for {Domain} are not allowed", url.Host);
						var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
						await badResponse.WriteStringAsync("Webhooks for this domain are not allowed");
						return badResponse;
					}
				}
				catch (Exception ex)
				{
					log.LogError(ex, "Error validating domain");
					var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
					await badResponse.WriteStringAsync("Invalid url");
					return badResponse;
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

			await using var sender = _serviceBusClient.CreateSender("WACSEvent");
			await sender.SendMessageAsync(CreateMessage(message));

			var response = req.CreateResponse(HttpStatusCode.OK);
			return response;
		}
		
		/// <summary>
		/// Trigger a merge
		/// </summary>
		/// <param name="req">Incoming http request</param>
		/// <param name="context">Function context</param>
		/// <returns>Http response for the result of the webhook</returns>
		[Function("MergeWebhook")]
		public async Task<HttpResponseData> MergeWebhookAsync(
			[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "merge")] HttpRequestData req,
			FunctionContext context)
		{
			var log = context.GetLogger("MergeWebhook");
			var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
			var request = JsonSerializer.Deserialize<MergeRequest>(requestBody, PipelineJsonContext.Default.MergeRequest);
			if (request == null)
			{
				log.LogError("Invalid merge request");
				var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
				await badResponse.WriteStringAsync("Invalid merge request");
				return badResponse;
			}

			await using var sender = _serviceBusClient.CreateSender("MergeRequest");
			await sender.SendMessageAsync(new ServiceBusMessage(requestBody));
			
			var response = req.CreateResponse(HttpStatusCode.OK);
			return response;
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
