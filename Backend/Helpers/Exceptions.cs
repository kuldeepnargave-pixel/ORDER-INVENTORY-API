namespace Backend.Helpers;

/// <summary>Mapped to HTTP 400 by the exception-handling middleware.</summary>
public class OrderValidationException : Exception
{
    public OrderValidationException(string message) : base(message) { }
}

/// <summary>Mapped to HTTP 404 by the exception-handling middleware.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Mapped to HTTP 403 by the exception-handling middleware.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>Mapped to HTTP 401 by the exception-handling middleware.</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

/// <summary>Mapped to HTTP 409 by the exception-handling middleware.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
