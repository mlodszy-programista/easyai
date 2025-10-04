using EasyAI.Interfaces;
using Microsoft.Extensions.Options;

namespace EasyAI.Chat
{
    public sealed class ModelCatalog : IModelCatalog
    {
        private readonly ILogger<ModelCatalog> _logger;
        private readonly AppSettings _settings;
        public string ModelsRoot { get; }

        public ModelCatalog(IOptions<AppSettings> options, ILogger<ModelCatalog> logger)
        {
            _settings = options.Value;
            _logger = logger;
            var baseDir = _settings.ModelsPath;

            if (Path.IsPathRooted(baseDir))
            {
                ModelsRoot = baseDir;
            }
            else
            {
                _logger.LogError("Error in model path.");
                throw new Exception("Error in model path.");
            }

            Directory.CreateDirectory(ModelsRoot);
            _logger = logger;
        }

        public IReadOnlyList<string> ListModels()
        {
            if (!Directory.Exists(ModelsRoot))
                return Array.Empty<string>();

            return Directory.EnumerateFiles(ModelsRoot, "*.gguf", SearchOption.TopDirectoryOnly)
                            .Select(Path.GetFileName)
                            .Where(n => n is not null && n.Length > 0)
                            .Select(n => n!)
                            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
        }
    }
}
