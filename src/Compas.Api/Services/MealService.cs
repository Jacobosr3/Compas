using System.Globalization;
using System.Text.Json;
using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Services;

/// <summary>Comidas con foto: lógica compartida por la API REST (la app) y el conector MCP (Claude).</summary>
public class MealService
{
    public const int MaxPhotoBytes = 3 * 1024 * 1024;
    private static readonly string[] PhotoTypes = ["image/jpeg", "image/png", "image/webp"];
    private static readonly string[] MealTypes = ["breakfast", "lunch", "snack", "dinner", "other"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly CompasDbContext _db;

    public MealService(CompasDbContext db) => _db = db;

    private static string D(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static List<MealItem> ParseItems(string json) => JsonSerializer.Deserialize<List<MealItem>>(json, Json) ?? [];

    private static MealDto ToDto(MealLog m, bool hasPhoto) => new(
        m.Id, D(m.Date), m.Time, m.MealType, m.Note, hasPhoto, m.Status.ToString(), ParseItems(m.ItemsJson),
        m.TotalCalories, m.ProteinG, m.CarbsG, m.FatG, m.AnalysisNotes, m.CreatedAt, m.AnalyzedAt, m.ConfirmedAt);

    private static string? CleanMealType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        var t = type.Trim().ToLowerInvariant();
        return MealTypes.Contains(t) ? t : throw new ArgumentException("'mealType' debe ser breakfast, lunch, snack, dinner u other.");
    }

    private static string? CleanTime(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return null;
        return ProfileService.TryParseTime(time, out _) ? time : throw new ArgumentException("La hora debe tener el formato HH:mm.");
    }

    private static DateTime ParseDate(string? text, string field) =>
        WorkoutService.TryParseDate(text, out var d) ? d : throw new ArgumentException($"'{field}' debe tener el formato yyyy-MM-dd.");

    private static void CheckMacro(double? v, string what)
    {
        if (v is { } x && (double.IsNaN(x) || x < 0 || x > 1000)) throw new ArgumentException($"{what} debe estar entre 0 y 1000 g.");
    }

    /// <summary>Valida los alimentos y los devuelve limpios (nombre recortado, cantidades sin espacios sobrantes).</summary>
    private static List<MealItem> CleanItems(List<MealItemInput>? inputs)
    {
        if (inputs is null || inputs.Count == 0) throw new ArgumentException("Indica al menos un alimento.");
        if (inputs.Count > 40) throw new ArgumentException("Demasiados alimentos (máximo 40).");
        var items = new List<MealItem>();
        foreach (var i in inputs)
        {
            var name = i.Name?.Trim();
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Cada alimento necesita un nombre.");
            if (name.Length > 120) throw new ArgumentException($"El nombre '{name[..20]}…' es demasiado largo (máximo 120).");
            if (double.IsNaN(i.Calories) || i.Calories < 0 || i.Calories > 5000)
                throw new ArgumentException($"Las calorías de '{name}' deben estar entre 0 y 5000.");
            CheckMacro(i.ProteinG, $"Las proteínas de '{name}'");
            CheckMacro(i.CarbsG, $"Los hidratos de '{name}'");
            CheckMacro(i.FatG, $"Las grasas de '{name}'");
            var qty = i.Quantity?.Trim();
            if (qty?.Length > 80) throw new ArgumentException($"La cantidad de '{name}' es demasiado larga (máximo 80).");
            items.Add(new MealItem(name, string.IsNullOrEmpty(qty) ? null : qty, Math.Round(i.Calories, 1), i.ProteinG, i.CarbsG, i.FatG));
        }
        return items;
    }

    private static void ApplyItems(MealLog m, List<MealItem> items)
    {
        m.ItemsJson = JsonSerializer.Serialize(items, Json);
        m.TotalCalories = Math.Round(items.Sum(i => i.Calories), 1);
        // Un macro solo se suma si algún alimento lo informa; así se distingue "0 g" de "sin dato".
        m.ProteinG = items.Any(i => i.ProteinG is not null) ? Math.Round(items.Sum(i => i.ProteinG ?? 0), 1) : null;
        m.CarbsG = items.Any(i => i.CarbsG is not null) ? Math.Round(items.Sum(i => i.CarbsG ?? 0), 1) : null;
        m.FatG = items.Any(i => i.FatG is not null) ? Math.Round(items.Sum(i => i.FatG ?? 0), 1) : null;
    }

    public async Task<MealDto> CreateAsync(MealCreateInput input, CancellationToken ct)
    {
        var date = ParseDate(input.Date, "date");
        var note = input.Note?.Trim();
        if (note?.Length > 500) throw new ArgumentException("La nota es demasiado larga (máximo 500).");

        byte[]? photo = null;
        string? contentType = null;
        if (!string.IsNullOrWhiteSpace(input.PhotoBase64))
        {
            contentType = input.PhotoContentType?.Trim().ToLowerInvariant();
            if (contentType is null || !PhotoTypes.Contains(contentType)) throw new ArgumentException("La foto debe ser JPEG, PNG o WebP.");
            var b64 = input.PhotoBase64;
            var comma = b64.IndexOf(',');
            if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0) b64 = b64[(comma + 1)..];
            if (b64.Length > MaxPhotoBytes * 4L / 3 + 8) throw new ArgumentException("La foto pesa más de 3 MB.");
            try { photo = Convert.FromBase64String(b64); }
            catch (FormatException) { throw new ArgumentException("La foto no es base64 válido."); }
            if (photo.Length > MaxPhotoBytes) throw new ArgumentException("La foto pesa más de 3 MB.");
            if (photo.Length == 0) photo = null;
        }
        if (photo is null && string.IsNullOrEmpty(note)) throw new ArgumentException("Añade una foto o una nota.");

        var m = new MealLog
        {
            Date = date,
            Time = CleanTime(input.Time),
            MealType = CleanMealType(input.MealType),
            Note = string.IsNullOrEmpty(note) ? null : note,
            Photo = photo,
            PhotoContentType = photo is null ? null : contentType
        };
        _db.MealLogs.Add(m);
        await _db.SaveChangesAsync(ct);
        return ToDto(m, photo is not null);
    }

    public async Task<List<MealDto>> ListAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var q = _db.MealLogs.AsNoTracking().AsQueryable();
        if (from is { } f) q = q.Where(m => m.Date >= f);
        if (to is { } t) q = q.Where(m => m.Date <= t);
        // Se proyecta sin cargar los bytes de la foto.
        var rows = await q.OrderByDescending(m => m.Date).ThenByDescending(m => m.Time).ThenByDescending(m => m.Id)
            .Select(m => new
            {
                Meal = new MealLog
                {
                    Id = m.Id, Date = m.Date, Time = m.Time, MealType = m.MealType, Note = m.Note, Status = m.Status,
                    ItemsJson = m.ItemsJson, TotalCalories = m.TotalCalories, ProteinG = m.ProteinG, CarbsG = m.CarbsG, FatG = m.FatG,
                    AnalysisNotes = m.AnalysisNotes, CreatedAt = m.CreatedAt, AnalyzedAt = m.AnalyzedAt, ConfirmedAt = m.ConfirmedAt
                },
                HasPhoto = m.Photo != null
            })
            .Take(500)
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r.Meal, r.HasPhoto)).ToList();
    }

    public async Task<(byte[] Data, string ContentType)?> GetPhotoAsync(int id, CancellationToken ct)
    {
        var m = await _db.MealLogs.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Photo, x.PhotoContentType }).FirstOrDefaultAsync(ct);
        return m?.Photo is { } data ? (data, m.PhotoContentType ?? "image/jpeg") : null;
    }

    /// <summary>Comidas que esperan análisis (las más antiguas primero), con su foto, para el conector MCP.</summary>
    public async Task<List<(MealDto Meal, byte[]? Photo, string? ContentType)>> PendingAsync(int limit, CancellationToken ct)
    {
        var rows = await _db.MealLogs.AsNoTracking().Where(m => m.Status == MealStatus.Pending)
            .OrderBy(m => m.Date).ThenBy(m => m.Id).Take(Math.Clamp(limit, 1, 10)).ToListAsync(ct);
        return rows.Select(m => (ToDto(m, m.Photo is not null), m.Photo, m.PhotoContentType)).ToList();
    }

    /// <summary>Guarda la propuesta de Claude. Solo para comidas no confirmadas; deja la comida en "Proposed" (nunca la confirma).</summary>
    public async Task<MealDto> SubmitAnalysisAsync(MealAnalysisInput input, CancellationToken ct)
    {
        var m = await _db.MealLogs.FirstOrDefaultAsync(x => x.Id == input.Id, ct) ?? throw new ArgumentException("No existe esa comida.");
        if (m.Status == MealStatus.Confirmed) throw new InvalidOperationException("Esa comida ya está confirmada por el usuario y no se puede modificar.");
        var items = CleanItems(input.Items);
        var notes = input.Notes?.Trim();
        if (notes?.Length > 1000) throw new ArgumentException("Las notas son demasiado largas (máximo 1000).");
        var type = CleanMealType(input.MealType);

        ApplyItems(m, items);
        if (type is not null) m.MealType = type;
        m.AnalysisNotes = string.IsNullOrEmpty(notes) ? null : notes;
        m.Status = MealStatus.Proposed;
        m.AnalyzedAt = m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(m, m.Photo is not null);
    }

    /// <summary>El usuario revisa la comida (puede corregir alimentos, tipo, fecha, hora o nota) y la confirma.</summary>
    public async Task<MealDto?> ConfirmAsync(int id, MealConfirmInput input, CancellationToken ct)
    {
        var m = await _db.MealLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return null;
        if (input.Items is not null) ApplyItems(m, CleanItems(input.Items));
        else if (m.Status == MealStatus.Pending) throw new ArgumentException("Aún no hay alimentos: espera al análisis o añádelos a mano.");
        if (input.MealType is not null) m.MealType = CleanMealType(input.MealType);
        if (!string.IsNullOrEmpty(input.Date)) m.Date = ParseDate(input.Date, "date");
        if (input.Time is not null) m.Time = CleanTime(input.Time);
        if (input.Note is not null)
        {
            var note = input.Note.Trim();
            if (note.Length > 500) throw new ArgumentException("La nota es demasiado larga (máximo 500).");
            m.Note = note.Length == 0 ? null : note;
        }
        m.Status = MealStatus.Confirmed;
        m.ConfirmedAt = m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(m, m.Photo is not null);
    }

    /// <summary>Vuelve a poner la comida en "Pending" para que Claude la analice de nuevo (borra la propuesta o la confirmación).</summary>
    public async Task<MealDto?> ReanalyzeAsync(int id, CancellationToken ct)
    {
        var m = await _db.MealLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return null;
        if (m.Photo is null) throw new ArgumentException("Sin foto no hay nada que analizar: edita los alimentos a mano.");
        m.Status = MealStatus.Pending;
        m.ItemsJson = "[]";
        m.TotalCalories = 0;
        m.ProteinG = m.CarbsG = m.FatG = null;
        m.AnalysisNotes = null;
        m.AnalyzedAt = m.ConfirmedAt = null;
        m.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(m, true);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var m = await _db.MealLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return false;
        _db.MealLogs.Remove(m);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------- Resumen nutricional ----------------

    private static double ActivityFactor(string? level) => level switch
    {
        "sedentary" => 1.2,
        "light" => 1.375,
        "moderate" => 1.55,
        "active" => 1.725,
        "very_active" => 1.9,
        _ => 1.2
    };

    /// <summary>Superávit/déficit sobre el mantenimiento según la fase: volumen +300 kcal, definición −500 kcal.</summary>
    private static double PhaseDelta(NutritionPhase phase) => phase switch
    {
        NutritionPhase.Bulk => 300,
        NutritionPhase.Cut => -500,
        _ => 0
    };

    private async Task<NutritionTargetsDto> TargetsAsync(DateTime today, CancellationToken ct)
    {
        var p = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var phase = p?.Phase ?? NutritionPhase.Maintenance;
        var weight = (await _db.BodyMeasurements.AsNoTracking().Where(m => m.WeightKg != null)
            .OrderByDescending(m => m.Date).Select(m => m.WeightKg).FirstOrDefaultAsync(ct));

        var missing = new List<string>();
        if (p?.HeightCm is null) missing.Add("altura");
        if (p?.BirthDate is null) missing.Add("fecha de nacimiento");
        if (p?.Sex is null) missing.Add("sexo");
        if (weight is null) missing.Add("peso (registra una medida)");
        if (missing.Count > 0)
            return new NutritionTargetsDto(weight, null, null, null, phase.ToString(), "Faltan datos en el perfil: " + string.Join(", ", missing) + ".");

        var birth = p!.BirthDate!.Value.Date;
        var age = today.Year - birth.Year;
        if (birth > today.AddYears(-age)) age--;
        var bmr = 10 * weight!.Value + 6.25 * p.HeightCm!.Value - 5 * age + (p.Sex == "male" ? 5 : -161);
        var maintenance = bmr * ActivityFactor(p.ActivityLevel);
        string? note = p.ActivityLevel is null ? "Sin nivel de actividad en el perfil: se asume sedentario (x1,2), así que el mantenimiento puede estar infravalorado." : null;
        return new NutritionTargetsDto(weight, Math.Round(bmr), Math.Round(maintenance), Math.Round(maintenance + PhaseDelta(phase)), phase.ToString(), note);
    }

    /// <summary>Totales por día de las comidas CONFIRMADAS entre dos fechas (ambas incluidas) y objetivo calórico estimado.</summary>
    public async Task<NutritionSummaryDto> SummaryAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var meals = await _db.MealLogs.AsNoTracking()
            .Where(m => m.Status == MealStatus.Confirmed && m.Date >= from && m.Date <= to)
            .Select(m => new { m.Date, m.TotalCalories, m.ProteinG, m.CarbsG, m.FatG })
            .ToListAsync(ct);
        var days = meals.GroupBy(m => m.Date.Date).OrderBy(g => g.Key)
            .Select(g => new NutritionDayDto(D(g.Key), g.Count(), Math.Round(g.Sum(x => x.TotalCalories)),
                Math.Round(g.Sum(x => x.ProteinG ?? 0)), Math.Round(g.Sum(x => x.CarbsG ?? 0)), Math.Round(g.Sum(x => x.FatG ?? 0))))
            .ToList();
        var avg = days.Count == 0 ? 0 : Math.Round(days.Average(d => d.Calories));

        var pending = await _db.MealLogs.CountAsync(m => m.Status == MealStatus.Pending, ct);
        var proposed = await _db.MealLogs.CountAsync(m => m.Status == MealStatus.Proposed, ct);
        return new NutritionSummaryDto(D(from), D(to), days, avg, pending, proposed, await TargetsAsync(DateTime.Today, ct));
    }
}
