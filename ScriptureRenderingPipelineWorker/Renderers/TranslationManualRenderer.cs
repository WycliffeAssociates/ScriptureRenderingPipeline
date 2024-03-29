﻿using System.Text;
using System.Text.Json;
using DotLiquid;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PipelineCommon.Helpers;
using PipelineCommon.Helpers.MarkdigExtensions;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipelineWorker.Models;
using YamlDotNet.Serialization;

namespace ScriptureRenderingPipelineWorker.Renderers
{
	public class TranslationManualRenderer: IRenderer
	{
		public async Task RenderAsync(RendererInput input, IOutputInterface output)
		{
			// TODO: This needs to be converted from a hard-coded english string to something localized
			var subtitleText = "This section answers the following question:";
			var sections = await GetSectionsAsync(input.FileSystem, input.BasePath, input.ResourceContainer, input.BaseUrl, input.UserToRouteResourcesTo, input.LanguageCode);
			var navigation = ConvertNavigation(sections);
			var printBuilder = new StringBuilder();
			var outputWrapper = new OutputAndLoggingWrapper(output, input.Logger);
			var outputTasks = new List<Task>();
			var outputIndex = new OutputIndex()
			{
				LanguageCode = input.LanguageCode,
				LanguageName = input.LanguageName,
				RepoUrl = input.RepoUrl,
				ResourceType = "tm",
				ResourceTitle = input.Title,
				TextDirection = input.LanguageTextDirection,
				Bible = null,
				Words = null,
				Navigation = navigation,
				AppMeta = input.AppsMeta
			};
			foreach (var category in sections)
			{
				var titleMapping = new Dictionary<string, string>(category.Content.Count);
				var builder = new StringBuilder();
				builder.AppendLine($"<h1>{category.title}</h1>");
				foreach (var content in category.Content)
				{
					builder.AppendLine($"<div id=\"{content.slug}\"></div>");
					builder.AppendLine($"<h2>{content.title}</h2>");

					if (!string.IsNullOrEmpty(subtitleText))
					{
						builder.AppendLine($"<div>{subtitleText} {content.subtitle}</div>");
						builder.AppendLine($"<br/>");
					}

					builder.AppendLine(content.content);

					builder.AppendLine("<hr/>");

					titleMapping.Add(content.slug, content.title?.TrimEnd());
					outputWrapper.LogTitle(content.slug, content.title?.TrimEnd());
				}
				outputTasks.Add(outputWrapper.WriteAllTextAsync(BuildFileName(category), builder.ToString()));

				printBuilder.Append(builder);

				// output mapping to file
				outputTasks.Add(outputWrapper.WriteAllTextAsync($"{Path.GetFileNameWithoutExtension(category.filename)}.json", JsonSerializer.Serialize(titleMapping, WorkerJsonContext.Default.DictionaryStringString)));
			}
			outputTasks.Add(outputWrapper.WriteAllTextAsync("index.json", JsonSerializer.Serialize(outputIndex, WorkerJsonContext.Default.OutputIndex)));

			if (sections.Count > 0)
			{
				outputTasks.Add(outputWrapper.WriteAllTextAsync("print_all.html", input.PrintTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), input.Title }))));
			}
			
			outputTasks.Add(outputWrapper.FinishAsync());

			await Task.WhenAll(outputTasks);
		}
		private string BuildFileName(TranslationManualSection section)
		{
			return section.filename;
		}

		private List<OutputNavigation> ConvertNavigation(List<TranslationManualSection> sections)
		{
			var output = new List<OutputNavigation>(sections.Count);
			foreach (var section in sections)
			{
				if (section.TableOfContents == null)
				{
					continue;
				}
				var navigationSection = new OutputNavigation()
				{
					File = BuildFileName(section),
					Label = section.title,
				};

				var stack = new Stack<(TableOfContents tableOfContents, string fileName, bool lastChild, bool isTopLevel)>();
				var parents = new Stack<OutputNavigation>();
				parents.Push(navigationSection);

				stack.Push((section.TableOfContents, BuildFileName(section), false, true));
				while (stack.Count > 0)
				{
					var (tableOfContents, fileName, lastChild, isTopLevel) = stack.Pop();
					var currentItem = new OutputNavigation()
					{
						File = fileName,
						Label = tableOfContents.title,
						Slug = tableOfContents.link ?? ""
					};
					if (!isTopLevel)
					{
						parents.Peek().Children.Add(currentItem);
					}
					else
					{
						output.Add(currentItem);
					}

					if (lastChild)
					{
						parents.Pop();
					}

					if (tableOfContents.sections.Count == 0)
					{
						continue;
					}

					// Put it on the stack backwards so things end up in the right order
					for (var i = tableOfContents.sections.Count - 1; i >= 0; i--)
					{
						var itemIsLastChild = !isTopLevel && i == tableOfContents.sections.Count - 1;
						stack.Push((tableOfContents.sections[i], fileName, itemIsLastChild, false));
					}
					parents.Push(currentItem);
				}
			}
			return output;
		}

		private async Task<List<TranslationManualSection>> GetSectionsAsync(IZipFileSystem fileSystem, string basePath, ResourceContainer resourceContainer, string baseUrl, string userToRouteResourcesTo, string languageCode)
		{
			var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UsePipeTables()
					.Use(new RCLinkExtension(new RCLinkOptions() { BaseUser = userToRouteResourcesTo, ServerUrl = baseUrl, LanguageCode = languageCode }))
					.Build();
			var output = new List<TranslationManualSection>();
			var projects = resourceContainer.projects.OrderBy(p => p.sort);
			foreach (var project in projects)
			{
				var section = new TranslationManualSection(project.title, project.path, Path.GetFileNameWithoutExtension(project.path) + ".html");
				// Load table of contents
				var tableOfContents = await LoadTableOfContentsAsync(fileSystem, fileSystem.Join(basePath, project.path));
				section.TableOfContents = tableOfContents;
				if (tableOfContents != null)
				{
					var stack = new Stack<TableOfContents>(new[] { tableOfContents });
					while (stack.Count > 0)
					{
						var item = stack.Pop();
						if (item.link != null)
						{
							var path = fileSystem.JoinPath(basePath, project.path, item.link);
							var content = await GetContentAsync(fileSystem, path);
							if (content == null)
							{
								section.Content.Add(new TranslationManualContent()
								{
									title = await GetTitleAsync(fileSystem, path),
									slug = item.link,
									subtitle = await GetSubTitleAsync(fileSystem, path),
									content = string.Empty,
								});
								continue;
							}
							var markdown = Markdown.Parse(content, pipeline);
							foreach (var link in markdown.Descendants<LinkInline>())
							{
								if (string.IsNullOrEmpty(link.Url))
								{
									continue;
								}
								if (link.Url.EndsWith("01.md"))
								{
									link.Url = RewriteContentLink(link.Url, section);
								}
							}
							section.Content.Add(new TranslationManualContent()
							{
								title = await GetTitleAsync(fileSystem, path),
								slug = item.link,
								subtitle = await GetSubTitleAsync(fileSystem, path),
								content = markdown.ToHtml(pipeline),
							});
						}

						if (item.sections.Count != 0)
						{
							// Put it on the stack backwards so things end up in the right order
							for (var i = item.sections.Count - 1; i >= 0; i--)
							{
								stack.Push(item.sections[i]);
							}
						}
					}
				}
				output.Add(section);
			}
			return output;
		}

		private string RewriteContentLink(string link, TranslationManualSection currentSection)
		{
			var splitLink = link.Split("/");
			if (splitLink[0] == "..")
			{
				if (splitLink.Length == 3)
				{
					return currentSection.filename + "#" + splitLink[1];
				}
				else if (splitLink.Length == 5 || splitLink[1] == "..")
				{
					return splitLink[2] + ".html#" + splitLink[3];
				}
			}
			return link;
		}

		private async Task<string> GetSubTitleAsync(IZipFileSystem fileSystem, string slugPath)
		{
			var path = fileSystem.Join(slugPath, "sub-title.md");
			if (fileSystem.FileExists(path))
			{
				return await fileSystem.ReadAllTextAsync(path);
			}
			return null;
		}
		private async Task<string> GetTitleAsync(IZipFileSystem fileSystem, string slugPath)
		{
			var path = fileSystem.Join(slugPath, "title.md");
			if (fileSystem.FileExists(path))
			{
				return await fileSystem.ReadAllTextAsync(path);
			}
			return null;
		}
		private async Task<string> GetContentAsync(IZipFileSystem fileSystem, string slugPath)
		{
			var path = fileSystem.Join(slugPath, "01.md");
			if (fileSystem.FileExists(path))
			{
				return await fileSystem.ReadAllTextAsync(path);
			}
			return null;
		}
		private async Task<TableOfContents> LoadTableOfContentsAsync(IZipFileSystem fileSystem, string projectPath)
		{
			var path = fileSystem.Join(projectPath, "toc.yaml");
			if (!fileSystem.FileExists(path))
			{
				return null;
			}
			try
			{
				var serializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
				return serializer.Deserialize<TableOfContents>(await fileSystem.ReadAllTextAsync(path));
			}
			catch (Exception ex)
			{
				// We got invalid YAML, so we'll just ignore it
				return null;
			}
		}
	}
}
