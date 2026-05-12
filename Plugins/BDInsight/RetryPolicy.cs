using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BDInsight
{
    #region RetryPolicy

    public static class RetryPolicy
    {
        public static async Task<RetryResult<T>> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            int maxAttempts,
            Func<T, bool>? successPredicate = null,
            Func<Exception, bool>? shouldRetryOnException = null,
            Func<int, int>? delayMsFactory = null,
            Action<int, Exception?>? onRetry = null,
            CancellationToken token = default)
        {
            if (maxAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));

            successPredicate ??= (_ => true);
            shouldRetryOnException ??= (_ => true);
            delayMsFactory ??= (attempt => Math.Min(300 * attempt, 1500));

            Exception? lastException = null;
            T? lastValue = default;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    lastValue = await action(token);
                    if (successPredicate(lastValue))
                        return RetryResult<T>.Success(lastValue, attempt);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (!shouldRetryOnException(ex) || attempt >= maxAttempts)
                        break;

                    onRetry?.Invoke(attempt, ex);
                    await Task.Delay(delayMsFactory(attempt), token);
                    continue;
                }

                if (attempt < maxAttempts)
                {
                    onRetry?.Invoke(attempt, null);
                    await Task.Delay(delayMsFactory(attempt), token);
                }
            }

            return RetryResult<T>.Fail(lastValue, lastException, maxAttempts);
        }

        public static async Task<RetryResult<bool>> ExecuteBoolAsync(
            Func<CancellationToken, Task<bool>> action,
            int maxAttempts,
            Func<Exception, bool>? shouldRetryOnException = null,
            Func<int, int>? delayMsFactory = null,
            Action<int, Exception?>? onRetry = null,
            CancellationToken token = default)
        {
            return await ExecuteAsync(
                action,
                maxAttempts,
                successPredicate: v => v,
                shouldRetryOnException: shouldRetryOnException,
                delayMsFactory: delayMsFactory,
                onRetry: onRetry,
                token: token);
        }
    }


    public sealed class RetryResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Value { get; private set; }
        public Exception? Exception { get; private set; }
        public int Attempts { get; private set; }

        public static RetryResult<T> Success(T? value, int attempts) =>
            new RetryResult<T> { IsSuccess = true, Value = value, Attempts = attempts };

        public static RetryResult<T> Fail(T? value, Exception? exception, int attempts) =>
            new RetryResult<T> { IsSuccess = false, Value = value, Exception = exception, Attempts = attempts };
    }


    #endregion
}
