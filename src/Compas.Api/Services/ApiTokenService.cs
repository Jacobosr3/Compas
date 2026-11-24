using System.Security.Cryptography;
using System.Text;
using Compas.Api.Data;
using Compas.Api.Dtos;
using Compas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Compas.Api.Services;

/// <summary>Tokens personales del conector MCP: alta, baja y validación (por hash, nunca en claro).</summary>
public class ApiTokenService
{
    private readonly CompasDbContext _db;

    public ApiTokenService(CompasDbContext db) => _db = db;

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static ApiTokenDto ToDto(ApiToken t) => new(t.Id, t.Name, t.Prefix, t.CreatedAt, t.LastUsedAt);

    public async Task<List<ApiTokenDto>> ListAsync(CancellationToken ct) =>
        (await _db.ApiTokens.AsNoTracking().OrderByDescending(t => t.CreatedAt).ToListAsync(ct)).Select(ToDto).ToList();

    /// <summary>Prefijo de los tokens del conector MCP (acceso completo a lo que expone el MCP).</summary>
    public const string McpPrefix = "cmp_";
    /// <summary>Prefijo de los tokens de captura rápida (atajo del móvil): solo sirven para subir comidas con foto.</summary>
    public const string QuickPrefix = "cmq_";

    /// <summary>Crea un token (256 bits aleatorios); devuelve el valor en claro, que no se puede recuperar después.</summary>
    public async Task<(ApiTokenDto Dto, string Value)> CreateAsync(string? name, CancellationToken ct, string prefix = McpPrefix)
    {
        var value = prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var clean = string.IsNullOrWhiteSpace(name) ? (prefix == QuickPrefix ? "Atajo de comida" : "Conector") : name.Trim();
        var token = new ApiToken
        {
            Name = clean.Length > 100 ? clean[..100] : clean,
            TokenHash = Hash(value),
            Prefix = value[..8]
        };
        _db.ApiTokens.Add(token);
        await _db.SaveChangesAsync(ct);
        return (ToDto(token), value);
    }

    public async Task<bool> RevokeAsync(int id, CancellationToken ct)
    {
        var token = await _db.ApiTokens.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (token is null) return false;
        _db.ApiTokens.Remove(token);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Id del usuario dueño del token, o null si no existe. Sin sesión, así que se salta el filtro por usuario.</summary>
    /// <remarks>El prefijo exigido separa los alcances: un token cmq_ no abre el MCP y uno cmp_ no sirve para la captura rápida.</remarks>
    public async Task<int?> ResolveUserAsync(string? token, CancellationToken ct, string prefix = McpPrefix)
    {
        if (string.IsNullOrEmpty(token) || !token.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var hash = Hash(token);
        var row = await _db.ApiTokens.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (row is null) return null;
        if (row.LastUsedAt is null || DateTime.UtcNow - row.LastUsedAt > TimeSpan.FromMinutes(5))
        {
            row.LastUsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return row.UserId;
    }
}
