// WIRECLUB --------------------------------------------------------------
#pragma warning disable 1591

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Web;
using MongoDB.Bson;
using MongoDB.Bson.IO;

using System.Threading;

public static class LockingConstants
{
    public const int Timeout = 100000;
}

public struct DisposableLock : IDisposable
{
    private readonly object _scope;

    public static bool TimerEnabled;
    private static long _accumulated;

    public static long TotalTimeWaitingForlock
    {
        get
        {
            return Interlocked.Read(ref _accumulated);
        }
    }

    public static DisposableLock Lock(object scope, int timeout = LockingConstants.Timeout)
    {
        var timer = TimerEnabled ? Stopwatch.StartNew() : null;
        try
        {
            if (Monitor.TryEnter(scope, timeout) == false)
                throw new TimeoutException();
        }
        finally
        {
            if (timer != null)
                Interlocked.Add(ref _accumulated, timer.ElapsedMilliseconds);
        }
        return new DisposableLock(scope);
    }

    private DisposableLock(object scope)
    {
        _scope = scope;
    }

    public void Dispose()
    {
        Monitor.Exit(_scope);
    }
}

public static class DictionaryExtensions
{
    public static int IncrementKey<K>(this Dictionary<K, int> dictionary, K key)
    {
        return dictionary[key] = dictionary.AcquireKey(key) + 1;
    }

    public static V AcquireKey<K, V>(this Dictionary<K, V> dictionary, K key) where V : new()
    {
        V value;
        if (dictionary.TryGetValue(key, out value) == false)
            dictionary[key] = value = new V();
        return value;
    }

    public static void AddIfNotPresent<K, V>(this Dictionary<K, V> dictionary, K key, V value)
    {
        if (dictionary.ContainsKey(key) == false)
            dictionary[key] = value;
    }
}

public static class StringExtensions
{
    public static string Merge(this string str, params object[] data)
    {
        return String.Format(str, data);
    }
}

namespace MongoDB.Driver.Core
{
    public delegate T TraceDelegate<out T>();

    public class QueryPerformanceData
    {
        public string Identifier;
        public double Milliseconds;
        public int Count;
        public int Slow;
        public int Tragic;
        public int Unacceptable;
    }

    public class QueryTraceData
    {
        public string RequestContext;
        public string Collection;
        public string Query;
        public double Milliseconds;
    }

    public static class Trace
    {
        private const int QueryTimeSlow = 20;
        private const int QueryTimeTragic = 100;
        private const int QueryTimeUnacceptable = 200;

        private static readonly bool _enableProfiler = bool.Parse(ConfigurationManager.AppSettings["mongodb.profiler"] ?? "false");
        private static readonly bool _enableTracing = bool.Parse(ConfigurationManager.AppSettings["mongodb.trace"] ?? "true");
        private static readonly int _traceThreshold = int.Parse(ConfigurationManager.AppSettings["mongodb.trace-threshold"] ?? "0");

        private static Dictionary<string, QueryPerformanceData> _performance = new Dictionary<string, QueryPerformanceData>();
        private static List<QueryTraceData> _traceBuffer = new List<QueryTraceData>();

        public static Dictionary<string, QueryPerformanceData> CollectPerformanceData()
        {
            using (DisposableLock.Lock(_performance))
            {
                var result = _performance;
                _performance = new Dictionary<string, QueryPerformanceData>();
                return result;
            }
        }

        public static List<QueryTraceData> FetchTraceBuffer()
        {
            using (DisposableLock.Lock(_traceBuffer))
            {
                var result = _traceBuffer;
                _traceBuffer = new List<QueryTraceData>();
                return result;
            }
        }

        public static T DoWrappedTrace<T>(TraceDelegate<T> action, string context, string collection, object query)
        {
            // Execute and time action
            var timer = Stopwatch.StartNew();
            var result = action();
            timer.Stop();
            
            // Update request context
            if (HttpContext.Current != null)
            {
                var count = HttpContext.Current.Items["dbcount"] == null ? 0 : Convert.ToInt32(HttpContext.Current.Items["dbcount"]);
                HttpContext.Current.Items["dbcount"] = count + 1;

                var time = HttpContext.Current.Items["dbtime"] == null ? 0 : Convert.ToInt32(HttpContext.Current.Items["dbtime"]);
                HttpContext.Current.Items["dbtime"] = time + timer.ElapsedMilliseconds;
            }

            // Capture profiler information
            if (_enableProfiler)
            {
                var identifier = "{0}.{1}({2})".Merge(collection, context, query == null ? "$all" : query.ToJson(query.GetType(), new JsonWriterSettings { OutputMode = JsonOutputMode.Structural }));
                using (DisposableLock.Lock(_performance))
                {
                    var record = _performance.AcquireKey(identifier);
                    record.Identifier = identifier;
                    record.Milliseconds += timer.Elapsed.TotalMilliseconds;
                    record.Count++;

                    if (timer.Elapsed.TotalMilliseconds > QueryTimeUnacceptable)
                        record.Unacceptable++;
                    else if (timer.Elapsed.TotalMilliseconds > QueryTimeTragic)
                        record.Tragic++;
                    else if (timer.Elapsed.TotalMilliseconds > QueryTimeSlow)
                        record.Slow++;
                }
            }

            if (_enableTracing && timer.ElapsedMilliseconds >= _traceThreshold)
            {
                var httpRequestUrl = string.Empty;
                try
                {
                    if (HttpContext.Current != null)
                        httpRequestUrl = HttpContext.Current.Request.Url.ToString();
                }
                catch (HttpException)
                {
                }

                using (DisposableLock.Lock(_traceBuffer))
                {
                    _traceBuffer.Add(new QueryTraceData()
                    {
                        RequestContext = httpRequestUrl,
                        Collection = collection,
                        Query = "{0}.{1}({2})".Merge(collection, context, query == null ? "$all" : query.ToJson(query.GetType(), new JsonWriterSettings { OutputMode = JsonOutputMode.JavaScript })),
                        Milliseconds = timer.Elapsed.TotalMilliseconds

                    });

                    // Clear the trace buffer if it gets too large, something should be reading it back
                    if (_traceBuffer.Count > 1000)
                        _traceBuffer.Clear();
                }
            }

            return result;
        }
    }
}

#pragma warning restore 1591
// WIRECLUB --------------------------------------------------------------
