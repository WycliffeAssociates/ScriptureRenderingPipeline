using System.Threading.Tasks;
using PipelineCommon.Helpers;

namespace ScriptureRenderingPipeline.Renderers;

public interface IRenderer
{
    public Task RenderAsync(RendererInput input, IOutputInterface output);
}