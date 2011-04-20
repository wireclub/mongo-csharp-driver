using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MongoDB.Driver.Core
{
	public	delegate T TraceDelgate<T>();

	public static class Trace
	{
		public static T DoWrappedTrace<T>(TraceDelgate<T> action, string context, string collection, object query)
		{
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			var result = action();
			Debug.WriteLine("[Mongo:" + context + "] " + stopwatch.ElapsedMilliseconds + "ms " + collection + " " + query);
			//Debug.WriteLine(new StackTrace());
			return result;
		}
	}
}
