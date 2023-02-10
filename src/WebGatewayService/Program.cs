using LettuceEncrypt;
using Serilog;
using Serilog.Events;

try
{
    Console.WriteLine("Starting web gateway");
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddJsonFile("environment-configuration.json", true, true);
    builder.Logging.ClearProviders();

    builder.Host.UseSerilog((context, l) =>
    {
        l.ReadFrom.Configuration(context.Configuration);
        l.WriteTo.Console();

        var levels = new Dictionary<string, string>();
        context.Configuration.GetSection("Logging:LogLevel").Bind(levels);

        var defaultLevel = GetLogLevel(levels.GetValueOrDefault("Default", "Warning"), LogEventLevel.Warning);
        l.MinimumLevel.Is(defaultLevel);

        foreach (var kvp in levels.Where(kvp => !"Default".Equals(kvp.Key, StringComparison.InvariantCultureIgnoreCase)))
        {
            l.MinimumLevel.Override(kvp.Key, GetLogLevel(kvp.Value, defaultLevel));
        }
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

    if (builder.Configuration.GetValue<bool>("BlockUnSecureRequests"))
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.IsHttps)
            {
                await next(context);
            }
            else
            {
                context.Response.StatusCode = 400; // BadRequest
                await context.Response.WriteAsync("Only secured connections are allowed.");
            }
        });
    }

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
    Console.WriteLine("Started web gateway");
}
catch (Exception ex)
{
    Console.WriteLine("Application terminated unexpectedly.\n\n{0}\n\n{1}\n\n{2}", ex.Message, ex.StackTrace, ex);
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

static LogEventLevel GetLogLevel(string s, LogEventLevel @default)
{
    switch (s)
    {
        case "Trace":
            return LogEventLevel.Verbose;
        case "Debug":
            return LogEventLevel.Debug;
        case "Information":
            return LogEventLevel.Information;
        case "Warning":
            return LogEventLevel.Warning;
        case "Error":
            return LogEventLevel.Error;
        case "Critical":
        case "None":
            return LogEventLevel.Fatal;
        default:
            return @default;
    }
}
