namespace Fakeeh.MedicalKit.Application.Abstractions;

/// <summary>Fakeeh HIS could not be reached or returned an unexpected response.</summary>
public sealed class HisUnavailableException(string message) : Exception(message);
