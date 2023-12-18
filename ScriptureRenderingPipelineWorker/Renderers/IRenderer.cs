using PipelineCommon.Helpers;
using ScriptureRenderingPipelineWorker.Models;

namespace ScriptureRenderingPipelineWorker.Renderers;

public interface IRenderer
{
    public Task RenderAsync(RendererInput input, IOutputInterface output);
}