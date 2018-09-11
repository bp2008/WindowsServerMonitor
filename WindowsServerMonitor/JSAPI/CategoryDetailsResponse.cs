using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServerMonitor.JSAPI
{
	public class CategoryDetailsResponse : APIResponse
	{
		public CategoryDetails details;
		public CategoryDetailsResponse(PerformanceCounterCategory cat) : base()
		{
			this.details = new CategoryDetails(cat);
		}
	}
	public class CategoryDetails
	{
		public string[] instances;
		public PCCounter[] counters;

		public CategoryDetails(PerformanceCounterCategory cat)
		{
			instances = cat.GetInstanceNames();
			if (instances.Any())
			{
				foreach (string instanceName in instances)
				{
					if (cat.InstanceExists(instanceName))
					{
						counters = cat.GetCounters(instanceName).Select(c => new PCCounter(c.CounterName, c.CounterHelp)).ToArray();
						break;
					}
				}
			}
			else
			{
				counters = cat.GetCounters().Select(c => new PCCounter(c.CounterName, c.CounterHelp)).ToArray();
			}
		}
	}
	public class PCCounter
	{
		public string name;
		public string help;
		public PCCounter(string name, string help)
		{
			this.name = name;
			this.help = help;
		}
	}
}
