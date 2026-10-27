using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
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
/// selección de los que se incluyen en Compás, importación de sus eventos y escritura de vuelta
/// (crear, editar y borrar eventos desde Compás). Sin dependencias nuevas: llamadas HTTP directas
/// y cifrado con Data Protection de ASP.NET.
/// </summary>
public class GoogleCalendarService
{
    // calendarlist.readonly para listar calendarios y events para leer y escribir eventos.
    private const string Scopes =
        "openid email https://www.googleapis.com/auth/calendar.calendarlist.readonly https://www.googleapis.com/auth/calendar.events";
    private const string WriteScope = "https://www.googleapis.com/auth/calendar.events";
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

        // Google devuelve los permisos que el usuario ha concedido de verdad (puede desmarcar alguno).
        // Se compara permiso a permiso: "calendar.events.readonly" contiene el texto "calendar.events" pero no permite escribir.
        var granted = (tokens["scope"]?.GetValue<string>() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var canWrite = granted.Contains(WriteScope, StringComparer.Ordinal)
                       || granted.Contains("https://www.googleapis.com/auth/calendar", StringComparer.Ordinal);

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

        account.CanWrite = canWrite;
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
                .Select(c => new GoogleCalendarDto(c.CalendarId, c.Summary, c.ColorHex, c.IsPrimary, c.Selected, c.CanEdit)).ToList();

        var target = account is null ? null : await ResolveTargetCalendarAsync(account, ct);

        return new GoogleStatusDto(IsConfigured, account is not null, account?.Email, account?.LastSyncAt, account?.LastSyncError,
            account?.CanWrite ?? false, target?.CalendarId, calendars);
    }

    /// <summary>Vuelve a leer la lista de calendarios de Google y la fusiona con la selección guardada.</summary>
    public async Task RefreshCalendarsAsync(CancellationToken ct = default)
    {
        var account = await RequireAccountAsync(ct);
        var token = await GetAccessTokenAsync(account, ct);

        var remote = new List<(string Id, string Summary, string? Color, bool Primary, bool CanEdit)>();
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
                var role = item["accessRole"]?.GetValue<string>();
                remote.Add((id, summary, item["backgroundColor"]?.GetValue<string>(), item["primary"]?.GetValue<bool>() ?? false,
                    role is "owner" or "writer"));
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
                    CalendarId = r.Id, Summary = r.Summary, ColorHex = r.Color, IsPrimary = r.Primary,
                    Selected = firstTime && r.Primary, CanEdit = r.CanEdit
                });
            }
            else
            {
                row.Summary = r.Summary;
                row.ColorHex = r.Color;
                row.IsPrimary = r.Primary;
                row.CanEdit = r.CanEdit;
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

    /// <summary>Elige el calendario de Google donde se crean los eventos nuevos de Compás.</summary>
    public async Task SetDefaultCalendarAsync(string calendarId, CancellationToken ct = default)
    {
        var account = await RequireAccountAsync(ct);
        var row = await _db.GoogleCalendarSelections.FirstOrDefaultAsync(c => c.CalendarId == calendarId, ct)
                  ?? throw new GoogleException("Ese calendario no pertenece a la cuenta conectada.");
        if (!row.CanEdit)
            throw new GoogleException("En ese calendario solo tienes permiso de lectura: elige otro.");

        account.DefaultCalendarId = calendarId;
        await _db.SaveChangesAsync(ct);
    }

    // ---------------- Sincronización de eventos ----------------

    /// <summary>
    /// Importa los eventos de los calendarios seleccionados (unas semanas atrás y varios meses adelante)
    /// como eventos fijos. Es idempotente: actualiza los ya vinculados y elimina los que ya no existen en
    /// Google. Los eventos creados en Compás y enviados a Google quedan vinculados y no se duplican.
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

                var from = DateTime.Today.AddDays(-Math.Max(0, _options.SyncPastDays));
                var to = DateTime.Today.AddDays(Math.Max(1, _options.SyncDays));

                var fetched = new Dictionary<(string Calendar, string Event), CalendarEvent>();
                foreach (var cal in selected)
                    foreach (var evt in await FetchEventsAsync(cal, token, from, to, ct))
                        fetched[(evt.ExternalCalendarId!, evt.ExternalId!)] = evt;

                // Todos los eventos vinculados a Google, vengan de Google o se hayan creado en Compás.
                var existing = await _db.CalendarEvents.Where(e => e.ExternalCalendarId != null).ToListAsync(ct);
                var seen = new HashSet<(string, string)>();

                foreach (var e in existing)
                {
                    var key = (e.ExternalCalendarId!, e.ExternalId ?? "");
                    if (!selectedIds.Contains(e.ExternalCalendarId!))
                    {
                        // Calendario que ya no se incluye: lo importado se quita; lo creado en Compás se queda sin vínculo.
                        if (e.Source == EventSource.Import)
                        {
                            _db.CalendarEvents.Remove(e);
                        }
                        else
                        {
                            e.ExternalId = null;
                            e.ExternalCalendarId = null;
                        }
                    }
                    else if (fetched.TryGetValue(key, out var fresh))
                    {
                        e.Title = fresh.Title;
                        e.StartTime = fresh.StartTime;
                        e.EndTime = fresh.EndTime;
                        e.IsAllDay = fresh.IsAllDay;
                        if (e.Source == EventSource.Import) e.Notes = fresh.Notes;
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

    // ---------------- Escritura en Google (crear, editar, borrar) ----------------

    /// <summary>
    /// Crea en Google un evento nuevo de Compás y lo deja vinculado. Devuelve un aviso (no lanza) si la
    /// cuenta no está conectada con permiso de escritura o no hay calendario editable: el evento queda
    /// solo en Compás. Sí lanza si Google falla.
    /// </summary>
    public async Task<string?> PushNewEventAsync(CalendarEvent evt, string? calendarId = null, CancellationToken ct = default)
    {
        if (calendarId == "") return null; // el usuario eligió dejarlo solo en Compás

        var account = await _db.GoogleAccounts.FirstOrDefaultAsync(ct);
        if (account is null) return null; // sin Google conectado no hay nada que avisar

        if (!account.CanWrite)
            return "El evento solo se ha guardado en Compás: la conexión con Google es de solo lectura. Concede el permiso de escritura en la Agenda.";

        GoogleCalendarSelection? target;
        if (calendarId is not null)
        {
            target = await _db.GoogleCalendarSelections.FirstOrDefaultAsync(c => c.CalendarId == calendarId && c.Selected && c.CanEdit, ct);
            if (target is null)
                return "El evento solo se ha guardado en Compás: el calendario elegido no está incluido o es de solo lectura.";
            account.DefaultCalendarId = calendarId; // el último calendario elegido queda como predeterminado
        }
        else
        {
            target = await ResolveTargetCalendarAsync(account, ct);
            if (target is null)
                return "El evento solo se ha guardado en Compás: no hay ningún calendario de Google editable entre los incluidos.";
        }

        var token = await GetAccessTokenAsync(account, ct);
        var created = await SendJsonAsync(HttpMethod.Post, EventsUrl(target.CalendarId), ToGoogleBody(evt, includeNotes: true), token, ct);

        evt.ExternalId = created["id"]?.GetValue<string>();
        evt.ExternalCalendarId = evt.ExternalId is null ? null : target.CalendarId;
        await _db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Envía a Google los cambios de un evento vinculado. No hace nada si el evento no está vinculado.</summary>
    public async Task PushUpdateAsync(CalendarEvent evt, string title, DateTime start, DateTime end, bool isAllDay, string? notes, CancellationToken ct = default)
    {
        if (evt.ExternalId is null || evt.ExternalCalendarId is null) return;
        var (account, calendar) = await RequireWritableAsync(evt.ExternalCalendarId, ct);
        if (calendar is null) return; // su calendario ya no se incluye: el evento es solo local

        var token = await GetAccessTokenAsync(account, ct);
        var changed = new CalendarEvent { Title = title, StartTime = start, EndTime = end, IsAllDay = isAllDay, Notes = notes };
        // Los eventos importados conservan en Google su descripción/ubicación (Compás solo guarda la ubicación).
        var body = ToGoogleBody(changed, includeNotes: evt.Source == EventSource.Manual);
        await SendJsonAsync(new HttpMethod("PATCH"), EventsUrl(evt.ExternalCalendarId, evt.ExternalId), body, token, ct);
    }

    /// <summary>
    /// Mueve un evento vinculado a otro calendario de Google (conserva su id). Ambos calendarios deben
    /// estar incluidos y ser editables. El cambio se guarda en el evento en cuanto Google lo confirma.
    /// </summary>
    public async Task PushMoveAsync(CalendarEvent evt, string destinationCalendarId, CancellationToken ct = default)
    {
        if (evt.ExternalId is null || evt.ExternalCalendarId is null || evt.ExternalCalendarId == destinationCalendarId) return;

        var (account, source) = await RequireWritableAsync(evt.ExternalCalendarId, ct);
        if (source is null) throw new GoogleException("El calendario actual del evento ya no está incluido en Compás: no se puede moverlo.");

        var destination = await _db.GoogleCalendarSelections.FirstOrDefaultAsync(c => c.CalendarId == destinationCalendarId && c.Selected, ct)
                          ?? throw new GoogleException("El calendario elegido no está incluido en Compás.");
        if (!destination.CanEdit)
            throw new GoogleException($"En el calendario «{destination.Summary}» solo tienes permiso de lectura: elige otro.");

        var token = await GetAccessTokenAsync(account, ct);
        var url = QueryHelpers.AddQueryString(EventsUrl(evt.ExternalCalendarId, evt.ExternalId) + "/move",
            new Dictionary<string, string?> { ["destination"] = destinationCalendarId });
        await SendJsonAsync(HttpMethod.Post, url, null, token, ct);

        evt.ExternalCalendarId = destinationCalendarId;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Borra en Google un evento vinculado. No hace nada si no está vinculado.</summary>
    public async Task PushDeleteAsync(CalendarEvent evt, CancellationToken ct = default)
    {
        if (evt.ExternalId is null || evt.ExternalCalendarId is null) return;
        var (account, calendar) = await RequireWritableAsync(evt.ExternalCalendarId, ct);
        if (calendar is null) return;

        var token = await GetAccessTokenAsync(account, ct);
        await SendJsonAsync(HttpMethod.Delete, EventsUrl(evt.ExternalCalendarId, evt.ExternalId), null, token, ct, allowNotFound: true);
    }

    /// <summary>Cuenta y calendario listos para escribir; calendar es null si el calendario ya no está incluido.</summary>
    private async Task<(GoogleAccount Account, GoogleCalendarSelection? Calendar)> RequireWritableAsync(string calendarId, CancellationToken ct)
    {
        var account = await RequireAccountAsync(ct);
        var calendar = await _db.GoogleCalendarSelections.FirstOrDefaultAsync(c => c.CalendarId == calendarId && c.Selected, ct);
        if (calendar is null) return (account, null);

        if (!account.CanWrite)
            throw new GoogleException(
                "Este evento está en Google Calendar y Compás solo tiene permiso de lectura. Pulsa «Dar permiso de escritura» en el panel de Google Calendar de la Agenda.");
        if (!calendar.CanEdit)
            throw new GoogleException($"En el calendario «{calendar.Summary}» solo tienes permiso de lectura, así que no se puede modificar este evento.");
        return (account, calendar);
    }

    /// <summary>Calendario donde se crean los eventos nuevos: el elegido, si no el principal y, si no, el primero editable.</summary>
    private async Task<GoogleCalendarSelection?> ResolveTargetCalendarAsync(GoogleAccount account, CancellationToken ct)
    {
        var editable = await _db.GoogleCalendarSelections.AsNoTracking()
            .Where(c => c.Selected && c.CanEdit).OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Summary).ToListAsync(ct);
        return editable.FirstOrDefault(c => c.CalendarId == account.DefaultCalendarId) ?? editable.FirstOrDefault();
    }

    private string EventsUrl(string calendarId, string? eventId = null) =>
        $"{_options.CalendarApiBase}/calendars/{Uri.EscapeDataString(calendarId)}/events" + (eventId is null ? "" : "/" + Uri.EscapeDataString(eventId));

    private static JsonObject ToGoogleBody(CalendarEvent e, bool includeNotes)
    {
        var body = new JsonObject { ["summary"] = e.Title };
        if (e.IsAllDay)
        {
            var start = e.StartTime.Date;
            var end = e.EndTime.Date > start ? e.EndTime.Date : start.AddDays(1); // el fin de un evento de todo el día es exclusivo
            body["start"] = new JsonObject { ["date"] = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
            body["end"] = new JsonObject { ["date"] = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        }
        else
        {
            // La app trabaja con hora local; con el desfase incluido Google no necesita la zona horaria.
            body["start"] = new JsonObject { ["dateTime"] = new DateTimeOffset(DateTime.SpecifyKind(e.StartTime, DateTimeKind.Local)).ToString("o", CultureInfo.InvariantCulture) };
            body["end"] = new JsonObject { ["dateTime"] = new DateTimeOffset(DateTime.SpecifyKind(e.EndTime, DateTimeKind.Local)).ToString("o", CultureInfo.InvariantCulture) };
        }
        if (includeNotes) body["description"] = e.Notes ?? "";
        return body;
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

    /// <summary>
    /// Convierte un evento de Google en un evento fijo; null si no debe aparecer (cancelado, rechazado...).
    /// Los marcados como "disponible" SÍ se importan: el usuario los usa como bloques de tiempo (p. ej. turnos de trabajo).
    /// </summary>
    private static CalendarEvent? ToCalendarEvent(JsonNode item, string calendarId)
    {
        var id = item["id"]?.GetValue<string>();
        if (id is null) return null;

        if (item["status"]?.GetValue<string>() == "cancelled") return null;
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
            start = DateTime.ParseExact(startDate[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var endDate = item["end"]?["date"]?.GetValue<string>();
            end = endDate is null ? start.AddDays(1) : DateTime.ParseExact(endDate[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture);
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
        // Solo se borran los eventos importados; los creados en Compás se conservan (sin vínculo con Google).
        _db.CalendarEvents.RemoveRange(await _db.CalendarEvents.Where(e => e.Source == EventSource.Import && e.ExternalCalendarId != null).ToListAsync(ct));
        foreach (var linked in await _db.CalendarEvents.Where(e => e.Source != EventSource.Import && e.ExternalCalendarId != null).ToListAsync(ct))
        {
            linked.ExternalId = null;
            linked.ExternalCalendarId = null;
        }
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

    private Task<JsonNode> GetJsonAsync(string url, string accessToken, CancellationToken ct) =>
        SendJsonAsync(HttpMethod.Get, url, null, accessToken, ct);

    /// <summary>Petición autenticada a Google con cuerpo JSON opcional; devuelve el JSON de la respuesta ({} si no hay).</summary>
    private async Task<JsonNode> SendJsonAsync(HttpMethod method, string url, JsonNode? body, string accessToken, CancellationToken ct, bool allowNotFound = false)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        string text;
        try
        {
            response = await _httpFactory.CreateClient("google").SendAsync(request, ct);
            text = await response.Content.ReadAsStringAsync(ct);
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
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("Google Calendar respondió 403 a {Method} {Url}", method, url.Split('?')[0]);
            throw new GoogleException(method == HttpMethod.Get
                ? "Google no permite leer este calendario."
                : "Google no permite modificar este evento: falta el permiso de escritura o el calendario es de solo lectura.", needsReconnect: method != HttpMethod.Get);
        }
        if (allowNotFound && response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
            return new JsonObject();
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google Calendar respondió {Status} a {Method} {Url}", (int)response.StatusCode, method, url.Split('?')[0]);
            throw new GoogleException($"Google Calendar respondió con un error ({(int)response.StatusCode}).");
        }

        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        return TryParse(text) ?? throw new GoogleException("Respuesta de Google no válida.");
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
