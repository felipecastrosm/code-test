using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace code_test
{
    public static class AsyncExtensions
    {
        public static async Task ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> body, CancellationToken cancellationToken, int degreeOfParallelism = 0)
        {
            if (degreeOfParallelism <= 0)
            {
                degreeOfParallelism = Environment.ProcessorCount;
            }

            var messagesBatchQueue = new ConcurrentQueue<T>(source);
            var processingStartTasks = new Collection<Task>();
            var exceptions = new ConcurrentBag<Exception>();

            for (var i = 0; i < degreeOfParallelism; i++)
            {
                processingStartTasks.Add(await Task.Factory.StartNew(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested && messagesBatchQueue.TryDequeue(out var message))
                    {
                        try
                        {
                            await body(message);
                        }
                        catch (Exception e)
                        {
                            exceptions.Add(e);
                        }
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(processingStartTasks.ToArray());

            if (!exceptions.IsEmpty)
            {
                throw new AggregateException(exceptions);
            }
        }
    }
}
