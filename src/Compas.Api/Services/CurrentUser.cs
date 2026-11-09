using System.Security.Claims;

namespace Compas.Api.Services;

/// <summary>
/// Usuario de la petición en curso (el de la cookie de sesión). Con <see cref="Set"/> se puede fijar a mano, para el
/// momento justo después de iniciar sesión, en que la petición aún no lleva la cookie nueva. 0 = nadie.
/// </summary>
public class CurrentUser
{
    private readonly IHttpContextAccessor _http;
    private int? _fixed;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public int UserId => _fixed ?? (int.TryParse(_http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0);

    public void Set(int userId) => _fixed = userId;
}
