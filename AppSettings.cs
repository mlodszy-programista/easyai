public class AppSettings
{
    public required string DefaultModel { get; set; }
    public required string ModelsPath { get; set; }
    public required string Address { get; set; }
    public required string InitPrompt { get; set; }
    public required List<string> AntiPrompts { get; set; }
    public int MaxTokens { get; set; }
    public float Temperature { get; set; }
    public int TopK { get;  set; }
    public float TopP { get; set; }
    public float RepeatPenalty { get;  set; }
    public int Threads { get;  set; }
    public int ContextSize { get;  set; }
    public uint GpuLayers { get;  set; }
}