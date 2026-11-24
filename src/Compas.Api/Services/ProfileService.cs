using System.Globalization;
using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Services;

/// <summary>Perfil, medidas corporales y objetivos de fuerza: lógica compartida por la API REST y el conector MCP.</summary>
public class ProfileService
{
    private static readonly string[] Sexes = ["male", "female"];
    private static readonly string[] Activity = ["sedentary", "light", "moderate", "active", "very_active"];

    private readonly CompasDbContext _db;

    public ProfileService(CompasDbContext db) => _db = db;

    private static string? D(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string text, string field) =>
        WorkoutService.TryParseDate(text, out var d) ? d : throw new ArgumentException($"'{field}' debe tener el formato yyyy-MM-dd.");

    private static void CheckRange(double? v, double min, double max, string field)
    {
        if (v is { } x && (double.IsNaN(x) || x < min || x > max)) throw new ArgumentException($"'{field}' debe estar entre {min} y {max}.");
    }

    private static UserProfileDto ToDto(UserProfile p) => new(
        p.HeightCm, D(p.BirthDate), p.Sex, p.ActivityLevel, p.Phase.ToString(), D(p.PhaseStartDate), p.TargetWeightKg, D(p.TargetDate), p.Notes);

    private static BodyMeasurementDto ToDto(BodyMeasurement m) => new(
        m.Id, D(m.Date)!, m.WeightKg, m.BodyFatPct, m.WaistCm, m.ChestCm, m.HipCm, m.ArmCm, m.ThighCm, m.Notes);

    private static LiftGoalDto ToDto(LiftGoal g) => new(
        g.Id, g.ExerciseName, g.HevyExerciseTemplateId, g.TargetWeightKg, g.TargetReps, D(g.TargetDate), g.Notes, g.Achieved);

    private static TrainingSlotDto ToDto(TrainingSlot t) => new(t.Id, t.DayOfWeek, t.StartTime, t.DurationMinutes, t.Label);

    private static ExerciseRuleDto ToDto(ExerciseRule r) => new(r.Id, r.ExerciseName, r.RepMin, r.RepMax, r.IncrementKg, r.Notes);

    /// <summary>Perfil (vacío si aún no se ha rellenado), últimas <paramref name="measurementCount"/> medidas (la más reciente primero) y objetivos.</summary>
    public async Task<ProfileOverviewDto> GetAsync(int measurementCount, CancellationToken ct)
    {
        var profile = await _db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(ct) ?? new UserProfile();
        var measurements = await _db.BodyMeasurements.AsNoTracking()
            .OrderByDescending(m => m.Date).Take(Math.Clamp(measurementCount, 1, 200)).ToListAsync(ct);
        var goals = await _db.LiftGoals.AsNoTracking().OrderBy(g => g.ExerciseName).ToListAsync(ct);
        var slots = await _db.TrainingSlots.AsNoTracking().OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime).ToListAsync(ct);
        var rules = await _db.ExerciseRules.AsNoTracking().OrderBy(r => r.ExerciseName).ToListAsync(ct);
        return new ProfileOverviewDto(ToDto(profile), measurements.Select(ToDto).ToList(), goals.Select(ToDto).ToList(),
            slots.Select(ToDto).ToList(), rules.Select(ToDto).ToList());
    }

    public async Task<UserProfileDto> UpdateProfileAsync(UpdateProfileInput input, CancellationToken ct)
    {
        CheckRange(input.HeightCm, 50, 260, "heightCm");
        CheckRange(input.TargetWeightKg, 25, 400, "targetWeightKg");
        if (input.Sex is { Length: > 0 } sex && !Sexes.Contains(sex)) throw new ArgumentException("'sex' debe ser male o female.");
        if (input.ActivityLevel is { Length: > 0 } act && !Activity.Contains(act))
            throw new ArgumentException("'activityLevel' debe ser sedentary, light, moderate, active o very_active.");
        NutritionPhase? phase = null;
        if (input.Phase is { Length: > 0 } ph)
            phase = Enum.TryParse<NutritionPhase>(ph, true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed : throw new ArgumentException("'phase' debe ser Bulk, Maintenance o Cut.");

        var p = await _db.UserProfiles.FirstOrDefaultAsync(ct);
        var isNew = p is null;
        p ??= new UserProfile();

        if (input.HeightCm is { } h) p.HeightCm = h;
        if (input.BirthDate is { Length: > 0 } bd) p.BirthDate = ParseDate(bd, "birthDate");
        if (input.Sex is { Length: > 0 } s) p.Sex = s;
        if (input.ActivityLevel is { Length: > 0 } a) p.ActivityLevel = a;
        if (input.TargetWeightKg is { } tw) p.TargetWeightKg = tw;
        if (input.TargetDate is { Length: > 0 } td) p.TargetDate = ParseDate(td, "targetDate");
        if (input.Notes is not null) p.Notes = input.Notes;
        if (input.PhaseStartDate is { Length: > 0 } ps) p.PhaseStartDate = ParseDate(ps, "phaseStartDate");
        if (phase is { } newPhase)
        {
            // Al cambiar de fase, si no se indica otra fecha de inicio, empieza hoy.
            if (newPhase != p.Phase && input.PhaseStartDate is not { Length: > 0 }) p.PhaseStartDate = DateTime.UtcNow.Date;
            p.Phase = newPhase;
        }
        p.UpdatedAt = DateTime.UtcNow;
        if (isNew) _db.UserProfiles.Add(p);
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<BodyMeasurementDto> LogMeasurementAsync(BodyMeasurementInput input, CancellationToken ct)
    {
        var date = ParseDate(input.Date, "date");
        CheckRange(input.WeightKg, 25, 400, "weightKg");
        CheckRange(input.BodyFatPct, 1, 70, "bodyFatPct");
        foreach (var (v, name) in new[] { (input.WaistCm, "waistCm"), (input.ChestCm, "chestCm"), (input.HipCm, "hipCm"), (input.ArmCm, "armCm"), (input.ThighCm, "thighCm") })
            CheckRange(v, 10, 250, name);
        if (input.WeightKg is null && input.BodyFatPct is null && input.WaistCm is null && input.ChestCm is null
            && input.HipCm is null && input.ArmCm is null && input.ThighCm is null)
            throw new ArgumentException("Indica al menos un valor (peso o alguna medida).");

        var m = await _db.BodyMeasurements.FirstOrDefaultAsync(x => x.Date == date, ct);
        var isNew = m is null;
        m ??= new BodyMeasurement { Date = date };
        if (input.WeightKg is { } w) m.WeightKg = w;
        if (input.BodyFatPct is { } bf) m.BodyFatPct = bf;
        if (input.WaistCm is { } wa) m.WaistCm = wa;
        if (input.ChestCm is { } c) m.ChestCm = c;
        if (input.HipCm is { } hi) m.HipCm = hi;
        if (input.ArmCm is { } ar) m.ArmCm = ar;
        if (input.ThighCm is { } th) m.ThighCm = th;
        if (input.Notes is not null) m.Notes = input.Notes;
        if (isNew) _db.BodyMeasurements.Add(m);
        await _db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    public async Task<bool> DeleteMeasurementAsync(int id, CancellationToken ct)
    {
        var m = await _db.BodyMeasurements.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (m is null) return false;
        _db.BodyMeasurements.Remove(m);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<LiftGoalDto> UpsertLiftGoalAsync(LiftGoalInput input, CancellationToken ct)
    {
        var name = input.ExerciseName?.Trim();
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("El nombre del ejercicio es obligatorio.");
        if (name.Length > 200) throw new ArgumentException("El nombre del ejercicio es demasiado largo.");
        CheckRange(input.TargetWeightKg, 0, 1000, "targetWeightKg");
        if (input.TargetReps is < 1 or > 100) throw new ArgumentException("'targetReps' debe estar entre 1 y 100.");

        var lower = name.ToLower();
        var g = await _db.LiftGoals.FirstOrDefaultAsync(x => x.ExerciseName.ToLower() == lower, ct);
        var isNew = g is null;
        g ??= new LiftGoal();
        g.ExerciseName = name;
        g.TargetWeightKg = input.TargetWeightKg;
        if (input.TargetReps is { } reps) g.TargetReps = reps;
        if (input.HevyExerciseTemplateId is not null) g.HevyExerciseTemplateId = input.HevyExerciseTemplateId.Length == 0 ? null : input.HevyExerciseTemplateId;
        if (input.TargetDate is not null) g.TargetDate = input.TargetDate.Length == 0 ? null : ParseDate(input.TargetDate, "targetDate");
        if (input.Notes is not null) g.Notes = input.Notes;
        if (input.Achieved is { } ach) g.Achieved = ach;
        if (isNew) _db.LiftGoals.Add(g);
        await _db.SaveChangesAsync(ct);
        return ToDto(g);
    }

    public async Task<bool> DeleteLiftGoalAsync(int id, CancellationToken ct)
    {
        var g = await _db.LiftGoals.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (g is null) return false;
        _db.LiftGoals.Remove(g);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------- Huecos semanales de entreno ----------------

    /// <summary>Hora "HH:mm" válida (00:00-23:59).</summary>
    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>Sustituye por completo los huecos semanales de entreno (una lista vacía los borra todos).</summary>
    public async Task<List<TrainingSlotDto>> SetTrainingSlotsAsync(List<TrainingSlotInput> inputs, CancellationToken ct)
    {
        if (inputs.Count > 21) throw new ArgumentException("Demasiados huecos (máximo 21).");
        var slots = new List<TrainingSlot>();
        foreach (var i in inputs)
        {
            if (i.DayOfWeek is < 1 or > 7) throw new ArgumentException("'dayOfWeek' debe estar entre 1 (lunes) y 7 (domingo).");
            if (!TryParseTime(i.StartTime, out var start)) throw new ArgumentException("'startTime' debe tener el formato HH:mm.");
            var duration = i.DurationMinutes ?? 75;
            if (duration is < 20 or > 240) throw new ArgumentException("'durationMinutes' debe estar entre 20 y 240.");
            var label = i.Label?.Trim();
            if (label is { Length: > 50 }) throw new ArgumentException("'label' es demasiado largo (máximo 50).");
            slots.Add(new TrainingSlot
            {
                DayOfWeek = i.DayOfWeek,
                StartTime = start.ToString("HH:mm", CultureInfo.InvariantCulture),
                DurationMinutes = duration,
                Label = string.IsNullOrEmpty(label) ? null : label
            });
        }

        _db.TrainingSlots.RemoveRange(await _db.TrainingSlots.ToListAsync(ct));
        _db.TrainingSlots.AddRange(slots);
        await _db.SaveChangesAsync(ct);
        return slots.OrderBy(t => t.DayOfWeek).ThenBy(t => t.StartTime).Select(ToDto).ToList();
    }

    // ---------------- Reglas de progresión por ejercicio ----------------

    /// <summary>Crea o actualiza varias reglas de una vez; valida todas antes de guardar ninguna.</summary>
    public async Task<List<ExerciseRuleDto>> UpsertExerciseRulesAsync(List<ExerciseRuleInput> inputs, CancellationToken ct)
    {
        if (inputs.Count is 0 or > 200) throw new ArgumentException("Envía entre 1 y 200 reglas.");
        foreach (var i in inputs)
        {
            var name = i.ExerciseName?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 200) throw new ArgumentException("Cada regla necesita un nombre de ejercicio (máximo 200 caracteres).");
            if (i.RepMin is < 1 or > 100 || i.RepMax is < 1 or > 100 || i.RepMin > i.RepMax)
                throw new ArgumentException($"'{name}': repMin y repMax deben estar entre 1 y 100, con repMin <= repMax.");
            CheckRange(i.IncrementKg, 0, 50, $"incrementKg de '{name}'");
        }

        var existing = await _db.ExerciseRules.ToListAsync(ct);
        var result = new List<ExerciseRule>();
        foreach (var i in inputs)
        {
            var name = i.ExerciseName.Trim();
            var rule = existing.FirstOrDefault(r => string.Equals(r.ExerciseName, name, StringComparison.OrdinalIgnoreCase));
            if (rule is null)
            {
                rule = new ExerciseRule();
                existing.Add(rule);
                _db.ExerciseRules.Add(rule);
            }
            rule.ExerciseName = name;
            rule.RepMin = i.RepMin;
            rule.RepMax = i.RepMax;
            rule.IncrementKg = i.IncrementKg;
            if (i.Notes is not null) rule.Notes = i.Notes;
            result.Add(rule);
        }
        await _db.SaveChangesAsync(ct);
        return result.Select(ToDto).ToList();
    }

    public async Task<bool> DeleteExerciseRuleAsync(int id, CancellationToken ct)
    {
        var r = await _db.ExerciseRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return false;
        _db.ExerciseRules.Remove(r);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
