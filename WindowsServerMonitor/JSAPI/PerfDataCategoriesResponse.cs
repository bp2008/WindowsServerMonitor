using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServerMonitor.JSAPI
{
	public class PerfDataCategoriesResponse : APIResponse
	{
		public List<PerfDataCategory> categories;
	}
	public class PerfDataCategory
	{
		public string name;
		public string type;
		public string help;

		public PerfDataCategory(PerformanceCounterCategory c)
		{
			name = c.CategoryName;
			type = c.CategoryType.ToString();
			help = c.CategoryHelp;
		}
	}
}
