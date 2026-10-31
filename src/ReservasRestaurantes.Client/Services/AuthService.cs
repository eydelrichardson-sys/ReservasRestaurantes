using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using ReservasRestaurantes.Shared.Dtos;

namespace ReservasRestaurantes.Client.Services;

/// <summary>
/// Maneja el JWT del administrador. Se guarda en sessionStorage: al cerrar el navegador
/// hay que volver a iniciar sesión.
/// </summary>
public class AuthService : AuthenticationStateProvider
{
    private const string Clave = "rr_token";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private bool _cargado;

    public string? Token { get; private set; }

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_cargado)
        {
            _cargado = true;
            try { Token = await _js.InvokeAsync<string?>("sessionStorage.getItem", Clave); }
            catch { Token = null; }
        }

        var claims = string.IsNullOrEmpty(Token) ? null : LeerClaims(Token);
        if (claims == null)
        {
            Token = null;
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var identidad = new ClaimsIdentity(claims, "jwt", nameType: "name", roleType: "role");
        return new AuthenticationState(new ClaimsPrincipal(identidad));
    }

    /// <summary>Devuelve null si el login fue exitoso; si no, el mensaje de error.</summary>
    public Task<string?> LoginAsync(LoginRequest req) => AutenticarAsync("api/auth/login", req);

    /// <summary>Registra un restaurante nuevo y deja la sesión iniciada. null = éxito.</summary>
    public Task<string?> RegistrarAsync(RegistroRestauranteRequest req) => AutenticarAsync("api/auth/registro", req);

    private async Task<string?> AutenticarAsync<T>(string url, T body)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync(url, body);
            if (!resp.IsSuccessStatusCode)
            {
                if ((int)resp.StatusCode == 429) return "Demasiados intentos. Espera un minuto.";
                var err = await resp.Content.ReadFromJsonAsync<ApiError>();
                return err?.Mensaje is { Length: > 0 } m ? m : "No se pudo completar la operación.";
            }

            var data = await resp.Content.ReadFromJsonAsync<LoginResponse>();
            if (data == null) return "Respuesta inválida del servidor.";

            Token = data.Token;
            _cargado = true;
            await _js.InvokeVoidAsync("sessionStorage.setItem", Clave, Token);
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            return null;
        }
        catch (HttpRequestException)
        {
            return "No se pudo conectar con el servidor.";
        }
    }

    public async Task LogoutAsync()
    {
        Token = null;
        _cargado = true;
        try { await _js.InvokeVoidAsync("sessionStorage.removeItem", Clave); } catch { }
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    // ---------- lectura del JWT (sin validar firma: eso lo hace la API) ----------

    private static List<Claim>? LeerClaims(string jwt)
    {
        try
        {
            var partes = jwt.Split('.');
            if (partes.Length != 3) return null;

            var payload = partes[1].Replace('-', '+').Replace('_', '/');
            payload = (payload.Length % 4) switch
            {
                2 => payload + "==",
                3 => payload + "=",
                _ => payload
            };

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            var claims = new List<Claim>();

            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Array)
                    foreach (var v in p.Value.EnumerateArray())
                        claims.Add(new Claim(p.Name, v.ToString()));
                else
                    claims.Add(new Claim(p.Name, p.Value.ToString()));
            }

            var exp = claims.FirstOrDefault(c => c.Type == "exp");
            if (exp != null && long.TryParse(exp.Value, out var segundos) &&
                DateTimeOffset.FromUnixTimeSeconds(segundos) <= DateTimeOffset.UtcNow)
                return null; // token vencido

            return claims;
        }
        catch
        {
            return null;
        }
    }
}
