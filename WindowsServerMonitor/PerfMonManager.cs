using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using BPUtil;

namespace WindowsServerMonitor
{
	/// <summary>
	/// Manages one PerformanceCounter instance and its recent history.
	/// </summary>
	public class PerfMonManager
	{
		public readonly PerfMonSpec spec;
		/// <summary>
		/// A public error property that is used to report recurring errors and reduce log spam.
		/// </summary>
		public string Error { get; private set; }
		private Thread monitorThread;
		/// <summary>
		/// A doubly-linked list (not thread safe!) containing performance counter values where the first value is the newest and each next value gets progressively older.
		/// </summary>
		public LinkedList<PerfMonValue> values = new LinkedList<PerfMonValue>();
		/// <summary>
		/// The maximum age of items to keep, in milliseconds.
		/// </summary>
		private int maxAge;
		public PerfMonManager(PerfMonSpec spec)
		{
			this.spec = spec;
			this.maxAge = spec.GetKeepTimeMs();
		}
		public void Start()
		{
			lock (this)
			{
				Stop();
				monitorThread = new Thread(monitorRunner);
				monitorThread.Name = "PerfMonManager " + spec.GetLabel();
				monitorThread.IsBackground = true;
				monitorThread.Start();
			}
		}

		public void Stop()
		{
			lock (this)
			{
				if (monitorThread != null)
				{
					monitorThread.Abort();
					monitorThread = null;
				}
			}
		}

		/// <summary>
		/// Loops until this instance is stopped, gathering performance data at the specified interval.
		/// </summary>
		private void monitorRunner()
		{
			try
			{
				int intervalMs = spec.GetIntervalMs();
				//lock (this)
				//{
				//	DateTime now = DateTime.Now;
				//	DateTime date = now.AddMilliseconds(-1 * maxAge);
				//	while (date < now)
				//	{
				//		PerfMonValue pmv = MakeFakeValue(date);
				//		values.AddFirst(pmv);
				//		while (values.Last.Value.Time < pmv.Time - maxAge)
				//			values.RemoveLast();
				//		date = date.AddMilliseconds(intervalMs);
				//	}
				//}
				while (true)
				{
					try
					{
						string instanceName = spec.GetInstanceName();
						if (instanceName == null && spec.processFinder != null)
						{
							Error = "Failed to find Process instance name for monitor \"" + spec.GetLabel() + "\"";
							Thread.Sleep(Math.Max(intervalMs, 10000));
							continue;
						}
						using (PerformanceCounter counter = new PerformanceCounter(spec.categoryName, spec.counterName, instanceName, true))
						{
							counter.NextValue();
							Thread.Sleep(intervalMs);
							while (true)
							{
								PerfMonValue pmv = new PerfMonValue();
								pmv.Value = counter.NextValue();
								pmv.Time = TimeUtil.GetTimeInMsSinceEpoch();

								lock (this)
								{
									values.AddFirst(pmv);
									while (values.Last.Value.Time < pmv.Time - maxAge)
										values.RemoveLast();
								}

								Error = "";

								Thread.Sleep(intervalMs);
							}
						}
					}
					catch (ThreadAbortException) { }
					catch (Exception ex)
					{
						Error = "Data collection error for monitor \"" + spec.GetLabel() + "\": " + ex.ToString();
						Thread.Sleep(Math.Max(intervalMs, 10000));
					}
				}
			}
			catch (ThreadAbortException) { }
			catch (Exception ex)
			{
				Error = "Data collection for monitor \"" + spec.GetLabel() + "\" has ended: " + ex.ToString();
				Logger.Debug(ex);
			}
			Error = "Data collection has ended.";
		}

		private PerfMonValue MakeFakeValue(DateTime date)
		{
			return new PerfMonValue() { Time = TimeUtil.GetTimeInMsSinceEpoch(date), Value = StaticRandom.Next(0, 100) };
		}

		/// <summary>
		/// Returns all records.
		/// </summary>
		/// <returns></returns>
		public List<PerfMonValue> GetValues()
		{
			lock (this)
			{
				return values.ToList();
			}
		}
		/// <summary>
		/// Returns all records created after the specified time.
		/// </summary>
		/// <param name="numItems"></param>
		/// <returns></returns>
		public List<PerfMonValue> GetValues(long time)
		{
			lock (this)
			{
				List<PerfMonValue> l = new List<PerfMonValue>();
				LinkedListNode<PerfMonValue> v = values.First;
				while (v != null && time < v.Value.Time)
				{
					l.Add(v.Value);
					v = v.Next;
				}
				return l;
			}
		}
	}
}