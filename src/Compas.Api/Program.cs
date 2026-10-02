using Compas.Api.Data;
using Compas.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Compás API",
        Version = "v1",
        Description = "API del planificador inteligente Compás."
    });
});

builder.Services.Configure<PlanningOptions>(builder.Configuration.GetSection("Planning"));

var dbProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "Sqlite";
builder.Services.AddDbContext<CompasDbContext>(options =>
{
    if (string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
    else
        options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite"));
});

builder.Services.AddScoped<IPlanningEngine, HeuristicPlanningEngine>();
builder.Services.AddScoped<PlannerService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultClient", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// HttpClient para el proxy hacia Vite en desarrollo
builder.Services.AddHttpClient("vite", c =>
{
    c.BaseAddress = new Uri("http://localhost:5173");
});

var app = builder.Build();

// Migraciones automáticas en desarrollo
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CompasDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Compás API v1"));

app.UseCors("DefaultClient");
app.UseAuthorization();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    // Proxy hacia Vite para todo lo que no sea /api ni /swagger
    app.MapFallback(async (HttpContext ctx, IHttpClientFactory factory) =>
    {
        var client = factory.CreateClient("vite");
        var path = ctx.Request.Path + ctx.Request.QueryString;
        try
        {
            var response = await client.GetAsync(path);
            ctx.Response.StatusCode = (int)response.StatusCode;
            ctx.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "text/html";
            await response.Content.CopyToAsync(ctx.Response.Body);
        }
        catch
        {
            ctx.Response.StatusCode = 502;
            await ctx.Response.WriteAsync("Vite no está corriendo. Ejecuta 'npm run dev' en ClientApp/.");
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
