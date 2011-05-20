using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Web;

namespace MongoDB.Driver.Core
{
	public delegate T TraceDelegate<T>();

	public static class Trace
	{
		public static bool EnableTracing = bool.Parse(ConfigurationManager.AppSettings["mongodb.trace"] ?? "false");

		public static T DoWrappedTrace<T>(TraceDelgate<T> action, string context, string collection, object query)
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

            if(EnableTracing && stopwatch.ElapsedMilliseconds > 10)
            {
                Debug.WriteLine("[Mongo:" + context + "] " + stopwatch.ElapsedMilliseconds + "ms " + collection);
                // Debug.WriteLine(new StackTrace());
            }

			return result;
		}
	}

    public class CachedCursor<T> : MongoCursor<T>
    {
        private List<T> _cached;
        private readonly Stopwatch _time = new Stopwatch();
        
        public long Milliseconds
        {
            get
            {
                return _time.ElapsedMilliseconds;
            }
        }

        public CachedCursor(MongoCollection collection, IMongoQuery query) : base(collection, query)
        {
        }

        public override IEnumerator<T> GetEnumerator()
        {
            _time.Start();
            if (_cached == null)
            {
                _cached = new List<T>();
                var e = base.GetEnumerator();
                while (e.MoveNext())
                    _cached.Add(e.Current);
            }
            _time.Stop();

            if (HttpContext.Current != null)
            {
                var current = HttpContext.Current.Items["enumtime"] == null ? 0 : Convert.ToInt32(HttpContext.Current.Items["enumtime"]);
                HttpContext.Current.Items["enumtime"] = current + _time.ElapsedMilliseconds;
            }

            return _cached.GetEnumerator();
        }
    }
}
