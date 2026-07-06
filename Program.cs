using System.Security.Cryptography;
using System.Text;
using Schedule_1_Calculator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton<MixCalculator>();

// Shared-password gate (HTTP Basic Auth). Set BasicAuth:Password to require a password before
// the site can be viewed; leave it empty to disable the gate (e.g. for local development).
// Configure it outside source control — via an environment variable (BasicAuth__Password),
// user-secrets, or appsettings. NOTE: Basic Auth sends the password on every request, so only
// expose the site over HTTPS.
var authUser = builder.Configuration["BasicAuth:Username"] ?? "friends";
var authPass = builder.Configuration["BasicAuth:Password"];

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Force HTTPS. This runs before the password gate so the password is never requested or sent
// over plain HTTP — an HTTP request is redirected to HTTPS first.
app.UseHttpsRedirection();

if (!string.IsNullOrEmpty(authPass))
{
    // User agents of link-preview crawlers (Discord, X, Slack, …). They can't log in, so we let
    // them past the gate to read the Open Graph tags — otherwise social embeds never render.
    var previewBots = new[]
    {
        "Discordbot", "Twitterbot", "facebookexternalhit", "Slackbot",
        "TelegramBot", "WhatsApp", "LinkedInBot", "redditbot"
    };

    app.Use(async (context, next) =>
    {
        // Let the preview image (fetched anonymously by Discord's image proxy) and known
        // link-preview crawlers through the gate so social embeds work without a password.
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var isPreviewRequest =
            string.Equals(context.Request.Path.Value, "/og-image.png", StringComparison.OrdinalIgnoreCase)
            || previewBots.Any(bot => userAgent.Contains(bot, StringComparison.OrdinalIgnoreCase));

        if (isPreviewRequest)
        {
            await next();
            return;
        }

        if (TryGetBasicCredentials(context.Request.Headers.Authorization, out var user, out var pass)
            && FixedEquals(user, authUser) && FixedEquals(pass, authPass))
        {
            await next();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"Schedule 1 Calculator\", charset=\"UTF-8\"";
        await context.Response.WriteAsync("Authentication required.");
    });

    static bool TryGetBasicCredentials(string? header, out string user, out string pass)
    {
        user = pass = string.Empty;
        if (header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
        }
        catch (FormatException)
        {
            return false;
        }

        int sep = decoded.IndexOf(':');
        if (sep < 0) return false;

        user = decoded[..sep];
        pass = decoded[(sep + 1)..];
        return true;
    }

    // Constant-time comparison so the check doesn't leak the password via timing.
    static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
