using System;
using System.Collections.Generic;

namespace ShaderGen
{
    public class ShaderGenerationResult
    {
        private readonly Dictionary<LanguageBackend, List<ShaderSetSource>> _generatedShaders
            = new Dictionary<LanguageBackend, List<ShaderSetSource>>();

        public IReadOnlyList<ShaderSetSource> GetOutput(LanguageBackend backend)
        {
            if (_generatedShaders.Count == 0)
            {
                return Array.Empty<ShaderSetSource>();
            }

            if (!_generatedShaders.TryGetValue(backend, out List<ShaderSetSource> list))
            {
                throw new InvalidOperationException($"The backend {backend} was not used to generate shaders for this object.");
            }

            return list;
        }

        internal void AddShaderSet(LanguageBackend backend, ShaderSetSource gss)
        {
            if (!_generatedShaders.TryGetValue(backend, out List<ShaderSetSource> list))
            {
                list = new List<ShaderSetSource>();
                _generatedShaders.Add(backend, list);
            }

            list.Add(gss);
        }
    }
}
