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
    private readonly CurrentUser _currentUser;

    public McpController(ApiTokenService tokens, WorkoutService workouts, CurrentUser currentUser)
    {
        _tokens = tokens;
        _workouts = workouts;
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
                    ["instructions"] = "Conector de Compás. Usa upsert_workout_session para programar entrenamientos con sus pesos objetivo "
                                       + "(es idempotente con source + externalId), list_workout_sessions para leer lo ya programado."
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
        Tool("delete_workout_session", "Borra una sesión programada por su id.",
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
                    var (session, created) = await _workouts.UpsertAsync(input, ct);
                    return ToolResult(JsonSerializer.Serialize(new { created, session }, Json));
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
                default:
                    throw new McpException(-32602, $"Herramienta desconocida: {name}");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or InvalidOperationException or FormatException)
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
