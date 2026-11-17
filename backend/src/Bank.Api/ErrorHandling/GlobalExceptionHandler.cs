using System.Text.Json;
using Bank.Application.Common.Exceptions;
using Bank.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.ErrorHandling;

/// <summary>
/// Translates exceptions into RFC 9457 problem details responses.
/// Only <see cref="AppException"/> and <see cref="DomainException"/> messages and validation messages reach
/// the client; all of them are written for end users. Any other exception becomes a
/// generic 500 so stack traces, SQL errors and internal names are never leaked.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        // The client went away (a closed tab, a page left half way). SQL Server reports that as an error of its own
        // rather than a cancellation, which is not a fault worth an error log, and nobody is left to read a reply.
        if (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request {Method} {Path} ended because the client went away", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
            DomainException => (StatusCodes.Status400BadRequest, "İş kuralı ihlali"),
            ImportRejectedException => (StatusCodes.Status400BadRequest, "Dosya içe aktarılamadı"),
            BadRequestException => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
            NotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı"),
            ConflictException => (StatusCodes.Status409Conflict, "İşlem mevcut kayıtla çakışıyor"),
            IdempotencyKeyReusedException => (StatusCodes.Status422UnprocessableEntity, "Idempotency-Key yeniden kullanıldı"),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Kimlik doğrulama başarısız"),
            SecondFactorRequiredException => (StatusCodes.Status401Unauthorized, "İkinci doğrulama gerekli"),
            SecondFactorFailedException => (StatusCodes.Status401Unauthorized, "İkinci doğrulama başarısız"),
            AccountLockedException => (StatusCodes.Status423Locked, "Hesap geçici olarak kilitli"),
            ServiceUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Hizmet geçici olarak kullanılamıyor"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;

        // Validation failures use the same field -> messages shape as ASP.NET's own model validation,
        // so clients handle both the same way.
        var details = exception is ValidationException validation
            ? new HttpValidationProblemDetails(validation.Errors
                .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            : new ProblemDetails();

        details.Status = status;
        details.Title = title;
        details.Detail = exception is AppException or DomainException ? exception.Message : null;

        var problem = new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = details
        };

        // The sign-in form reads this to ask for the code instead of showing an error.
        if (exception is SecondFactorRequiredException)
            problem.ProblemDetails.Extensions["secondFactorRequired"] = true;
        if (exception is AccountLockedException locked)
            problem.ProblemDetails.Extensions["lockedUntil"] = DateTime.SpecifyKind(locked.LockedUntil, DateTimeKind.Utc);

        if (exception is ImportRejectedException rejected)
            problem.ProblemDetails.Extensions["errors"] = rejected.Errors;

        return await _problemDetails.TryWriteAsync(problem);
    }
}
