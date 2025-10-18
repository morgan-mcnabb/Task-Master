namespace Application.Common.Exceptions;

public sealed class ConcurrencyException(string message) : Exception(message);