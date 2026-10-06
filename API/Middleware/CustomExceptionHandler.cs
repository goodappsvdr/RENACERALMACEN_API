using System.Net.Mime;
using API.SERVICE.Domain.Exceptions;
using API.SERVICE.Models.Common;

namespace API.Middleware;

/// <summary>
/// Traduce excepciones a <see cref="ErrorCatchResponse"/>. Las de negocio conservan su mensaje y código;
/// cualquier otra es 500 con mensaje genérico (el detalle queda solo en el log).
/// </summary>
public sealed class CustomExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CustomExceptionHandler> _logger;

    public CustomExceptionHandler(RequestDelegate next, ILogger<CustomExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            await WriteAsync(context, new ErrorCatchResponse(ex.Message, ex.StatusCode));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente cortó el request: no hay a quién responder.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, new ErrorCatchResponse("Ocurrió un error inesperado.", StatusCodes.Status500InternalServerError));
        }
    }

    private static Task WriteAsync(HttpContext context, ErrorCatchResponse error)
    {
        context.Response.StatusCode = error.StatusCode;
        context.Response.ContentType = MediaTypeNames.Application.Json;
        return context.Response.WriteAsJsonAsync(error);
    }
}
