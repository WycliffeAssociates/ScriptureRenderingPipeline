using DotLiquid;
using ScriptureRenderingPipeline.Helpers;
using ScriptureRenderingPipeline.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using System.Text;
using Markdig;
using System.Linq;

namespace ScriptureRenderingPipeline.Renderers
{
    public class TranslationManualRenderer
    {
        public void Render(ZipFileSystem sourceDir, string basePath, string destinationDir, Template template, string repoUrl, string heading, ResourceContainer resourceContainer, bool isBTTWriterProject = false)
        {
            var sections = GetSections(sourceDir, basePath, resourceContainer);
            var navigation = BuildNavigation(sections);
            foreach(var category in sections)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"<h1>{category.title}</h1>");
                foreach(var content in category.Content)
                {
                    builder.AppendLine($"<div id=\"{content.slug}\"></div>");
                    builder.AppendLine($"<h2>{content.title}</h2>");
                    builder.AppendLine(content.content);
                    builder.AppendLine("<hr/>");
                }
                var templateResult = template.Render(Hash.FromAnonymousObject(new
                {
                    content = builder.ToString(),
                    contenttype = "tw",
                    translationManualNavigation = navigation,
                    currentPage = category.filename,
                    heading,
                    sourceLink = repoUrl
                }
                ));

                File.WriteAllText(Path.Join(destinationDir, BuildFileName(category)),templateResult);
            }

            if (sections.Count > 0)
            {
                File.Copy(Path.Join(destinationDir,BuildFileName(sections[0])), Path.Combine(destinationDir, "index.html"));
            }
        }
        private string BuildFileName(TranslationManualSection section)
        {
            return section.filename;
        }
        private List<TranslationManualNavigationSection> BuildNavigation(List<TranslationManualSection> sections)
        {
            var output = new List<TranslationManualNavigationSection>(sections.Count);
            foreach(var section in sections)
            {
                var navigationSection = new TranslationManualNavigationSection()
                {
                    FileName = BuildFileName(section),
                    Title = section.title,
                };

                var stack = new Stack<(TableOfContents tableOfContents, string fileName, int direction, bool isTopLevel)>();

                stack.Push((section.TableOfContents, BuildFileName(section), 0, true));
                while (stack.Count > 0)
                {
                    var (tableOfContents, fileName, direction, isTopLevel) = stack.Pop();
                    if (!isTopLevel)
                    {
                        navigationSection.Navigation.Add(new TranslationManaulNavigation()
                        {
                            filename = fileName,
                            direction = tableOfContents.sections.Count == 0 ? direction : 1,
                            title = tableOfContents.title,
                            slug = tableOfContents.link ?? ""
                        }); 
                    }

                    if (tableOfContents.sections.Count != 0)
                    {
                        // Put it on the stack backwards so things end up in the right order
                        for (var i = tableOfContents.sections.Count - 1; i >= 0; i--)
                        {
                            int childDirection = 0;
                            if ( !isTopLevel && i == tableOfContents.sections.Count - 1)
                            {
                                childDirection = -1;
                            }
                            stack.Push((tableOfContents.sections[i], fileName, childDirection, false));
                        }
                    }
                }
                output.Add(navigationSection);
            }
            return output;
        }
        private List<TranslationManualSection> GetSections(ZipFileSystem fileSystem, string basePath, ResourceContainer resourceContainer)
        {
            var output = new List<TranslationManualSection>();
            foreach(var project in resourceContainer.projects.OrderBy(p => p.sort))
            {
                var section = new TranslationManualSection(project.title,project.path, project.sort.ToString() + ".html");
                // Load table of contents
                var tableOfContents = LoadTableOfContents(fileSystem, fileSystem.Join(basePath, project.path));
                section.TableOfContents = tableOfContents;
                if (tableOfContents != null)
                {
                    var stack = new Stack<TableOfContents>(new[] { tableOfContents });
                    while(stack.Count > 0)
                    {
                        var item = stack.Pop();
                        if (item.link != null)
                        {
                            var path = fileSystem.JoinPath(basePath, project.path, item.link);
                            section.Content.Add(new TranslationManualContent()
                            {
                                title = GetTitle(fileSystem, path),
                                slug = item.link,
                                subtitle = GetSubTitle(fileSystem, path),
                                content = Markdown.ToHtml(GetContent(fileSystem, path)),
                            }); 
                        }

                        if (item.sections.Count != 0)
                        {
                            // Put it on the stack backwards so things end up in the right order
                            for (var i = item.sections.Count - 1;  i >= 0; i--)
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
        private string GetSubTitle(ZipFileSystem fileSystem, string slugPath)
        {
            var path = fileSystem.Join(slugPath, "sub-title.md");
            if (fileSystem.FileExists(path))
            {
                return fileSystem.ReadAllText(path);
            }
            return null;
        }
        private string GetTitle(ZipFileSystem fileSystem, string slugPath)
        {
            var path = fileSystem.Join(slugPath, "title.md");
            if (fileSystem.FileExists(path))
            {
                return fileSystem.ReadAllText(path);
            }
            return null;
        }
        private string GetContent(ZipFileSystem fileSystem, string slugPath)
        {
            var path = fileSystem.Join(slugPath, "01.md");
            if (fileSystem.FileExists(path))
            {
                return fileSystem.ReadAllText(path);
            }
            return null;
        }
        private TableOfContents LoadTableOfContents(ZipFileSystem fileSystem, string projectPath)
        {
            var path = fileSystem.Join(projectPath, "toc.yaml");
            if (fileSystem.FileExists(path))
            {
                try
                {
                    var serializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                    return serializer.Deserialize<TableOfContents>(fileSystem.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    throw new Exception($"Unable to load table of contents for {path}", ex);
                }
            }
            return null;
        }
        private Dictionary<string,TranslationManualConfig> LoadConfig(ZipFileSystem fileSystem, Project project, string slug)
        {
            var path = fileSystem.Join(project.path, slug, "config.yaml");
            if (fileSystem.FileExists(path))
            {
                Deserializer serializer = new Deserializer();
                return serializer.Deserialize<Dictionary<string,TranslationManualConfig>>(fileSystem.ReadAllText(path));
            }

            return null;
        }
    }
    public class TranslationManualConfig
    {
        public List<string> recommend { get; set; }
        public List<string> dependencies { get; set; }
    }
    public class TranslationManualContent
    {
        public string title { get; set;  }
        public string subtitle { get; set; }
        public string content { get; set; }
        public string slug { get; set; }
    }
    public class TranslationManualSection
    {
        public string title { get; set; }
        public string path { get; set; }
        public TableOfContents TableOfContents { get; set; }
        public List<TranslationManualContent> Content { get; set; }
        public string filename { get; set; }

        public TranslationManualSection(string title, string path, string filename)
        {
            this.title = title;
            this.path = path;
            this.filename = filename;
            Content = new List<TranslationManualContent>();
        }
    }
    public class TableOfContents
    {
        public string title { get; set;  }
        public string link { get; set; }
        public List<TableOfContents> sections {  get; set; }
        public TableOfContents(string title, string link)
        {
            this.title = title;
            this.link = link;
            sections = new List<TableOfContents>();
        }
        public TableOfContents()
        {
            sections = new List<TableOfContents>();
        }
    }
    public class TranslationManualNavigationSection: ILiquidizable
    {
        public string Title { get; set; }
        public string FileName { get; set; }
        public List<TranslationManaulNavigation> Navigation { get; set; }
        public TranslationManualNavigationSection()
        {
            Navigation = new List<TranslationManaulNavigation>();
        }

        public object ToLiquid()
        {
            return new
            {
                title = Title,
                fileName = FileName,
                navigation = Navigation,
            };
        }
    }
    public class TranslationManaulNavigation: ILiquidizable
    {
        public string title {  get; set; }
        public string filename { get; set; }
        /// <summary>
        /// 0 for no change, 1 for descending a level, and -1 for ascending
        /// </summary>
        public int direction { get; set; }
        public string slug { get; set; }

        public object ToLiquid()
        {
            return new
            {
                title,
                filename,
                direction,
                slug
            };
        }
    }
}
