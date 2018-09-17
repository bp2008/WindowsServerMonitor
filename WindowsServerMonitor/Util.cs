using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServerMonitor
{
	public static class Util
	{
		public static string GetProcessInstanceName(int pid)
		{
			return GetProcessInstanceName(Process.GetProcessById(pid));
		}
		public static string GetProcessInstanceName(Process process)
		{
			string processName = Path.GetFileNameWithoutExtension(process.ProcessName);

			PerformanceCounterCategory cat = new PerformanceCounterCategory("Process");

			string[] instances = cat.GetInstanceNames().Where(inst => inst.StartsWith(processName)).ToArray();

			foreach (string instance in instances)
			{
				using (PerformanceCounter cnt = new PerformanceCounter("Process",
					 "ID Process", instance, true))
				{
					int val = (int)cnt.RawValue;
					if (val == process.Id)
					{
						return instance;
					}
				}
			}
			throw new Exception("Could not find performance counter " +
				"instance name for current process. This is truly strange ...");
		}
	}
}
