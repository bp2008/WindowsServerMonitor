using System;
using System.Collections.Generic;

namespace WindowsServerMonitor
{
	public class PerfMonValueCollection
	{
		public string name;
		public double scale;
		public int keepTimeMs;
		public string graphId;
		public IEnumerable<PerfMonValue> values;
		public PerfMonValueCollection() { }
		public PerfMonValueCollection(PerfMonManager m, long time = 0)
		{
			this.name = m.spec.GetLabel();
			this.scale = m.spec.GetScale();
			this.keepTimeMs = m.spec.GetKeepTimeMs();
			this.graphId = m.spec.graphId;
			this.values = time <= 0 ? m.GetValues() : m.GetValues(time);
		}
	}
}