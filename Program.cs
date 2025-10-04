using EasyAI.Chat;
using EasyAI.Interfaces;
using EasyAI.Web;
using Microsoft.Extensions.Hosting.WindowsServices;
using System.Net;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
var config = builder.Configuration.GetSection("AppSettings").Get<AppSettings>()?? throw new Exception("appsettings.json not found.")
    ;
builder.Services.AddTransient<ModelCatalog>();

builder.Host.UseWindowsService(o => o.ServiceName = "EasyAI");
builder.WebHost.UseUrls(config.Address);

builder.Services.AddSingleton<IHtmlPageRenderer, HtmlPageRenderer>();
builder.Services.AddSingleton<IModelCatalog, ModelCatalog>();
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.AddSingleton<IChatEngine>(sp =>
{
var baseDir = config.ModelsPath;
    var defaultModel = Path.Combine(baseDir, config.DefaultModel);
    var engine = new LlamaChatEngine(
            new LlamaEngineConfig(
                ModelPath: defaultModel,
                ContextSize: config.ContextSize,
                GpuLayers: (int)config.GpuLayers,
                Threads: Math.Max(config.Threads, Environment.ProcessorCount - 1),
                SystemPrompt: config.InitPrompt,
                Temperature: config.Temperature,
                TopK: config.TopK,
                TopP: config.TopP,
                RepeatPenalty: config.RepeatPenalty,
                AntiPrompts: config.AntiPrompts,
                MaxTokens: config.MaxTokens
            )
    );
    engine.Initialize();
    return engine;
});

if (OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService())
{
    #pragma warning disable CA1416
        builder.Logging.AddEventLog(o =>
        {
            o.SourceName = "EasyAI";
        });
    #pragma warning restore CA1416
}
else
{
    builder.Logging.AddConsole();
}

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("EasyAI starting...");

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

// HTML
app.MapGet("/", (IHtmlPageRenderer html) =>
    Results.Content(html.Render(string.Empty, string.Empty), "text/html; charset=utf-8"));
app.MapPost("/prompt", async (HttpRequest req, IChatEngine engine, IHtmlPageRenderer html) =>
{
    if (!req.HasFormContentType) return Results.BadRequest("Invalid form data.");
    var form = await req.ReadFormAsync();
    var prompt = (form["prompt"].ToString() ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(prompt))
        return Results.Content(html.Render(string.Empty, string.Empty), "text/html; charset=utf-8");

    var reply = await engine.GetFullReplyAsync(prompt, req.HttpContext.RequestAborted);
    return Results.Content(html.Render(prompt, reply), "text/html; charset=utf-8");
});

app.MapPost("/api/prompt", async (PromptDto dto, IChatEngine engine, HttpContext ctx) =>
{
    var text = (dto?.Prompt ?? string.Empty).Trim();
    var reply = await engine.GetFullReplyAsync(text, ctx.RequestAborted);
    return Results.Json(new { prompt = text, reply, timestamp = DateTimeOffset.Now },
        new JsonSerializerOptions { WriteIndented = true });
});

app.MapPost("/api/stream", PostPromptSse);

app.MapGet("/api/models", (IModelCatalog catalog) =>
{
    var list = catalog.ListModels();
    return Results.Json(new { models = list });
});

app.MapGet("/api/current-model", (IChatEngine engine, IModelCatalog catalog) =>
{
    var path = engine.CurrentModelPath ?? "";
    var name = string.IsNullOrWhiteSpace(path) ? "" : Path.GetFileName(path);
    return Results.Json(new { name, path, root = catalog.ModelsRoot });
});

app.MapPost("/api/select-model", async (ModelSelectDto dto, IModelCatalog catalog, IChatEngine engine) =>
{
    if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
        return Results.BadRequest("Model name required.");

    var full = Path.GetFullPath(Path.Combine(catalog.ModelsRoot, dto.Name));
    if (!File.Exists(full))
        return Results.NotFound(new { error = "Model file not found.", dto.Name });

    await engine.LoadModelAsync(full);
    return Results.Ok(new { selected = dto.Name });
});

app.MapGet("/health", () => Results.Ok(new { status = "OK", time = DateTimeOffset.Now }));

await app.RunAsync();

static async Task PostPromptSse(PromptDto dto, IChatEngine engine, HttpContext ctx)
{
    ctx.Response.StatusCode = (int)HttpStatusCode.OK;
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("Connection", "keep-alive");

    await foreach (var chunk in engine.StreamReplyAsync(dto?.Prompt ?? string.Empty, ctx.RequestAborted))
    {
        var text = chunk.Replace("\r\n", "\n").Replace("\r", "\n");
        foreach (var line in text.Split('\n'))
        {
            await ctx.Response.WriteAsync($"data: {line}\n", ctx.RequestAborted);
        }
        await ctx.Response.WriteAsync("\n", ctx.RequestAborted); // koniec eventu
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
    }

    await ctx.Response.WriteAsync("event: done\ndata: [DONE]\n\n", ctx.RequestAborted);
}

public sealed record PromptDto(string Prompt);
public sealed record ModelSelectDto(string Name);
