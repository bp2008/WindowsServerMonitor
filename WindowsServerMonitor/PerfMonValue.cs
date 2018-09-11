using System;
using BPUtil;

namespace WindowsServerMonitor
{
	public class PerfMonValue
	{
		/// <summary>
		/// Time in milliseconds since the unix epoch.
		/// </summary>
		public long Time;
		/// <summary>
		/// The value of the counter.
		/// </summary>
		public double Value;
	}
}