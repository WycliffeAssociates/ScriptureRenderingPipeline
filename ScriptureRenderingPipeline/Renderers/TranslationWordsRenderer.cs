using DotLiquid;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PipelineCommon.Helpers;
using PipelineCommon.Helpers.MarkdigExtensions;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ScriptureRenderingPipeline.Renderers
{
	public class TranslationWordsRenderer: IRenderer
	{
		public async Task RenderAsync(RendererInput input, IOutputInterface output)
		{
			var projectPath = input.ResourceContainer.projects[0].path;
			var categories = await LoadWordsAsync(input.FileSystem, input.FileSystem.Join(input.BasePath, projectPath), input.BaseUrl, input.UserToRouteResourcesTo, input.LanguageCode);
			var printBuilder = new StringBuilder();
			var outputTasks = new List<Task>();
			var outputIndex = new OutputIndex()
			{
				LanguageCode = input.LanguageCode,
				LanguageName = input.LanguageName,
				TextDirection = input.LanguageTextDirection,
				RepoUrl = input.RepoUrl,
				ResourceType = "tw",
				ResourceTitle = input.Title,
				Bible = null,
				Words = new List<OutputWordCategory>(),
				AppMeta = input.AppsMeta
			};
			foreach (var category in categories)
			{
				var outputCategory = new OutputWordCategory()
				{
					Slug = category.Slug,
					Label = category.Title
				};
				var titleMapping = new Dictionary<string, string>(category.Words.Count);
				var builder = new StringBuilder();
				builder.AppendLine($"<h1>{category.Title}</h1>");
				foreach (var word in category.Words)
				{
					builder.AppendLine($"<div id=\"{word.Slug}\"></div>");
					builder.AppendLine(word.Content);
					builder.AppendLine("<hr/>");
					titleMapping.Add(word.Slug, word.Title.Trim());
					outputCategory.Words.Add(new OutputWord()
					{
						Slug = word.Slug,
						Label = word.Title
					});
				}

				outputIndex.Words.Add(outputCategory);

				printBuilder.Append(builder);
				outputTasks.Add(output.WriteAllTextAsync(BuildFileName(category.Slug), builder.ToString()));
				outputTasks.Add(output.WriteAllTextAsync(
					$"{Path.GetFileNameWithoutExtension(BuildFileName(category.Slug))}.json", JsonSerializer.Serialize(titleMapping)));
			}
			outputTasks.Add(output.WriteAllTextAsync("index.json", JsonSerializer.Serialize(outputIndex)));

			if (categories.Count > 0)
			{
				outputTasks.Add(output.WriteAllTextAsync("print_all.html", input.PrintTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), heading = input.Title }))));
			}

			await Task.WhenAll(outputTasks);
		}

		private string RewriteContentLinks(string link, TranslationWordsCategory category)
		{
			var splitLink = link.Split("/");
			if (splitLink.Length == 1)
			{
				return BuildFileName(category.Slug) + "#" + splitLink[0][..^3];
			}

			if (splitLink[0] == ".")
			{
				return BuildFileName(category.Slug) + "#" + splitLink[1][..^3];
			}
			else if (splitLink[0] == "..")
			{
				if (splitLink.Length == 3)
				{
					return BuildFileName(splitLink[1]) + "#" + splitLink[2][..^3];
				}
			}
			return link;
		}

		private string BuildFileName(string slug)
		{
			return $"{slug}.html";
		}
		private async Task<List<TranslationWordsCategory>> LoadWordsAsync(IZipFileSystem sourceDir, string basePath, string baseUrl, string userToRouteResourcesTo, string languageCode)
		{
			var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Use(new RCLinkExtension(new RCLinkOptions()
			{
				BaseUser = userToRouteResourcesTo,
				ServerUrl = baseUrl,
				LanguageCode = languageCode,
			})).Build();
			var output = new List<TranslationWordsCategory>();
			foreach (var dir in sourceDir.GetFolders(basePath))
			{
				if (Utils.TranslationWordsValidSections.Contains(dir))
				{
					var category = new TranslationWordsCategory()
					{
						Slug = dir,
						Title = Utils.TranslationWordsTitleMapping[dir],
					};

					foreach (var file in sourceDir.GetFiles(sourceDir.Join(basePath, dir), ".md"))
					{
						var slug = Path.GetFileNameWithoutExtension(file);
						var content = Markdown.Parse(await sourceDir.ReadAllTextAsync(file), pipeline);
						var headings = content.Descendants<HeadingBlock>().ToList();
						var titleHeading = headings.FirstOrDefault(h => h.Level == 1);

						foreach (var heading in headings)
						{
							heading.Level++;
						}

						foreach (var link in content.Descendants<LinkInline>())
						{
							if (link.Url != null && link.Url.EndsWith(".md"))
							{
								link.Url = RewriteContentLinks(link.Url, category);
							}
						}

						category.Words.Add(new TranslationWordsEntry()
						{
							Title = titleHeading?.Inline?.FirstChild?.ToString() ?? slug,
							Content = content.ToHtml(pipeline),
							Slug = slug,
						});
					}
					category.Words = category.Words.OrderBy(i => i.Title).ToList();
					output.Add(category);
				}
			}
			return output;
		}
	}
}
