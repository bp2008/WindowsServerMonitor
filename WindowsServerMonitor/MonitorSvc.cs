using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServerMonitor
{
	public partial class MonitorSvc : ServiceBase
	{
		WebServer server;
		public MonitorSvc()
		{
			InitializeComponent();
		}

		protected override void OnStart(string[] args)
		{
			server = new WebServer();
			server.SetBindings(WebServer.settings.webPort, WebServer.settings.webPort);
		}

		protected override void OnStop()
		{
			server.Stop();
		}

		public void DoStart()
		{
			OnStart(null);
		}

		public void DoStop()
		{
			OnStop();
		}
	}
}
