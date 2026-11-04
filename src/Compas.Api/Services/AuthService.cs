using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using Compas.Api.Data;
using Compas.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Compas.Api.Services;

public class AuthException : Exception
{
    public int StatusCode { get; }
    public AuthException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
}

/// <summary>
/// Usuarios, contraseñas y sesión. Contraseñas con PBKDF2-SHA256 (sin paquetes nuevos), sesión con la cookie
/// de autenticación de ASP.NET (persistente si el usuario marca «Recordar este dispositivo»).
/// </summary>
public class AuthService
{
    private const int Iterations = 600_000; // recomendación de OWASP para PBKDF2-HMAC-SHA256
    private const int MinPasswordLength = 10;
    private const int MaxPasswordLength = 128;

    private readonly CompasDbContext _db;
    private readonly AuthOptions _options;

    public AuthService(CompasDbContext db, IOptions<AuthOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    // ---------------- Política de acceso ----------------

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email)
    {
        if (email.Length is 0 or > 320) return false;
        return MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('.', StringComparison.Ordinal);
    }

    /// <summary>¿Puede este correo crear una cuenta? Con OwnerEmail, solo ese; si no, solo el primer usuario.</summary>
    public async Task<bool> CanRegisterAsync(string email, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_options.OwnerEmail))
            return NormalizeEmail(_options.OwnerEmail) == email && !await _db.Users.AnyAsync(u => u.Email == email, ct);
        return !await _db.Users.AnyAsync(ct);
    }

    /// <summary>¿Hay alguna cuenta que todavía se pueda crear? (decide si la pantalla ofrece «Crear cuenta»).</summary>
    public async Task<bool> IsRegistrationOpenAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_options.OwnerEmail))
        {
            var owner = NormalizeEmail(_options.OwnerEmail);
            return !await _db.Users.AnyAsync(u => u.Email == owner, ct);
        }
        return !await _db.Users.AnyAsync(ct);
    }

    // ---------------- Contraseñas ----------------

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Hash de relleno: se comprueba contra él cuando el correo no existe, para que tardar lo mismo no delate qué cuentas hay.
    private static readonly string DummyHash = HashPassword("relleno-sin-uso");

    // ---------------- Registro e inicio de sesión ----------------

    public async Task<AppUser> RegisterAsync(string? rawEmail, string? password, string? displayName, CancellationToken ct = default)
    {
        var email = NormalizeEmail(rawEmail);
        if (!IsValidEmail(email)) throw new AuthException("Escribe un correo válido.");
        if (password is null || password.Length < MinPasswordLength)
            throw new AuthException($"La contraseña debe tener al menos {MinPasswordLength} caracteres.");
        if (password.Length > MaxPasswordLength) throw new AuthException($"La contraseña no puede pasar de {MaxPasswordLength} caracteres.");
        if (!await CanRegisterAsync(email, ct))
            throw new AuthException("El registro está cerrado: esta instalación de Compás ya tiene propietario.", 403);

        var name = displayName?.Trim();
        var user = new AppUser
        {
            Email = email,
            DisplayName = string.IsNullOrEmpty(name) ? null : name[..Math.Min(name.Length, 100)],
            PasswordHash = HashPassword(password),
            LastLoginAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<AppUser> LoginAsync(string? rawEmail, string? password, CancellationToken ct = default)
    {
        var email = NormalizeEmail(rawEmail);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Siempre se calcula un hash, exista o no el usuario.
        var matches = VerifyPassword(password ?? "", user?.PasswordHash ?? DummyHash);
        if (user?.PasswordHash is null || !matches)
            throw new AuthException("Correo o contraseña incorrectos. Si te registraste con Google, usa «Continuar con Google».", 401);

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return user;
    }

    /// <summary>
    /// Usuario para una cuenta de Google ya verificada. Solo entra quien tiene permiso (ya es usuario, o puede
    /// registrarse); un usuario existente con ese correo se vincula a su cuenta de Google.
    /// </summary>
    public async Task<AppUser> SignInWithGoogleAsync(GoogleSignIn google, CancellationToken ct = default)
    {
        if (!google.EmailVerified || string.IsNullOrEmpty(google.Subject))
            throw new AuthException("Google no ha verificado el correo de esta cuenta.", 403);

        var email = NormalizeEmail(google.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleSubject == google.Subject, ct)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            if (!await CanRegisterAsync(email, ct))
                throw new AuthException("Esta cuenta de Google no tiene acceso a Compás.", 403);
            user = new AppUser { Email = email, DisplayName = google.Name };
            _db.Users.Add(user);
        }
        else if (user.GoogleSubject is not null && user.GoogleSubject != google.Subject)
        {
            throw new AuthException("Este correo ya está vinculado a otra cuenta de Google.", 403);
        }

        user.GoogleSubject = google.Subject;
        user.DisplayName ??= google.Name;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return user;
    }

    // ---------------- Sesión (cookie) ----------------

    public async Task SignInAsync(HttpContext http, AppUser user, bool remember)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName ?? user.Email)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        var props = new AuthenticationProperties
        {
            IsPersistent = remember, // cookie con caducidad (recuerda el dispositivo) o de sesión (se borra al cerrar el navegador)
            ExpiresUtc = DateTimeOffset.UtcNow.Add(remember ? TimeSpan.FromDays(_options.RememberDays) : TimeSpan.FromHours(_options.SessionHours)),
            AllowRefresh = true
        };
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }

    public static Task SignOutAsync(HttpContext http) => http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
