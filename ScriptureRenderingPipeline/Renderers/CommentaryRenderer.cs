using DotLiquid;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ScriptureRenderingPipeline.Renderers
{
    internal class CommentaryRenderer
    {

        public async Task RenderAsync(ZipFileSystem sourceDir, string basePath, string destinationDir, Template template, Template printTemplate, string repoUrl, string heading, ResourceContainer resourceContainer, string baseUrl, string userToRouteResourcesTo, string textDirection, bool isBTTWriterProject = false)
        {
            LoadMarkdownFiles(sourceDir, basePath, resourceContainer.projects);
            return;
        }

        private async Task LoadMarkdownFiles(ZipFileSystem sourceDir, string basePath, Project[] projects)
        {
            foreach (var project in projects)
            {
                foreach (var chapter in sourceDir.GetFiles(sourceDir.Join(basePath, project.path), ".md"))
                {
                    var chapterSource = sourceDir.ReadAllText(chapter);
                }
            }
        }

    }
}
