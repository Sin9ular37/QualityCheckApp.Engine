using System;
using System.Threading;
using System.Threading.Tasks;

namespace QualityCheckApp.Engine.Infrastructure
{
    internal static class StaTask
    {
        public static Task<T> Run<T>(Func<T> function, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<T>();

            Thread thread = new Thread(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var result = function();
                    tcs.TrySetResult(result);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            })
            {
                IsBackground = true
            };

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled();
            });

            return tcs.Task;
        }
    }
}
