using LettuceEncrypt;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
try
{
    Log.Information("Starting web gateway");
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddJsonFile("environment-configuration.json", true, true);
    builder.Logging.ClearProviders();
    builder.Host.UseSerilog((h, l) =>
    {
        l.ReadFrom.Configuration(h.Configuration);
        l.WriteTo.Console();
    });

    AddLettuce(builder);

    builder.Services
        .AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app
        .UseHsts()
        .UseHttpsRedirection();

    app.MapGet("/.info", async context =>
    {
        var result = new Dictionary<string, string>
        {
            ["_.now"] = DateTime.UtcNow.ToString("O"),
            ["verb"] = context.Request.Method,
            ["path"] = context.Request.Path
        };

        foreach (var header in context.Request.Headers.OrderBy(o => o.Key))
        {
            result[$"headers.{header.Key}"] = header.Value.ToString();
        }

        foreach (var cookie in context.Request.Cookies.OrderBy(o => o.Key))
        {
            result[$"cookies.{cookie.Key}"] = cookie.Value;
        }

        await context.Response.WriteAsJsonAsync(result, context.RequestAborted);
    });
    app.MapReverseProxy();

    await app.RunAsync();
    Log.Information("Started web gateway");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static void AddLettuce(WebApplicationBuilder webApplicationBuilder)
{
    var lettuceConfiguration = webApplicationBuilder.Configuration.GetSection("LettuceEncrypt");
    if (!lettuceConfiguration.Exists())
        return;

    var lettuce = webApplicationBuilder.Services.AddLettuceEncrypt();
    var password = lettuceConfiguration.GetValue<string>("PersistPassword");
    if (String.IsNullOrEmpty(password))
        return;

    var path = lettuceConfiguration.GetValue<string>("PersistPath");
    if (String.IsNullOrEmpty(path))
    {
        path = "../app_data/";
    }

    if (!Path.IsPathRooted(path))
    {
        path = GetEnvironmentPath(path);
    }

    lettuce.PersistDataToDirectory(new DirectoryInfo(path), password);
}

static string GetEnvironmentPath(string relativePath) =>
    Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, relativePath);
