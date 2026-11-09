var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter(
    "System.Net.Http.HttpClient.vite",
    LogLevel.Warning
);

builder.Services.AddHttpClient("vite", client =>
{
    // Dirección del servidor de Vite (configurable con "Vite:Url"; por defecto el puerto de desarrollo habitual).
    client.BaseAddress = new Uri(builder.Configuration["Vite:Url"] ?? "http://localhost:5174");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Es un proxy transparente: las cookies son del navegador, no del servidor (con el contenedor por defecto, las
    // cookies de sesión de un usuario se guardarían aquí y se enviarían en las peticiones de todos), y las
    // redirecciones las sigue el navegador, no el proxy.
    UseCookies = false,
    AllowAutoRedirect = false
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.Map("/{**path}", async (
        HttpContext ctx,
        IHttpClientFactory factory,
        string? path) =>
    {
        var client = factory.CreateClient("vite");

        var targetPath = string.IsNullOrEmpty(path)
            ? "/"
            : "/" + path;

        var targetUri = targetPath + ctx.Request.QueryString;

        try
        {
            using var request = new HttpRequestMessage(
                new HttpMethod(ctx.Request.Method),
                targetUri);

            // Copiar el body del request (necesario para POST/PUT)
            if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                request.Content = new StreamContent(ctx.Request.Body);
            }

            // Copiar todos los headers del request original
            foreach (var header in ctx.Request.Headers)
            {
                if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                {
                    request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }
            }

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ctx.RequestAborted);

            ctx.Response.StatusCode = (int)response.StatusCode;

            // Se devuelven las cabeceras de la respuesta (Set-Cookie para iniciar y cerrar sesión, Location en las
            // redirecciones, Content-Type...) salvo las propias de cada conexión.
            foreach (var header in response.Headers.Concat(response.Content.Headers))
            {
                if (SkippedResponseHeaders.Contains(header.Key)) continue;
                ctx.Response.Headers[header.Key] = header.Value.ToArray();
            }

            await response.Content.CopyToAsync(ctx.Response.Body);
        }
        catch (HttpRequestException)
        {
            ctx.Response.StatusCode = 502;

            await ctx.Response.WriteAsync(
                "Vite no está corriendo. Ejecuta 'npm run dev' en ClientApp/.");
        }
    });
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

app.Run();

static partial class Program
{
    // Cabeceras que describen la conexión con Vite, no la respuesta: Kestrel pone las suyas.
    static readonly HashSet<string> SkippedResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Transfer-Encoding", "Connection", "Keep-Alive", "Proxy-Connection", "Upgrade", "Content-Length"
    };
}