using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MongoDB.Driver.Core
{
	public	delegate T TraceDelgate<T>();

	public static class Trace
	{
		public static bool EnableTracing = bool.Parse(ConfigurationManager.AppSettings["mongodb.trace"] ?? "false");

		public static T DoWrappedTrace<T>(TraceDelgate<T> action, string context, string collection, object query)
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			var result = action();
			if (EnableTracing)
			{
				Debug.WriteLine("[Mongo:" + context + "] " + stopwatch.ElapsedMilliseconds + "ms " + collection + " " + query);
				//Debug.WriteLine(new StackTrace());
			}
			return result;
		}
	}
}
