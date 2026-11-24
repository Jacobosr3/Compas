using System.Text.Json;
using System.Text.Json.Nodes;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Compas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Compas.Api.Controllers;

/// <summary>
/// Conector MCP (Model Context Protocol, transporte HTTP "streamable" sin streaming): permite que una automatización de
/// Claude escriba y lea los entrenamientos de Compás. No usa la cookie de sesión: se autentica con un token personal,
/// en la cabecera "Authorization: Bearer cmp_..." o, para clientes que no admiten cabeceras, en la ruta (/mcp/cmp_...).
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("mcp")]
public class McpController : ControllerBase
{
    private const string ProtocolVersion = "2025-03-26";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApiTokenService _tokens;
    private readonly WorkoutService _workouts;
    private readonly ProfileService _profile;
    private readonly CurrentUser _currentUser;

    public McpController(ApiTokenService tokens, WorkoutService workouts, ProfileService profile, CurrentUser currentUser)
    {
        _tokens = tokens;
        _workouts = workouts;
        _profile = profile;
        _currentUser = currentUser;
    }

    [HttpGet("mcp")]
    [HttpGet("mcp/{token}")]
    public IActionResult Get() => StatusCode(StatusCodes.Status405MethodNotAllowed); // no hay canal de notificaciones del servidor

    [HttpDelete("mcp")]
    [HttpDelete("mcp/{token}")]
    public IActionResult Delete() => Ok(); // no hay sesiones que cerrar

    [HttpPost("mcp")]
    [HttpPost("mcp/{token}")]
    public async Task<IActionResult> Post([FromBody] JsonNode? body, string? token, CancellationToken ct)
    {
        var bearer = Request.Headers.Authorization.ToString();
        if (bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) token = bearer["Bearer ".Length..].Trim();

        var userId = await _tokens.ResolveUserAsync(token, ct);
        if (userId is null)
        {
            Response.Headers.WWWAuthenticate = "Bearer";
            return Unauthorized();
        }
        _currentUser.Set(userId.Value);

        if (body is JsonArray) return BadRequest(Error(null, -32600, "No se admiten lotes JSON-RPC."));
        if (body is not JsonObject msg) return BadRequest(Error(null, -32700, "Cuerpo JSON no válido."));

        var id = msg["id"]?.DeepClone();
        var method = msg["method"]?.GetValue<string>();
        if (id is null) return Accepted(); // notificación (p. ej. notifications/initialized): sin respuesta

        try
        {
            JsonNode result = method switch
            {
                "initialize" => new JsonObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                    ["serverInfo"] = new JsonObject { ["name"] = "compas", ["version"] = "1.0.0" },
                    ["instructions"] = "Conector de Compás. Antes de programar, lee get_profile (fase volumen/mantenimiento/definición, peso objetivo, "
                                       + "medidas, objetivos de fuerza, huecos semanales de entreno y reglas de progresión por ejercicio) y list_calendar_events "
                                       + "(bloques ocupados) para no pisar nada. upsert_workout_session programa un entreno con sus pesos objetivo "
                                       + "(idempotente con source + externalId); con startTime también lo coloca en el calendario y en Google Calendar. "
                                       + "Las horas son siempre las locales del usuario (Europe/Madrid)."
                },
                "ping" => new JsonObject(),
                "tools/list" => new JsonObject { ["tools"] = Tools() },
                "tools/call" => await CallToolAsync(msg["params"] as JsonObject, ct),
                _ => throw new McpException(-32601, $"Método no soportado: {method}")
            };
            return Ok(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result });
        }
        catch (McpException ex)
        {
            return Ok(Error(id, ex.Code, ex.Message));
        }
    }

    private static JsonObject Error(JsonNode? id, int code, string message) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id,
        ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
    };

    private sealed class McpException(int code, string message) : Exception(message)
    {
        public int Code { get; } = code;
    }

    // ---------------- Herramientas ----------------

    private static JsonArray Tools() => new()
    {
        Tool("upsert_workout_session",
            "Crea o actualiza una sesión de entrenamiento programada con sus ejercicios, series y pesos objetivo. "
            + "Si ya existe una con el mismo source + externalId (o, sin ellos, mismo día y título) la sustituye entera.",
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("title", "date", "exercises"),
                ["properties"] = new JsonObject
                {
                    ["title"] = new JsonObject { ["type"] = "string", ["description"] = "Nombre de la sesión, p. ej. \"Push\"." },
                    ["date"] = new JsonObject { ["type"] = "string", ["description"] = "Día programado, formato yyyy-MM-dd." },
                    ["startTime"] = new JsonObject { ["type"] = "string", ["description"] = "Hora de inicio HH:mm (hora local). Si se indica, la sesión se coloca en el calendario y en Google Calendar; si se omite, se quita su evento." },
                    ["durationMinutes"] = new JsonObject { ["type"] = "integer", ["description"] = "Duración en minutos (15-300, por defecto 75); solo con startTime." },
                    ["notes"] = new JsonObject { ["type"] = "string" },
                    ["source"] = new JsonObject { ["type"] = "string", ["description"] = "Origen, p. ej. \"hevy\"." },
                    ["externalId"] = new JsonObject { ["type"] = "string", ["description"] = "Id estable en el origen (p. ej. id de rutina + fecha) para no duplicar." },
                    ["exercises"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["required"] = new JsonArray("name", "sets"),
                            ["properties"] = new JsonObject
                            {
                                ["name"] = new JsonObject { ["type"] = "string" },
                                ["notes"] = new JsonObject { ["type"] = "string" },
                                ["sets"] = new JsonObject
                                {
                                    ["type"] = "array",
                                    ["items"] = new JsonObject
                                    {
                                        ["type"] = "object",
                                        ["required"] = new JsonArray("type"),
                                        ["properties"] = new JsonObject
                                        {
                                            ["type"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("warmup", "normal", "dropset", "failure") },
                                            ["weightKg"] = new JsonObject { ["type"] = "number", ["description"] = "Peso objetivo en kg; omitir si es peso corporal." },
                                            ["reps"] = new JsonObject { ["type"] = "integer", ["description"] = "Repeticiones objetivo." }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }),
        Tool("list_workout_sessions", "Lista las sesiones programadas, opcionalmente entre dos fechas (yyyy-MM-dd, ambas incluidas).",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["from"] = new JsonObject { ["type"] = "string" },
                    ["to"] = new JsonObject { ["type"] = "string" }
                }
            }),
        Tool("list_calendar_events",
            "Bloques ocupados del calendario (incluye Google Calendar) entre dos días, ambos incluidos, en hora local. Úsalo para elegir huecos libres.",
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("from", "to"),
                ["properties"] = new JsonObject
                {
                    ["from"] = new JsonObject { ["type"] = "string", ["description"] = "yyyy-MM-dd" },
                    ["to"] = new JsonObject { ["type"] = "string", ["description"] = "yyyy-MM-dd" }
                }
            }),
        Tool("delete_workout_session", "Borra una sesión programada por su id (y su evento de calendario).",
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("id"),
                ["properties"] = new JsonObject { ["id"] = new JsonObject { ["type"] = "integer" } }
            }),
        Tool("get_profile",
            "Perfil y objetivos del usuario: altura, edad, sexo, nivel de actividad, fase actual (Bulk = volumen, Maintenance = mantenimiento, "
            + "Cut = definición), peso objetivo y fecha, últimas medidas corporales (la más reciente primero) y objetivos de fuerza.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["measurements"] = new JsonObject { ["type"] = "integer", ["description"] = "Cuántas medidas recientes devolver (por defecto 30)." }
                }
            }),
        Tool("update_profile", "Actualiza el perfil. Solo se cambian los campos que se envían.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["heightCm"] = new JsonObject { ["type"] = "number" },
                    ["birthDate"] = new JsonObject { ["type"] = "string", ["description"] = "yyyy-MM-dd" },
                    ["sex"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("male", "female") },
                    ["activityLevel"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("sedentary", "light", "moderate", "active", "very_active") },
                    ["phase"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("Bulk", "Maintenance", "Cut") },
                    ["phaseStartDate"] = new JsonObject { ["type"] = "string", ["description"] = "yyyy-MM-dd; al cambiar de fase, por defecto hoy." },
                    ["targetWeightKg"] = new JsonObject { ["type"] = "number" },
                    ["targetDate"] = new JsonObject { ["type"] = "string", ["description"] = "yyyy-MM-dd" },
                    ["notes"] = new JsonObject { ["type"] = "string" }
                }
            }),
        Tool("log_body_measurement", "Registra peso y/o medidas de un día; si ya hay registro de esa fecha, actualiza los campos enviados.",
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("date"),
                ["properties"] = new JsonObject
                {
                    ["date"] = new JsonObject { ["type"] = "string", ["description"] = "yyyy-MM-dd" },
                    ["weightKg"] = new JsonObject { ["type"] = "number" },
                    ["bodyFatPct"] = new JsonObject { ["type"] = "number" },
                    ["waistCm"] = new JsonObject { ["type"] = "number" },
                    ["chestCm"] = new JsonObject { ["type"] = "number" },
                    ["hipCm"] = new JsonObject { ["type"] = "number" },
                    ["armCm"] = new JsonObject { ["type"] = "number" },
                    ["thighCm"] = new JsonObject { ["type"] = "number" },
                    ["notes"] = new JsonObject { ["type"] = "string" }
                }
            }),
        Tool("upsert_lift_goal", "Crea o actualiza el objetivo de fuerza de un ejercicio (clave: nombre del ejercicio, sin distinguir mayúsculas).",
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("exerciseName", "targetWeightKg"),
                ["properties"] = new JsonObject
                {
                    ["exerciseName"] = new JsonObject { ["type"] = "string", ["description"] = "Nombre del ejercicio como en Hevy." },
                    ["targetWeightKg"] = new JsonObject { ["type"] = "number" },
                    ["targetReps"] = new JsonObject { ["type"] = "integer", ["description"] = "Por defecto 1." },
                    ["hevyExerciseTemplateId"] = new JsonObject { ["type"] = "string" },
                    ["targetDate"] = new JsonObject { ["type"] = "string", ["description"] = "yyyy-MM-dd; cadena vacía para quitarla." },
                    ["notes"] = new JsonObject { ["type"] = "string" },
                    ["achieved"] = new JsonObject { ["type"] = "boolean" }
                }
            }),
        Tool("delete_lift_goal", "Borra un objetivo de fuerza por su id.",
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("id"),
                ["properties"] = new JsonObject { ["id"] = new JsonObject { ["type"] = "integer" } }
            }),
        Tool("set_training_slots",
            "Define los huecos semanales en los que el usuario puede entrenar. SUSTITUYE todos los anteriores (lista vacía = ninguno). "
            + "Cada hueco: día 1 (lunes) a 7 (domingo), hora de inicio HH:mm, minutos disponibles y, opcionalmente, el tipo de sesión preferido.",
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("slots"),
                ["properties"] = new JsonObject
                {
                    ["slots"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["required"] = new JsonArray("dayOfWeek", "startTime"),
                            ["properties"] = new JsonObject
                            {
                                ["dayOfWeek"] = new JsonObject { ["type"] = "integer", ["description"] = "1 = lunes ... 7 = domingo" },
                                ["startTime"] = new JsonObject { ["type"] = "string", ["description"] = "HH:mm" },
                                ["durationMinutes"] = new JsonObject { ["type"] = "integer", ["description"] = "20-240, por defecto 75" },
                                ["label"] = new JsonObject { ["type"] = "string", ["description"] = "Sesión preferida ese día, p. ej. Push" }
                            }
                        }
                    }
                }
            }),
        Tool("upsert_exercise_rules",
            "Crea o actualiza reglas de progresión por ejercicio (clave: nombre del ejercicio, sin distinguir mayúsculas). "
            + "Al completar repMax repeticiones en todas las series se sube incrementKg; por debajo de repMin se mantiene o se baja.",
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("rules"),
                ["properties"] = new JsonObject
                {
                    ["rules"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["required"] = new JsonArray("exerciseName", "repMin", "repMax", "incrementKg"),
                            ["properties"] = new JsonObject
                            {
                                ["exerciseName"] = new JsonObject { ["type"] = "string" },
                                ["repMin"] = new JsonObject { ["type"] = "integer" },
                                ["repMax"] = new JsonObject { ["type"] = "integer" },
                                ["incrementKg"] = new JsonObject { ["type"] = "number" },
                                ["notes"] = new JsonObject { ["type"] = "string" }
                            }
                        }
                    }
                }
            }),
        Tool("delete_exercise_rule", "Borra una regla de progresión por su id.",
            new JsonObject
            {
                ["type"] = "object",
                ["required"] = new JsonArray("id"),
                ["properties"] = new JsonObject { ["id"] = new JsonObject { ["type"] = "integer" } }
            })
    };

    private static JsonObject Tool(string name, string description, JsonObject schema) => new()
    {
        ["name"] = name,
        ["description"] = description,
        ["inputSchema"] = schema
    };

    private async Task<JsonNode> CallToolAsync(JsonObject? p, CancellationToken ct)
    {
        var name = p?["name"]?.GetValue<string>() ?? throw new McpException(-32602, "Falta el nombre de la herramienta.");
        var args = p["arguments"] as JsonObject ?? new JsonObject();

        try
        {
            switch (name)
            {
                case "upsert_workout_session":
                {
                    var input = args.Deserialize<WorkoutSessionInput>(Json) ?? throw new ArgumentException("Argumentos no válidos.");
                    return ToolResult(JsonSerializer.Serialize(await _workouts.UpsertAsync(input, ct), Json));
                }
                case "list_calendar_events":
                {
                    var from = WorkoutService.TryParseDate(args["from"]?.GetValue<string>(), out var d1) ? d1 : throw new ArgumentException("'from' debe ser yyyy-MM-dd.");
                    var to = WorkoutService.TryParseDate(args["to"]?.GetValue<string>(), out var d2) ? d2 : throw new ArgumentException("'to' debe ser yyyy-MM-dd.");
                    if (to < from || (to - from).TotalDays > 62) throw new ArgumentException("El rango debe ser de 62 días como máximo y 'to' no puede ser anterior a 'from'.");
                    return ToolResult(JsonSerializer.Serialize(await _workouts.ListBusyAsync(from, to, ct), Json));
                }
                case "list_workout_sessions":
                {
                    DateTime? from = null, to = null;
                    if (args["from"]?.GetValue<string>() is { Length: > 0 } f)
                        from = WorkoutService.TryParseDate(f, out var d1) ? d1 : throw new ArgumentException("'from' debe ser yyyy-MM-dd.");
                    if (args["to"]?.GetValue<string>() is { Length: > 0 } t)
                        to = WorkoutService.TryParseDate(t, out var d2) ? d2 : throw new ArgumentException("'to' debe ser yyyy-MM-dd.");
                    return ToolResult(JsonSerializer.Serialize(await _workouts.ListAsync(from, to, ct), Json));
                }
                case "delete_workout_session":
                {
                    var id = args["id"]?.GetValue<int>() ?? throw new ArgumentException("Falta 'id'.");
                    return await _workouts.DeleteAsync(id, ct) ? ToolResult("Borrada.") : ToolResult("No existe esa sesión.", isError: true);
                }
                case "get_profile":
                {
                    var count = args["measurements"]?.GetValue<int>() ?? 30;
                    return ToolResult(JsonSerializer.Serialize(await _profile.GetAsync(count, ct), Json));
                }
                case "update_profile":
                {
                    var input = args.Deserialize<UpdateProfileInput>(Json) ?? throw new ArgumentException("Argumentos no válidos.");
                    return ToolResult(JsonSerializer.Serialize(await _profile.UpdateProfileAsync(input, ct), Json));
                }
                case "log_body_measurement":
                {
                    var input = args.Deserialize<BodyMeasurementInput>(Json) ?? throw new ArgumentException("Argumentos no válidos.");
                    return ToolResult(JsonSerializer.Serialize(await _profile.LogMeasurementAsync(input, ct), Json));
                }
                case "upsert_lift_goal":
                {
                    var input = args.Deserialize<LiftGoalInput>(Json) ?? throw new ArgumentException("Argumentos no válidos.");
                    return ToolResult(JsonSerializer.Serialize(await _profile.UpsertLiftGoalAsync(input, ct), Json));
                }
                case "delete_lift_goal":
                {
                    var id = args["id"]?.GetValue<int>() ?? throw new ArgumentException("Falta 'id'.");
                    return await _profile.DeleteLiftGoalAsync(id, ct) ? ToolResult("Borrado.") : ToolResult("No existe ese objetivo.", isError: true);
                }
                case "set_training_slots":
                {
                    var slots = args["slots"]?.Deserialize<List<TrainingSlotInput>>(Json) ?? throw new ArgumentException("Falta 'slots'.");
                    return ToolResult(JsonSerializer.Serialize(await _profile.SetTrainingSlotsAsync(slots, ct), Json));
                }
                case "upsert_exercise_rules":
                {
                    var rules = args["rules"]?.Deserialize<List<ExerciseRuleInput>>(Json) ?? throw new ArgumentException("Falta 'rules'.");
                    return ToolResult(JsonSerializer.Serialize(await _profile.UpsertExerciseRulesAsync(rules, ct), Json));
                }
                case "delete_exercise_rule":
                {
                    var id = args["id"]?.GetValue<int>() ?? throw new ArgumentException("Falta 'id'.");
                    return await _profile.DeleteExerciseRuleAsync(id, ct) ? ToolResult("Borrada.") : ToolResult("No existe esa regla.", isError: true);
                }
                default:
                    throw new McpException(-32602, $"Herramienta desconocida: {name}");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or InvalidOperationException or FormatException or GoogleException)
        {
            // Errores de la herramienta (datos mal formados): se devuelven como resultado, para que el modelo pueda corregirlos.
            return ToolResult(ex.Message, isError: true);
        }
    }

    private static JsonObject ToolResult(string text, bool isError = false) => new()
    {
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
        ["isError"] = isError
    };
}
