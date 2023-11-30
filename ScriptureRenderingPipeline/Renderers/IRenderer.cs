using System.Threading.Tasks;
using PipelineCommon.Helpers;
using ScriptureRenderingPipeline.Models;

namespace ScriptureRenderingPipeline.Renderers;

public interface IRenderer
{
    public Task RenderAsync(RendererInput input, IOutputInterface output);
}