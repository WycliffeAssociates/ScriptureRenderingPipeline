using DotLiquid;
using ScriptureRenderingPipeline.Helpers;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using System.Text;
using Markdig;
using System.Linq;
using System.Threading.Tasks;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PipelineCommon.Helpers;
using PipelineCommon.Helpers.MarkdigExtensions;
using System.Text.Json;

namespace ScriptureRenderingPipeline.Renderers
{
    public class TranslationManualRenderer
    {
        public async Task RenderAsync(ZipFileSystem sourceDir, string basePath, string destinationDir, Template template, Template printTemplate, string repoUrl, string heading, ResourceContainer resourceContainer, string baseUrl, string userToRouteResourcesTo, string textDirection, string languageCode, bool isBTTWriterProject = false)
        {
            // TODO: This needs to be converted from a hard-coded english string to something localized
            string subtitleText = "This section answers the following question:";
            var sections = await GetSectionsAsync(sourceDir, basePath, resourceContainer, baseUrl, userToRouteResourcesTo, languageCode);
            var navigation = BuildNavigation(sections);
            var printBuilder = new StringBuilder();
            var outputTasks = new List<Task>();
            var indexWritten = false;
            var outputIndex = new OutputIndex()
            {
                LanguageCode = languageCode,
                LanguageName = "",
                RepoUrl = repoUrl,
                ResourceTitle = heading,
                TextDirection = textDirection,
                Bible = null,
                Words = null,
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

                    titleMapping.Add(content.slug, content.title.TrimEnd());
                }
                outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, BuildFileName(category)), builder.ToString()));

                printBuilder.Append(builder);

                // output mapping to file
                outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, Path.GetFileNameWithoutExtension(category.filename) + ".json"), JsonSerializer.Serialize(titleMapping)));
            }
            outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "index.json"), JsonSerializer.Serialize(outputIndex)));

            if (sections.Count > 0)
            {
                outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "print_all.html"), printTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), heading }))));
            }

            await Task.WhenAll(outputTasks);
        }
        private string BuildFileName(TranslationManualSection section)
        {
            return section.filename;
        }
        //private List<OutputNavigation> BuildNavigation()
        private List<TranslationManualNavigationSection> BuildNavigation(List<TranslationManualSection> sections)
        {
            var output = new List<TranslationManualNavigationSection>(sections.Count);
            foreach (var section in sections)
            {
                if (section.TableOfContents == null)
                {
                    continue;
                }
                var navigationSection = new TranslationManualNavigationSection()
                {
                    FileName = BuildFileName(section),
                    Title = section.title,
                };

                var stack = new Stack<(TableOfContents tableOfContents, string fileName, bool lastChild, bool isTopLevel)>();

                stack.Push((section.TableOfContents, BuildFileName(section), false, true));
                while (stack.Count > 0)
                {
                    var (tableOfContents, fileName, lastChild, isTopLevel) = stack.Pop();
                    if (!isTopLevel)
                    {
                        navigationSection.Navigation.Add(new TranslationManaulNavigation()
                        {
                            filename = fileName,
                            hasChildren = tableOfContents.sections.Count != 0,
                            lastChild = lastChild,
                            title = tableOfContents.title,
                            slug = tableOfContents.link ?? ""
                        });
                    }

                    if (tableOfContents.sections.Count != 0)
                    {
                        // Put it on the stack backwards so things end up in the right order
                        for (var i = tableOfContents.sections.Count - 1; i >= 0; i--)
                        {
                            bool itemIsLastChild = !isTopLevel && i == tableOfContents.sections.Count - 1;
                            stack.Push((tableOfContents.sections[i], fileName, itemIsLastChild, false));
                        }
                    }
                }
                output.Add(navigationSection);
            }
            return output;
        }
        private async Task<List<TranslationManualSection>> GetSectionsAsync(ZipFileSystem fileSystem, string basePath, ResourceContainer resourceContainer, string baseUrl, string userToRouteResourcesTo, string languageCode)
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
                                throw new Exception($"Missing content for {project.path}/{item.link}");
                            }
                            var markdown = Markdown.Parse(content, pipeline);
                            foreach (var link in markdown.Descendants<LinkInline>())
                            {
                                if (link.Url == null)
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

        private async Task<string> GetSubTitleAsync(ZipFileSystem fileSystem, string slugPath)
        {
            var path = fileSystem.Join(slugPath, "sub-title.md");
            if (fileSystem.FileExists(path))
            {
                return await fileSystem.ReadAllTextAsync(path);
            }
            return null;
        }
        private async Task<string> GetTitleAsync(ZipFileSystem fileSystem, string slugPath)
        {
            var path = fileSystem.Join(slugPath, "title.md");
            if (fileSystem.FileExists(path))
            {
                return await fileSystem.ReadAllTextAsync(path);
            }
            return null;
        }
        private async Task<string> GetContentAsync(ZipFileSystem fileSystem, string slugPath)
        {
            var path = fileSystem.Join(slugPath, "01.md");
            if (fileSystem.FileExists(path))
            {
                return await fileSystem.ReadAllTextAsync(path);
            }
            return null;
        }
        private async Task<TableOfContents> LoadTableOfContentsAsync(ZipFileSystem fileSystem, string projectPath)
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
                throw new Exception($"Unable to load table of contents for {path}", ex);
            }
        }
    }
}
