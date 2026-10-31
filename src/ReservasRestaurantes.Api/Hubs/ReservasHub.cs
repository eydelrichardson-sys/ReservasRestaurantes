using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ReservasRestaurantes.Api.Hubs;

/// <summary>
/// Canal de tiempo real. Solo administradores autenticados pueden conectarse, y cada uno
/// solo puede unirse al grupo de SU restaurante (claim "rid" del JWT).
/// El aviso "ReservasActualizadas" no lleva datos: el dashboard vuelve a pedir la lista por la API.
/// </summary>
[Authorize(Roles = "Admin")]
public class ReservasHub : Hub
{
    public static string Grupo(int idRestaurante) => $"rest-{idRestaurante}";

    public async Task Unirse(int idRestaurante)
    {
        var rid = Context.User?.FindFirst("rid")?.Value;
        if (!int.TryParse(rid, out var propio) || propio != idRestaurante)
            throw new HubException("No tienes acceso a este restaurante.");

        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(idRestaurante));
    }
}
