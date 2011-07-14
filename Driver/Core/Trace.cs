using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;
using AOD;
using MongoDB.Bson;
using MongoDB.Bson.IO;

namespace MongoDB.Driver.Core
{
    public delegate T TraceDelegate<out T>();

    public class QueryPerformanceRecord
    {
        public string Identifier;
        public double Milliseconds;
        public int Count;
    }

    public static class Trace
    {
        private static readonly bool _enableTracing = bool.Parse(ConfigurationManager.AppSettings["mongodb.trace"] ?? "true");
        private static readonly int _traceThreshold = int.Parse(ConfigurationManager.AppSettings["mongodb.trace-threshold"] ?? "20");
        private static Dictionary<string, QueryPerformanceRecord> _performance = new Dictionary<string, QueryPerformanceRecord>();

        public static Dictionary<string, QueryPerformanceRecord> CollectPerformanceData()
        {
            using (DisposableLock.Lock(_performance))
            {
                var result = _performance;
                _performance = new Dictionary<string, QueryPerformanceRecord>();
                return result;
            }
        }

        public static T DoWrappedTrace<T>(TraceDelegate<T> action, string context, string collection, object query)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var result = action();
            stopwatch.Stop();

            if (HttpContext.Current != null)
            {
                var count = HttpContext.Current.Items["dbcount"] == null ? 0 : Convert.ToInt32(HttpContext.Current.Items["dbcount"]);
                HttpContext.Current.Items["dbcount"] = count + 1;

                var time = HttpContext.Current.Items["dbtime"] == null ? 0 : Convert.ToInt32(HttpContext.Current.Items["dbtime"]);
                HttpContext.Current.Items["dbtime"] = time + stopwatch.ElapsedMilliseconds;
            }

            var identifier = "{0} - {1}".Merge(collection, query == null ? "all" : query.ToJson(query.GetType(), new JsonWriterSettings { OutputMode = JsonOutputMode.Structural }));
            identifier = Regex.Replace(identifier, "(\\[.*\\])", "@array");

            using (DisposableLock.Lock(_performance))
            {
                var record = _performance.AcquireKey(identifier);
                record.Identifier = identifier;
                record.Milliseconds += stopwatch.ElapsedMilliseconds;
                record.Count++;
            }

            if (_enableTracing && stopwatch.ElapsedMilliseconds >= _traceThreshold)
                Debug.WriteLine(string.Format("[Mongo:{0}] {1}ms {2} {3}", context, stopwatch.ElapsedMilliseconds, collection, query));

            return result;
        }
    }
}
