using System.Collections.Generic;

namespace EasyAI.Interfaces
{
    public interface IModelCatalog
    {
        IReadOnlyList<string> ListModels();
        string ModelsRoot { get; }
    }
}
