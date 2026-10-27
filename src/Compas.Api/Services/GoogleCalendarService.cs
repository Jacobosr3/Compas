using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Compas.Api.Services;

/// <summary>Error esperable al hablar con Google (configuración, permisos, token revocado...).</summary>
public class GoogleException : Exception
{
    /// <summary>Si es true, hay que volver a conectar la cuenta.</summary>
    public bool NeedsReconnect { get; }

    public GoogleException(string message, bool needsReconnect = false) : base(message) => NeedsReconnect = needsReconnect;
}

/// <summary>
/// Conexión con Google Calendar: OAuth (el usuario elige la cuenta), lista de calendarios,
/// selección de los que se incluyen en Compás e importación de sus eventos como eventos fijos.
/// Sin dependencias nuevas: llamadas HTTP directas y cifrado con Data Protection de ASP.NET.
/// </summary>
public class GoogleCalendarService
{
    private const string Scopes = "openid email https://www.googleapis.com/auth/calendar.readonly";
    private const string AccessTokenCacheKey = "google-access-token";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    // Evita dos sincronizaciones a la vez (botón de la agenda y planificador).
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    private readonly CompasDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly GoogleOptions _options;
    private readonly IMemoryCache _cache;
    private readonly IDataProtector _protector;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(
        CompasDbContext db,
        IHttpClientFactory httpFactory,
        IOptions<GoogleOptions> options,
        IMemoryCache cache,
        IDataProtectionProvider dataProtection,
        ILogger<GoogleCalendarService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _options = options.Value;
        _cache = cache;
        _protector = dataProtection.CreateProtector("Compas.Google.RefreshToken");
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    // ---------------- Conexión (OAuth) ----------------

    /// <summary>
    /// Dirección de Google a la que enviar al usuario. Con "select_account" Google le pide elegir la
    /// cuenta, y "consent" garantiza que devuelva un token de actualización.
    /// </summary>
    public string BuildAuthUrl(string? returnUrl)
    {
        if (!IsConfigured)
            throw new GoogleException("Google Calendar no está configurado: faltan el ClientId y el ClientSecret.");

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        _cache.Set(StateKey(state), SafeReturnUrl(returnUrl), StateLifetime);

        return QueryHelpers.AddQueryString(_options.AuthUrl, new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["access_type"] = "offline",
            ["prompt"] = "select_account consent",
            ["state"] = state
        });
    }

    /// <summary>Recupera (y consume) la dirección de vuelta asociada a un "state"; null si no es válido o caducó.</summary>
    public string? TakeReturnUrl(string? state)
    {
        if (string.IsNullOrEmpty(state) || !_cache.TryGetValue(StateKey(state), out string? url)) return null;
        _cache.Remove(StateKey(state));
        return url;
    }

    public string DefaultReturnUrl => SafeReturnUrl(null);

    /// <summary>Intercambia el código de Google por tokens, guarda la cuenta y carga sus calendarios.</summary>
    public async Task CompleteConnectionAsync(string code, CancellationToken ct = default)
    {
        var tokens = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = _options.RedirectUri,
            ["grant_type"] = "authorization_code"
        }, ct);

        var access = tokens["access_token"]?.GetValue<string>() ?? throw new GoogleException("Google no devolvió un token de acceso.");
        var refresh = tokens["refresh_token"]?.GetValue<string>();
        CacheAccessToken(access, tokens["expires_in"]);

        var email = (await GetJsonAsync(_options.UserInfoUrl, access, ct))["email"]?.GetValue<string>() ?? "(cuenta de Google)";

        var account = await _db.GoogleAccounts.FirstOrDefaultAsync(ct);
        if (account is not null && !string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            // Otra cuenta distinta: se parte de cero para no mezclar calendarios ni eventos.
            await RemoveAllDataAsync(ct);
            account = null;
        }

        if (account is null)
        {
            if (refresh is null)
                throw new GoogleException("Google no devolvió permiso para mantener la conexión. Inténtalo de nuevo.");
            account = new GoogleAccount { Email = email, RefreshToken = _protector.Protect(refresh) };
            _db.GoogleAccounts.Add(account);
        }
        else if (refresh is not null)
        {
            account.RefreshToken = _protector.Protect(refresh);
        }

        account.ConnectedAt = DateTime.Now;
        account.LastSyncError = null;
        await _db.SaveChangesAsync(ct);

        await RefreshCalendarsAsync(ct);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var account = await _db.GoogleAccounts.FirstOrDefaultAsync(ct);
        if (account is null) return;

        // Se intenta revocar el permiso en Google; si falla, igualmente se borra todo lo local.
        try
        {
            var refresh = _protector.Unprotect(account.RefreshToken);
            using var revoked = await _httpFactory.CreateClient("google").PostAsync(
                _options.RevokeUrl, new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refresh }), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo revocar el permiso de Google (se borra igualmente la conexión local).");
        }

        await RemoveAllDataAsync(ct);
    }

    // ---------------- Estado y calendarios ----------------

    public async Task<GoogleStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var account = await _db.GoogleAccounts.AsNoTracking().FirstOrDefaultAsync(ct);
        var calendars = account is null
            ? new List<GoogleCalendarDto>()
            : (await _db.GoogleCalendarSelections.AsNoTracking()
                    .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Summary).ToListAsync(ct))
                .Select(c => new GoogleCalendarDto(c.CalendarId, c.Summary, c.ColorHex, c.IsPrimary, c.Selected)).ToList();

        return new GoogleStatusDto(IsConfigured, account is not null, account?.Email, account?.LastSyncAt, account?.LastSyncError, calendars);
    }

    /// <summary>Vuelve a leer la lista de calendarios de Google y la fusiona con la selección guardada.</summary>
    public async Task RefreshCalendarsAsync(CancellationToken ct = default)
    {
        var account = await RequireAccountAsync(ct);
        var token = await GetAccessTokenAsync(account, ct);

        var remote = new List<(string Id, string Summary, string? Color, bool Primary)>();
        string? pageToken = null;
        do
        {
            var url = QueryHelpers.AddQueryString($"{_options.CalendarApiBase}/users/me/calendarList",
                new Dictionary<string, string?> { ["minAccessRole"] = "reader", ["maxResults"] = "250", ["pageToken"] = pageToken });
            var json = await GetJsonAsync(url, token, ct);
            foreach (var item in json["items"]?.AsArray() ?? new JsonArray())
            {
                var id = item?["id"]?.GetValue<string>();
                if (id is null) continue;
                var summary = item!["summaryOverride"]?.GetValue<string>() ?? item["summary"]?.GetValue<string>() ?? id;
                remote.Add((id, summary, item["backgroundColor"]?.GetValue<string>(), item["primary"]?.GetValue<bool>() ?? false));
            }
            pageToken = json["nextPageToken"]?.GetValue<string>();
        } while (pageToken is not null);

        var stored = await _db.GoogleCalendarSelections.ToListAsync(ct);
        var firstTime = stored.Count == 0;

        foreach (var r in remote)
        {
            var row = stored.FirstOrDefault(s => s.CalendarId == r.Id);
            if (row is null)
            {
                // La primera vez se propone el calendario principal; el resto, el usuario decide.
                _db.GoogleCalendarSelections.Add(new GoogleCalendarSelection
                {
                    CalendarId = r.Id, Summary = r.Summary, ColorHex = r.Color, IsPrimary = r.Primary, Selected = firstTime && r.Primary
                });
            }
            else
            {
                row.Summary = r.Summary;
                row.ColorHex = r.Color;
                row.IsPrimary = r.Primary;
            }
        }

        // Calendarios que ya no existen en la cuenta (o a los que se perdió el acceso).
        var gone = stored.Where(s => remote.All(r => r.Id != s.CalendarId)).ToList();
        _db.GoogleCalendarSelections.RemoveRange(gone);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Guarda qué calendarios se incluyen en Compás y sincroniza.</summary>
    public async Task SaveSelectionAsync(IReadOnlyCollection<string> calendarIds, CancellationToken ct = default)
    {
        await RequireAccountAsync(ct);
        var rows = await _db.GoogleCalendarSelections.ToListAsync(ct);
        foreach (var row in rows) row.Selected = calendarIds.Contains(row.CalendarId);
        await _db.SaveChangesAsync(ct);

        await SyncAsync(ct);
    }

    // ---------------- Sincronización de eventos ----------------

    /// <summary>
    /// Importa los eventos de los calendarios seleccionados (hoy y los próximos días) como eventos
    /// fijos. Es idempotente: actualiza los ya importados y elimina los que ya no existen en Google
    /// o pertenecen a un calendario que se ha dejado de incluir.
    /// </summary>
    public async Task<int> SyncAsync(CancellationToken ct = default)
    {
        await SyncLock.WaitAsync(ct);
        try
        {
            var account = await RequireAccountAsync(ct);
            try
            {
                var token = await GetAccessTokenAsync(account, ct);
                var selected = await _db.GoogleCalendarSelections.Where(c => c.Selected).ToListAsync(ct);
                var selectedIds = selected.Select(c => c.CalendarId).ToHashSet();

                var from = DateTime.Today;
                var to = from.AddDays(Math.Max(1, _options.SyncDays));

                var fetched = new Dictionary<(string Calendar, string Event), CalendarEvent>();
                foreach (var cal in selected)
                    foreach (var evt in await FetchEventsAsync(cal, token, from, to, ct))
                        fetched[(evt.ExternalCalendarId!, evt.ExternalId!)] = evt;

                var existing = await _db.CalendarEvents
                    .Where(e => e.Source == EventSource.Import && e.ExternalCalendarId != null).ToListAsync(ct);
                var seen = new HashSet<(string, string)>();

                foreach (var e in existing)
                {
                    var key = (e.ExternalCalendarId!, e.ExternalId ?? "");
                    if (!selectedIds.Contains(e.ExternalCalendarId!))
                    {
                        _db.CalendarEvents.Remove(e); // calendario que ya no se incluye
                    }
                    else if (fetched.TryGetValue(key, out var fresh))
                    {
                        e.Title = fresh.Title;
                        e.StartTime = fresh.StartTime;
                        e.EndTime = fresh.EndTime;
                        e.IsAllDay = fresh.IsAllDay;
                        e.Notes = fresh.Notes;
                        seen.Add(key);
                    }
                    else if (e.EndTime > from && e.StartTime < to)
                    {
                        _db.CalendarEvents.Remove(e); // ya no existe (o cambió de día) en Google
                    }
                }

                foreach (var (key, evt) in fetched)
                    if (!seen.Contains(key)) _db.CalendarEvents.Add(evt);

                account.LastSyncAt = DateTime.Now;
                account.LastSyncError = null;
                await _db.SaveChangesAsync(ct);
                return fetched.Count;
            }
            catch (GoogleException ex)
            {
                account.LastSyncError = ex.Message;
                await _db.SaveChangesAsync(ct);
                throw;
            }
        }
        finally
        {
            SyncLock.Release();
        }
    }

    /// <summary>
    /// Sincroniza solo si hay cuenta conectada y la última sincronización es antigua. Nunca lanza:
    /// planificar no debe fallar porque Google no responda.
    /// </summary>
    public async Task TrySyncIfStaleAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        try
        {
            if (!IsConfigured) return;
            var account = await _db.GoogleAccounts.AsNoTracking().FirstOrDefaultAsync(ct);
            if (account is null) return;
            if (account.LastSyncAt is { } last && DateTime.Now - last < maxAge) return;
            await SyncAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudo sincronizar Google Calendar antes de planificar.");
        }
    }

    // ---------------- Internos ----------------

    private async Task<List<CalendarEvent>> FetchEventsAsync(
        GoogleCalendarSelection calendar, string token, DateTime from, DateTime to, CancellationToken ct)
    {
        var result = new List<CalendarEvent>();
        string? pageToken = null;
        do
        {
            var url = QueryHelpers.AddQueryString(
                $"{_options.CalendarApiBase}/calendars/{Uri.EscapeDataString(calendar.CalendarId)}/events",
                new Dictionary<string, string?>
                {
                    ["timeMin"] = new DateTimeOffset(from).ToString("o", CultureInfo.InvariantCulture),
                    ["timeMax"] = new DateTimeOffset(to).ToString("o", CultureInfo.InvariantCulture),
                    ["singleEvents"] = "true", // expande los eventos repetidos en ocurrencias
                    ["orderBy"] = "startTime",
                    ["maxResults"] = "250",
                    ["pageToken"] = pageToken
                });

            var json = await GetJsonAsync(url, token, ct);
            foreach (var item in json["items"]?.AsArray() ?? new JsonArray())
                if (item is not null && ToCalendarEvent(item, calendar.CalendarId) is { } evt)
                    result.Add(evt);

            pageToken = json["nextPageToken"]?.GetValue<string>();
        } while (pageToken is not null);

        return result;
    }

    /// <summary>Convierte un evento de Google en un evento fijo; null si no debe bloquear tiempo.</summary>
    private static CalendarEvent? ToCalendarEvent(JsonNode item, string calendarId)
    {
        var id = item["id"]?.GetValue<string>();
        if (id is null) return null;

        if (item["status"]?.GetValue<string>() == "cancelled") return null;
        if (item["transparency"]?.GetValue<string>() == "transparent") return null; // marcado como "libre"
        if (item["eventType"]?.GetValue<string>() == "workingLocation") return null;

        // Eventos que el usuario ha rechazado.
        foreach (var attendee in item["attendees"]?.AsArray() ?? new JsonArray())
            if (attendee?["self"]?.GetValue<bool>() == true && attendee["responseStatus"]?.GetValue<string>() == "declined")
                return null;

        DateTime start, end;
        bool allDay;
        var startDate = item["start"]?["date"]?.GetValue<string>();
        if (startDate is not null)
        {
            allDay = true;
            start = DateTime.ParseExact(startDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var endDate = item["end"]?["date"]?.GetValue<string>();
            end = endDate is null ? start.AddDays(1) : DateTime.ParseExact(endDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        else
        {
            var startText = item["start"]?["dateTime"]?.GetValue<string>();
            var endText = item["end"]?["dateTime"]?.GetValue<string>();
            if (startText is null || endText is null) return null;
            allDay = false;
            // La app trabaja con la hora local del equipo.
            start = DateTimeOffset.Parse(startText, CultureInfo.InvariantCulture).LocalDateTime;
            end = DateTimeOffset.Parse(endText, CultureInfo.InvariantCulture).LocalDateTime;
        }

        if (end <= start) return null;

        var title = item["summary"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(title)) title = "(Sin título)";

        return new CalendarEvent
        {
            Title = title.Length > 300 ? title[..300] : title,
            StartTime = start,
            EndTime = end,
            IsAllDay = allDay,
            Source = EventSource.Import,
            Notes = item["location"]?.GetValue<string>(),
            ExternalId = id,
            ExternalCalendarId = calendarId
        };
    }

    private async Task<GoogleAccount> RequireAccountAsync(CancellationToken ct) =>
        await _db.GoogleAccounts.FirstOrDefaultAsync(ct)
        ?? throw new GoogleException("No hay ninguna cuenta de Google conectada.", needsReconnect: true);

    private async Task RemoveAllDataAsync(CancellationToken ct)
    {
        _db.CalendarEvents.RemoveRange(await _db.CalendarEvents.Where(e => e.Source == EventSource.Import && e.ExternalCalendarId != null).ToListAsync(ct));
        _db.GoogleCalendarSelections.RemoveRange(await _db.GoogleCalendarSelections.ToListAsync(ct));
        _db.GoogleAccounts.RemoveRange(await _db.GoogleAccounts.ToListAsync(ct));
        await _db.SaveChangesAsync(ct);
        _cache.Remove(AccessTokenCacheKey);
    }

    private async Task<string> GetAccessTokenAsync(GoogleAccount account, CancellationToken ct)
    {
        if (_cache.TryGetValue(AccessTokenCacheKey, out string? cached) && cached is not null) return cached;

        string refresh;
        try
        {
            refresh = _protector.Unprotect(account.RefreshToken);
        }
        catch (CryptographicException)
        {
            // Las claves de cifrado cambiaron (p. ej. otro equipo): el token guardado ya no se puede leer.
            throw new GoogleException("No se puede leer la conexión guardada con Google. Vuelve a conectar la cuenta.", needsReconnect: true);
        }

        var tokens = await RequestTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = refresh,
            ["grant_type"] = "refresh_token"
        }, ct);

        var access = tokens["access_token"]?.GetValue<string>() ?? throw new GoogleException("Google no devolvió un token de acceso.");
        CacheAccessToken(access, tokens["expires_in"]);
        return access;
    }

    private void CacheAccessToken(string token, JsonNode? expiresIn)
    {
        var seconds = expiresIn is null ? 3000 : Math.Max(60, expiresIn.GetValue<int>() - 60);
        _cache.Set(AccessTokenCacheKey, token, TimeSpan.FromSeconds(seconds));
    }

    private async Task<JsonNode> RequestTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        HttpResponseMessage response;
        string body;
        try
        {
            response = await _httpFactory.CreateClient("google").PostAsync(_options.TokenUrl, new FormUrlEncodedContent(form), ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (IsNetworkError(ex, ct))
        {
            throw Unreachable(ex);
        }
        using var _ = response;
        var json = TryParse(body);

        if (!response.IsSuccessStatusCode)
        {
            var error = json?["error"]?.GetValue<string>();
            if (error == "invalid_grant")
                throw new GoogleException("La conexión con Google ha caducado o se ha revocado. Vuelve a conectar la cuenta.", needsReconnect: true);
            _logger.LogWarning("Google rechazó la petición de token ({Status}): {Error}", (int)response.StatusCode, error);
            throw new GoogleException($"Google rechazó la conexión ({error ?? response.StatusCode.ToString()}).");
        }

        return json ?? throw new GoogleException("Respuesta de Google no válida.");
    }

    private async Task<JsonNode> GetJsonAsync(string url, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage response;
        string body;
        try
        {
            response = await _httpFactory.CreateClient("google").SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (IsNetworkError(ex, ct))
        {
            throw Unreachable(ex);
        }
        using var _ = response;

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _cache.Remove(AccessTokenCacheKey);
            throw new GoogleException("Google ha rechazado el acceso. Vuelve a conectar la cuenta.", needsReconnect: true);
        }
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google Calendar respondió {Status} a {Url}", (int)response.StatusCode, url.Split('?')[0]);
            throw new GoogleException($"Google Calendar respondió con un error ({(int)response.StatusCode}).");
        }

        return TryParse(body) ?? throw new GoogleException("Respuesta de Google no válida.");
    }

    /// <summary>Fallo de red o tiempo de espera agotado (no una cancelación pedida por el usuario).</summary>
    private static bool IsNetworkError(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private GoogleException Unreachable(Exception ex)
    {
        _logger.LogWarning(ex, "No se pudo contactar con Google.");
        return new GoogleException("No se pudo contactar con Google. Comprueba tu conexión e inténtalo de nuevo.");
    }

    private static JsonNode? TryParse(string body)
    {
        try { return JsonNode.Parse(body); } catch (System.Text.Json.JsonException) { return null; }
    }

    private static string StateKey(string state) => "google-state:" + state;

    /// <summary>Solo se vuelve a direcciones locales (evita redirecciones abiertas); si no, al cliente por defecto.</summary>
    private string SafeReturnUrl(string? returnUrl)
    {
        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            && uri.Host is "localhost" or "127.0.0.1" or "[::1]")
            return uri.GetLeftPart(UriPartial.Path);

        return _options.DefaultClientUrl.TrimEnd('/') + "/agenda";
    }
}
