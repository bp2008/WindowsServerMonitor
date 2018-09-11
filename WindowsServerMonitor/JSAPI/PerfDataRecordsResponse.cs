using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServerMonitor.JSAPI
{
	public class PerfDataRecordsResponse : APIResponse
	{
		public string machineName;
		public List<PerfMonValueCollection> collections;
	}
}
