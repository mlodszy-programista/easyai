using EasyAI.Interfaces;
using LLama;
using LLama.Common;
using LLama.Sampling;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyAI.Chat
{
    public sealed record LlamaEngineConfig(
        string ModelPath,
        int ContextSize,
        int Threads,
        int GpuLayers,
        string SystemPrompt,
        float Temperature,
        float TopP,
        int TopK,
        float RepeatPenalty,
        int MaxTokens,
        List<string> AntiPrompts);

    public sealed class LlamaChatEngine : IChatEngine, IDisposable
    {
        private readonly LlamaEngineConfig _config;
        private readonly SemaphoreSlim inferLock = new(1, 1);
        private LLamaWeights? weights;
        private LLamaContext? context;
        private InteractiveExecutor? executor;

        public string? CurrentModelPath { get; private set; }

        public LlamaChatEngine(LlamaEngineConfig config) {
            _config = config; 
        }

        public void Initialize()
        {
            if (executor is not null) return;
            LoadModelCore(_config.ModelPath);
        }

        public async Task LoadModelAsync(string modelPath, CancellationToken ct = default)
        {
            await inferLock.WaitAsync(ct);
            try
            {
                DisposeCore();
                LoadModelCore(modelPath);
            }
            finally
            {
                inferLock.Release();
            }
        }

        private void LoadModelCore(string modelPath)
        {
            var mp = new ModelParams(modelPath)
            {
                ContextSize = (uint?)_config.ContextSize,
                GpuLayerCount = _config.GpuLayers,
                Threads = Math.Max(_config.Threads, Environment.ProcessorCount - 1)
            };

            weights = LLamaWeights.LoadFromFile(mp);
            context = weights.CreateContext(mp);
            executor = new InteractiveExecutor(context);
            CurrentModelPath = modelPath;
        }

        private void DisposeCore()
        {
            executor = null;
            context?.Dispose(); context = null;
            weights?.Dispose(); weights = null;
        }

        public async Task<string> GetFullReplyAsync(string prompt, CancellationToken ct = default)
        {
            var sb = new System.Text.StringBuilder();
            await foreach (var piece in StreamReplyAsync(prompt, ct))
            {
                sb.Append(piece);
            }
            return sb.ToString();
        }

        public async IAsyncEnumerable<string> StreamReplyAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            if (executor is null)
                throw new InvalidOperationException("Engine not initialized.");

            await inferLock.WaitAsync(ct);
            try
            {
                var history = new ChatHistory();
                if (!string.IsNullOrWhiteSpace(_config.SystemPrompt))
                    history.AddMessage(AuthorRole.System, _config.SystemPrompt);

                var session = new ChatSession(executor, history);

                var infer = new InferenceParams
                {
                    MaxTokens = _config.MaxTokens,
                    AntiPrompts = _config.AntiPrompts,
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        Temperature = _config.Temperature,
                        TopP = _config.TopP,
                        TopK = _config.TopK,
                        RepeatPenalty = _config.RepeatPenalty
                    }
                };

                var userMsg = new ChatHistory.Message(AuthorRole.User, prompt);

                await foreach (var chunk in session.ChatAsync(userMsg, infer, ct))
                    yield return chunk;
            }
            finally
            {
                inferLock.Release();
            }
        }

        public void Dispose()
        {
            DisposeCore();
            inferLock.Dispose();
        }
    }
}
