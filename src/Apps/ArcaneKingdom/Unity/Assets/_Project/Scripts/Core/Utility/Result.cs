#nullable enable
using System;

namespace ArcaneKingdom.Core.Utility
{
    /// <summary>
    /// Generischer Result-Type zur expliziten Fehlerbehandlung ohne Exceptions.
    /// Wird fuer alle fehlbaren Operationen (Save, Auth, Network) verwendet.
    /// </summary>
    public readonly struct Result
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public Exception? Exception { get; }

        private Result(bool success, string? errorMessage, Exception? exception)
        {
            IsSuccess = success;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public static Result Success() => new(true, null, null);
        public static Result Failure(string error) => new(false, error, null);
        public static Result Failure(Exception ex) => new(false, ex.Message, ex);
    }

    /// <summary>
    /// Result-Type mit Werteruckgabe. Bei IsSuccess ist Value gesetzt.
    /// </summary>
    public readonly struct Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public string? ErrorMessage { get; }
        public Exception? Exception { get; }

        private Result(bool success, T? value, string? errorMessage, Exception? exception)
        {
            IsSuccess = success;
            Value = value;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public static Result<T> Success(T value) => new(true, value, null, null);
        public static Result<T> Failure(string error) => new(false, default, error, null);
        public static Result<T> Failure(Exception ex) => new(false, default, ex.Message, ex);
    }
}
