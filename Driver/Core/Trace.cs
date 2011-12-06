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

    public class QueryPerformanceData
    {
        public string Identifier;
        public double Milliseconds;
        public int Count;
        public int Slow;
        public int Tragic;
        public int Unacceptable;
    }

    public static class Trace
    {
        private static readonly bool _enableTracing = bool.Parse(ConfigurationManager.AppSettings["mongodb.trace"] ?? "true");
        private static readonly int _traceThreshold = int.Parse(ConfigurationManager.AppSettings["mongodb.trace-threshold"] ?? "0");

        private const int QueryTimeSlow = 20;
        private const int QueryTimeTragic = 100;
        private const int QueryTimeUnacceptable = 200;

        private static Dictionary<string, QueryPerformanceData> _performance = new Dictionary<string, QueryPerformanceData>();
        
        public static Dictionary<string, QueryPerformanceData> CopyPerformanceData()
        {
            using (DisposableLock.Lock(_performance))
                return new Dictionary<string, QueryPerformanceData>(_performance);
        }

        public static Dictionary<string, QueryPerformanceData> CollectPerformanceData()
        {
            using (DisposableLock.Lock(_performance))
            {
                var result = _performance;
                _performance = new Dictionary<string, QueryPerformanceData>();
                return result;
            }
        }

        public static T DoWrappedTrace<T>(TraceDelegate<T> action, string context, string collection, object query)
        {
            // If not logged in or no httpcontext
            

            // Execute and time action
            var timer = Stopwatch.StartNew();
            var result = action();
            timer.Stop();

            // Update request context
            if (HttpContext.Current != null)
            {
                var count = HttpContext.Current.Items["dbcount"] == null ? 0 : Convert.ToInt32(HttpContext.Current.Items["dbcount"]);
                var time = HttpContext.Current.Items["dbtime"] == null ? 0 : Convert.ToInt32(HttpContext.Current.Items["dbtime"]);

                HttpContext.Current.Items["dbcount"] = count + 1;
                HttpContext.Current.Items["dbtime"] = time + timer.ElapsedMilliseconds;
            }

            // Generate identifier
            var identifier = "{0} - {1}".Merge(collection, query == null ? "all" : query.ToJson(query.GetType(), new JsonWriterSettings { OutputMode = JsonOutputMode.Structural }));
            identifier = Regex.Replace(identifier, "(\\[.*\\])", "@array");

            // Increment times
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

			if (_enableTracing && timer.ElapsedMilliseconds >= _traceThreshold)
				Debug.WriteLine(string.Format("[Mongo:{0}] {1}ms {2} {3}", context, timer.ElapsedMilliseconds, collection, query));

            return result;
        }
    }
}
