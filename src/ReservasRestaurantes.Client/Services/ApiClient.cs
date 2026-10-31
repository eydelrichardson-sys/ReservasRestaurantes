using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ReservasRestaurantes.Shared.Dtos;

namespace ReservasRestaurantes.Client.Services;

public class ApiResult<T>
{
    public bool Ok { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Envoltorio de HttpClient: agrega el JWT, convierte errores en mensajes legibles
/// y cierra la sesión si el servidor responde 401.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;

    public ApiClient(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url);
    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null) => SendAsync<T>(HttpMethod.Post, url, body);
    public Task<ApiResult<object>> PostAsync(string url, object? body = null) => SendAsync<object>(HttpMethod.Post, url, body);
    public Task<ApiResult<object>> PutAsync(string url, object? body = null) => SendAsync<object>(HttpMethod.Put, url, body);
    public Task<ApiResult<object>> DeleteAsync(string url) => SendAsync<object>(HttpMethod.Delete, url);

    /// <summary>Descarga un archivo (Excel, PDF) como bytes.</summary>
    public async Task<ApiResult<byte[]>> GetBytesAsync(string url)
    {
        await _auth.GetAuthenticationStateAsync();

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(_auth.Token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);

        try
        {
            using var resp = await _http.SendAsync(req);
            if (resp.IsSuccessStatusCode)
                return new ApiResult<byte[]> { Ok = true, Data = await resp.Content.ReadAsByteArrayAsync() };

            if (resp.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_auth.Token))
                await _auth.LogoutAsync();

            return new ApiResult<byte[]> { Ok = false, Error = await LeerErrorAsync(resp) };
        }
        catch (HttpRequestException)
        {
            return new ApiResult<byte[]> { Ok = false, Error = "No se pudo conectar con el servidor. ¿Está corriendo la API?" };
        }
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        // Asegura que el token de sessionStorage esté cargado antes de la primera llamada.
        await _auth.GetAuthenticationStateAsync();

        using var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(_auth.Token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.Token);
        if (body != null)
            req.Content = JsonContent.Create(body, body.GetType());

        try
        {
            using var resp = await _http.SendAsync(req);

            if (resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
                    return new ApiResult<T> { Ok = true };

                var data = await resp.Content.ReadFromJsonAsync<T>();
                return new ApiResult<T> { Ok = true, Data = data };
            }

            if (resp.StatusCode == HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_auth.Token))
                await _auth.LogoutAsync();

            return new ApiResult<T> { Ok = false, Error = await LeerErrorAsync(resp) };
        }
        catch (HttpRequestException)
        {
            return new ApiResult<T> { Ok = false, Error = "No se pudo conectar con el servidor. ¿Está corriendo la API?" };
        }
    }

    private static async Task<string> LeerErrorAsync(HttpResponseMessage resp)
    {
        if ((int)resp.StatusCode == 429) return "Demasiadas solicitudes. Espera un momento e inténtalo de nuevo.";
        if (resp.StatusCode == HttpStatusCode.Forbidden) return "No tienes permiso para esta acción.";
        if (resp.StatusCode == HttpStatusCode.Unauthorized) return "Tu sesión expiró. Inicia sesión de nuevo.";

        try
        {
            var err = await resp.Content.ReadFromJsonAsync<ApiError>();
            if (!string.IsNullOrWhiteSpace(err?.Mensaje)) return err!.Mensaje;
        }
        catch { /* respuesta sin formato ApiError (p. ej. validación automática) */ }

        return resp.StatusCode == HttpStatusCode.BadRequest
            ? "Revisa los datos ingresados."
            : $"Error del servidor ({(int)resp.StatusCode}).";
    }
}
