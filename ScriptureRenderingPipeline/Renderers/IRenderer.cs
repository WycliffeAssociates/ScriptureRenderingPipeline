using System.Threading.Tasks;

namespace ScriptureRenderingPipeline.Renderers;

public interface IRenderer
{
    public Task RenderAsync(RendererInput input);
}