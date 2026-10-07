using System;

namespace DomainLayer.Common
{
    /// <summary>
    /// Represents the outcome of an operation as an explicit success/failure value rather than
    /// an exception. Business outcomes flow through <see cref="Result"/>; exceptions are reserved
    /// for truly unexpected faults. A failure always carries a non-empty <see cref="Error"/>.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Initializes a result. Enforces the invariant that success carries no error and
        /// failure carries a real error.
        /// </summary>
        /// <param name="isSuccess">Whether the operation succeeded.</param>
        /// <param name="error">The failure detail, or <see cref="Error.None"/> on success.</param>
        /// <exception cref="InvalidOperationException">Thrown when the success flag and error are inconsistent.</exception>
        protected Result(bool isSuccess, Error error)
        {
            if (isSuccess && error != Error.None)
            {
                throw new InvalidOperationException("A successful result cannot carry an error.");
            }

            if (!isSuccess && error == Error.None)
            {
                throw new InvalidOperationException("A failed result must carry an error.");
            }

            IsSuccess = isSuccess;
            Error = error;
        }

        /// <summary>True when the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>True when the operation failed. Convenience inverse of <see cref="IsSuccess"/>.</summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>The failure detail. Equals <see cref="Error.None"/> when <see cref="IsSuccess"/> is true.</summary>
        public Error Error { get; }

        /// <summary>Creates a successful, valueless result.</summary>
        public static Result Success() => new(true, Error.None);

        /// <summary>Creates a failed result carrying the supplied error.</summary>
        public static Result Failure(Error error) => new(false, error);

        /// <summary>Creates a successful result carrying a value.</summary>
        public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

        /// <summary>Creates a failed typed result carrying the supplied error.</summary>
        public static Result<TValue> Failure<TValue>(Error error) => new(default!, false, error);
    }

    /// <summary>
    /// A <see cref="Result"/> that carries a value on success. Accessing <see cref="Value"/> on a
    /// failed result throws, so callers must branch on <see cref="Result.IsSuccess"/> first.
    /// </summary>
    /// <typeparam name="TValue">Type of the value produced on success.</typeparam>
    public sealed class Result<TValue> : Result
    {
        private readonly TValue _value;

        /// <summary>
        /// Initializes a typed result.
        /// </summary>
        /// <param name="value">The success value (ignored when <paramref name="isSuccess"/> is false).</param>
        /// <param name="isSuccess">Whether the operation succeeded.</param>
        /// <param name="error">The failure detail, or <see cref="Error.None"/> on success.</param>
        internal Result(TValue value, bool isSuccess, Error error)
            : base(isSuccess, error)
        {
            _value = value;
        }

        /// <summary>
        /// The value produced on success.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if accessed on a failed result.</exception>
        public TValue Value => IsSuccess
            ? _value
            : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

        /// <summary>Implicitly wraps a value as a successful result.</summary>
        public static implicit operator Result<TValue>(TValue value) => Success(value);
    }
}
