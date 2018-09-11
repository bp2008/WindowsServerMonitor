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
				monitorThread.Name = "PerfMonManager " + spec.categoryName + ": " + spec.counterName + " (" + spec.instanceName + ")";
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
				int checkInstanceNameCounter = 0;
				int intervalMs = spec.GetIntervalMs();
				while (true)
				{
					try
					{
						string instanceName = spec.GetInstanceName();
						if (instanceName == null && spec.processFinder != null)
						{
							Logger.Info("Failed to find instance name for monitor " + spec.GetLabel());
							Thread.Sleep(Math.Max(intervalMs, 10000));
							continue;
						}
						using (PerformanceCounter counter = new PerformanceCounter(spec.categoryName, spec.counterName, instanceName, true))
						{
							counter.NextValue();
							Thread.Sleep(intervalMs);
							while (true)
							{
								if (spec.processFinder != null && ++checkInstanceNameCounter % 15 == 0 && spec.GetInstanceName() != counter.InstanceName)
									break; // Breaking here will cause the PerformanceCounter to be reinitialized.  This will catch process restarts.
								PerfMonValue pmv = new PerfMonValue();
								pmv.Time = (TimeUtil.GetTimeInMsSinceEpoch() / 100) * 100; // Round off to in 100ms interals so the graph tooltip works a little better.
								pmv.Value = counter.NextValue();

								lock (this)
								{
									values.AddFirst(pmv);
									while (values.Last.Value.Time < pmv.Time - maxAge)
										values.RemoveLast();
								}

								Thread.Sleep(intervalMs);
							}
						}
					}
					catch (ThreadAbortException) { }
					catch (Exception ex)
					{
						Logger.Debug(ex);
					}
				}
			}
			catch (ThreadAbortException) { }
			catch (Exception ex)
			{
				Logger.Debug(ex);
			}
		}
		/// <summary>
		/// Returns all records.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<PerfMonValue> GetValues()
		{
			lock (this)
			{
				return values;
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