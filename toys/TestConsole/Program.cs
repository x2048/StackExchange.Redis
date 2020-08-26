using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace TestConsole
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            int N = 1000000;
            int T = 10000;
            ThreadPool.SetMinThreads(100, 100);

            string connectionString = args.Length > 0 ? args[0] : "localhost";

            ConcurrentQueue<ConnectionMultiplexer> clients = new ConcurrentQueue<ConnectionMultiplexer>();
            for (int i = 0; i < 2; i++)
            {
                var client = ConnectionMultiplexer.Connect(connectionString);
                client.GetDatabase().Ping();
                clients.Enqueue(client);
            }

            bool terminated = false;
            var start = DateTime.Now;

            clients.TryDequeue(out var firstClient);
            clients.Enqueue(firstClient);
            await firstClient.GetDatabase().StringSetAsync("A", new string('a', 25000));

            int timeouts = 0;
            int reads = 0;
            long latency = 0;
            Exception ex = null;

            var tasks = Enumerable.Range(0, T).Select(async i =>
            {
                int timeoutCount = 0;
                RedisKey key = i.ToString();
                for (int t = 0; t < N/T && !terminated; t++)
                {
                    try
                    {
                        //await db.StringIncrementAsync(key, 1);
                        ConnectionMultiplexer client;
                        while (!clients.TryDequeue(out client)) await Task.Delay(0);
                        clients.Enqueue(client);

                        var s = new Stopwatch();
                        s.Start();

                        await client.GetDatabase().StringGetAsync("A");

                        Interlocked.Add(ref latency, (long)s.Elapsed.TotalMilliseconds);
                        Interlocked.Increment(ref reads);
                    }
                    catch (TimeoutException e)
                    { 
                        ex = e;
                        // Console.WriteLine("Timeout. Adding a multiplexer");
                        // clients.Enqueue(ConnectionMultiplexer.Connect(connectionString));
                        Interlocked.Increment(ref timeouts);
                    }
                }
                return timeoutCount;
            }).ToArray();

            var allTasks = Task.WhenAll(tasks);
            while (!allTasks.Wait(0))
            {
                await Task.Delay(1000);
                if (ex != null)
                {
                    Console.WriteLine(ex);
                    ex = null;
                }
                Console.WriteLine($"R = {reads}, T = {timeouts}, L = {(double)latency/reads}");
            }

            // int totalTimeouts = tasks.Sum(x => x.Result);
            Console.WriteLine("Total timeouts: " + timeouts);
            Console.WriteLine();
            // Show(client.GetCounters());

            var duration = DateTime.Now.Subtract(start).TotalMilliseconds;
            Console.WriteLine($"{duration}ms");
        }
        private static void Show(ServerCounters counters)
        {
            Console.WriteLine("CA: " + counters.Interactive.CompletedAsynchronously);
            Console.WriteLine("FA: " + counters.Interactive.FailedAsynchronously);
            Console.WriteLine("CS: " + counters.Interactive.CompletedSynchronously);
            Console.WriteLine();
        }
    }
}
