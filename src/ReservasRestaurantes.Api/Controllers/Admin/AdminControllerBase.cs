using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ReservasRestaurantes.Api.Controllers.Admin;

/// <summary>
/// Base de los controladores de administración. El administrador solo puede tocar
/// los datos de SU restaurante (claim "rid" del JWT), nunca el de otro.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
public abstract class AdminControllerBase : ControllerBase
{
    protected int IdRestaurante =>
        int.TryParse(User.FindFirst("rid")?.Value, out var id) ? id : 0;
}
