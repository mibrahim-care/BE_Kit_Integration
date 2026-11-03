namespace Fakeeh.MedicalKit.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
