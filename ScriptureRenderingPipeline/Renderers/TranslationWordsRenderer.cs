﻿using DotLiquid;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PipelineCommon.Helpers;
using PipelineCommon.Helpers.MarkdigExtensions;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Helpers;
using ScriptureRenderingPipeline.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptureRenderingPipeline.Renderers
{
    public class TranslationWordsRenderer
    {
        public async Task RenderAsync(ZipFileSystem sourceDir, string basePath, string destinationDir, Template template, Template printTemplate, string repoUrl, string heading, ResourceContainer resourceContainer, string baseUrl, string userToRouteResourcesTo, string textDirection, bool isBTTWriterProject = false)
        {
            var projectPath = resourceContainer.projects[0].path;
            var categories = await LoadWordsAsync(sourceDir, sourceDir.Join(basePath, projectPath), baseUrl, userToRouteResourcesTo);
            var printBuilder = new StringBuilder();
            var outputTasks = new List<Task>();
            foreach(var category in categories )
            {
                var builder = new StringBuilder();
                builder.AppendLine($"<h1>{category.Title}</h1>");
                foreach(var word in category.Words)
                {
                    builder.AppendLine($"<div id=\"{word.Slug}\"></div>");
                    builder.AppendLine(word.Content);
                    builder.AppendLine("<hr/>");
                }
                var templateResult = template.Render(Hash.FromDictionary(new Dictionary<string,object>()
                {
                    ["content"] = builder.ToString(),
                    ["contenttype"] = "tw",
                    ["wordsnavigation"] = categories,
                    ["currentslug"] = category.Slug,
                    ["heading"] = heading,
                    ["sourceLink"] = repoUrl,
                    ["textDirection"] = textDirection,
                }
                ));

                printBuilder.Append(builder);
                outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, BuildFileName(category.Slug)),templateResult));
            }

            if (categories.Count > 0)
            {
                File.Copy(Path.Join(destinationDir,BuildFileName(categories[0].Slug)), Path.Combine(destinationDir, "index.html"));
                outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "print_all.html"), printTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), heading }))));
            }

            await Task.WhenAll(outputTasks);
        }
        protected string RewriteContentLinks(string link, TranslationWordsCategory category)
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
        private async Task<List<TranslationWordsCategory>> LoadWordsAsync(ZipFileSystem sourceDir, string basePath, string baseUrl, string userToRouteResourcesTo)
        {
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Use(new RCLinkExtension(new RCLinkOptions()
            {
                BaseUser = userToRouteResourcesTo,
                ServerUrl = baseUrl,
            })).Build();
            var output = new List<TranslationWordsCategory>();
            foreach( var dir in sourceDir.GetFolders(basePath))
            {
                if (Utils.TranslationWordsValidSections.Contains(dir))
                {
                    var category = new TranslationWordsCategory()
                    {
                        Slug = dir,
                        Title = Utils.TranslationWordsTitleMapping[dir],
                    };

                    foreach(var file in sourceDir.GetFiles(sourceDir.Join(basePath, dir),".md"))
                    {
                        var slug = Path.GetFileNameWithoutExtension(file);
                        var content = Markdown.Parse(await sourceDir.ReadAllTextAsync(file), pipeline);
                        var headings = content.Descendants<HeadingBlock>().ToList();
                        var titleHeading = headings.FirstOrDefault(h => h.Level == 1);

                        foreach(var heading in headings)
                        {
                            heading.Level++;
                        }

                        foreach(var link in content.Descendants<LinkInline>())
                        {
                            if (link.Url.EndsWith(".md"))
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
                    output.Add(category);
                }
            }
            return output;
        }
    }
}
