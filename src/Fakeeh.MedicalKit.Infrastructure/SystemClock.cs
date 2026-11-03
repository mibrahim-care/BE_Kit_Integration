using Fakeeh.MedicalKit.Application.Abstractions;

namespace Fakeeh.MedicalKit.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
