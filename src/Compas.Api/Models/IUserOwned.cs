namespace Compas.Api.Models;

/// <summary>
/// Dato que pertenece a un usuario. CompasDbContext filtra por UserId todas las consultas de estas entidades y
/// asigna el usuario de la sesión a las nuevas, así que cada cuenta solo ve y modifica lo suyo.
/// UserId 0 = sin propietario (datos anteriores a las cuentas, que reclama el primer usuario que se crea).
/// </summary>
public interface IUserOwned
{
    int UserId { get; set; }
}
