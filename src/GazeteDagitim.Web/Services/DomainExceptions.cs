namespace GazeteDagitim.Web.Services;

public class DomainValidationException(string message) : InvalidOperationException(message)
{
}

public sealed class DomainConflictException(string message) : DomainValidationException(message)
{
}

public sealed class EntityNotFoundException(string message) : DomainValidationException(message)
{
}
