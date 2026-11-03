namespace Fakeeh.MedicalKit.Application.Abstractions;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed class DuplicateKeyException(string message, Exception? innerException = null)
    : Exception(message, innerException);
