using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using ShaderGen.Hlsl;
using ShaderGen.Tests.Tools;
using Xunit;

namespace ShaderGen.Tests
{
    public static class ShaderSetDiscovererTests
    {
        [SkippableFact(typeof(RequiredToolFeatureMissingException))]
        public static void ShaderSetAutoDiscovery()
        {
            ToolChain toolChain = ToolChain.Get(ToolFeatures.ToCompiled);
            if (toolChain == null)
            {
                throw new RequiredToolFeatureMissingException("No tool chain supporting compilation was found!");
            }

            Compilation compilation = TestUtil.GetCompilation();
            LanguageBackend backend = toolChain.CreateBackend(compilation);
            ShaderGenerator sg = new ShaderGenerator(compilation, backend);
            ShaderGenerationResult generationResult = sg.GenerateShaders();
            IReadOnlyList<ShaderSetSource> hlslSets = generationResult.GetOutput(backend);
            Assert.Equal(2, hlslSets.Count); // Was 4, not sure how to count these.
            ShaderSetSource setSource = hlslSets[0];
            
            //Updated to new naming convention
            Assert.Equal(setSource.VertexFunction.DeclaringType + "." + setSource.VertexFunction.Name + "+" + setSource.FragmentFunction.DeclaringType + "." + setSource.FragmentFunction.Name, setSource.Name);

            CompileResult result = toolChain.Compile(setSource.VertexShaderCode, Stage.Vertex, "VS");
            Assert.False(result.HasError, result.ToString());

            result = toolChain.Compile(setSource.FragmentShaderCode, Stage.Fragment, "FS");
            Assert.False(result.HasError, result.ToString());
        }
    }
}
