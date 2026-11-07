using OpenGE.DXC;
using OpenGE.Graphics.Decoders;
using OpenGE.Graphics.Resources;
using OpenGE.Importing;

namespace OpenGE.Sandbox.Importing;

internal sealed class MyImporter() : Importer(AppContext.BaseDirectory)
{
    protected override IEnumerable<Decoder> Compose()
    {
        yield return new ShaderDecoder(
            ShaderLanguage.Hlsl,
            new DxcShaderCompiler(),
            ShaderFormat.SpirV
        );
    }
}
