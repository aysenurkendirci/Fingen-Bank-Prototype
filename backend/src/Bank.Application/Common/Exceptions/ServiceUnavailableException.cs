namespace Bank.Application.Common.Exceptions;

/// <summary>Something the request depends on outside this system is not answering; answered with 503.</summary>
public sealed class ServiceUnavailableException : AppException
{
    public ServiceUnavailableException(string message) : base(message) { }
}
