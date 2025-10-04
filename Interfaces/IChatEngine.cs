using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyAI.Interfaces
{
    public interface IChatEngine
    {
        void Initialize();
        Task<string> GetFullReplyAsync(string prompt, CancellationToken ct = default);
        IAsyncEnumerable<string> StreamReplyAsync(string prompt, CancellationToken ct = default);

        Task LoadModelAsync(string modelPath, CancellationToken ct = default);
        string? CurrentModelPath { get; }
    }
}
