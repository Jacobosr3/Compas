using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Compas.Api.Data;
using Compas.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// En la nube (Vercel, Render, Cloud Run…) el puerto lo indica la variable PORT.
if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Detrás del proxy del proveedor: se respetan X-Forwarded-For/Proto/Host para saber el esquema (https) y la IP reales.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear(); // la API solo recibe tráfico a través del proveedor
    options.KnownProxies.Clear();
});

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
var usePostgres = string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase);

// Las fechas del modelo son DateTime sin Kind=UTC: columnas "timestamp" (sin zona horaria).
if (usePostgres)
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddDbContext<CompasDbContext>(options =>
{
    if (usePostgres)
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
    else
        options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite"));
});

builder.Services.AddScoped<IPlanningEngine, HeuristicPlanningEngine>();
builder.Services.AddScoped<PlannerService>();
builder.Services.AddScoped<ProjectStatusService>();

// Integración con Google Calendar (credenciales en user-secrets: Google:ClientId / Google:ClientSecret).
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection("Google"));
builder.Services.AddMemoryCache();
builder.Services.AddDataProtection().SetApplicationName("Compas").PersistKeysToDbContext<CompasDbContext>();
builder.Services.AddHttpClient("google", c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<GoogleCalendarService>();

// Inicio de sesión: cookie de autenticación (HttpOnly, SameSite=Lax) y toda la API protegida salvo /api/auth y el
// callback de Google (que llevan [AllowAnonymous]). Sin sesión la API responde 401 en vez de redirigir.
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>(); // el usuario de la sesión; CompasDbContext lo usa para filtrar los datos
var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "compas.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax; // las peticiones desde otros sitios no llevan la cookie (frena CSRF)
        // En producción la cookie solo viaja por https; en desarrollo (http://localhost) basta con seguir a la petición.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(authOptions.RememberDays);
        options.SlidingExpiration = true; // cada uso renueva la sesión: un dispositivo usado a menudo no vuelve a pedir la contraseña
        // La cookie solo vale mientras ese usuario exista (y con el mismo correo) en ESTA base de datos: una cookie de otra
        // instancia o de un usuario borrado se rechaza aunque esté bien firmada.
        options.Events.OnValidatePrincipal = async ctx =>
        {
            var idText = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = ctx.Principal?.FindFirstValue(ClaimTypes.Email);
            var db = ctx.HttpContext.RequestServices.GetRequiredService<CompasDbContext>();
            var valid = int.TryParse(idText, out var id) && email is not null
                        && await db.Users.AnyAsync(u => u.Id == id && u.Email == email);
            if (!valid)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Freno a los intentos de contraseña (10 por minuto y dirección) y, más holgado, al arranque del flujo de Google.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsync("Demasiados intentos. Espera un minuto e inténtalo de nuevo.", ct);
    };
    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("oauth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

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

// Migraciones automáticas en desarrollo (solo SQLite; el esquema de Postgres/Supabase
// se gestiona con migraciones de Supabase, las de EF están generadas para SQLite).
if (app.Environment.IsDevelopment() && !usePostgres)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CompasDbContext>();
    db.Database.Migrate();
}

// Al arrancar se alinea el estado de los proyectos con sus tareas (completados / archivados).
try
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<ProjectStatusService>().SyncAllAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "No se pudo sincronizar el estado de los proyectos al arrancar.");
}

app.UseForwardedHeaders();

// La documentación de la API solo en desarrollo: en producción no se publica el esquema.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Compás API v1"));
}

app.UseCors("DefaultClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/healthz", () => Results.Ok("ok")).AllowAnonymous(); // comprobación de salud para el proveedor

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
    }).AllowAnonymous(); // la página debe cargar sin sesión: es la que muestra el inicio de sesión
}
else if (app.Environment.WebRootPath is not null && Directory.Exists(app.Environment.WebRootPath))
{
    // Solo si la API sirve también el cliente compilado (wwwroot); con el cliente en Vercel no hace falta.
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html").AllowAnonymous();
}

app.Run();
