var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter(
    "System.Net.Http.HttpClient.vite",
    LogLevel.Warning
);

builder.Services.AddHttpClient("vite", client =>
{
    client.BaseAddress = new Uri("http://localhost:5174");
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

            if (response.Content.Headers.ContentType != null)
            {
                ctx.Response.ContentType =
                    response.Content.Headers.ContentType.ToString();
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