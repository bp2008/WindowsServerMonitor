using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BPUtil.Forms;

namespace WindowsServerMonitor
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static void Main()
		{
			if (Environment.UserInteractive)
			{
				string Title = "Windows Server Monitor " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " Service Manager";
				string ServiceName = "WindowsServerMonitor";

				MonitorSvc svcTestRun = null;
				if (Debugger.IsAttached)
				{
					svcTestRun = new MonitorSvc();
					svcTestRun.DoStart();
				}

				ButtonDefinition btnLaunch = new ButtonDefinition("Launch in Browser", btnLaunch_Click);
				ButtonDefinition btnLoadDefaultSettings = new ButtonDefinition("Default Settings", btnLoadDefaultSettings_Click);
				ButtonDefinition[] customButtons = new ButtonDefinition[] { btnLaunch, btnLoadDefaultSettings };

				System.Windows.Forms.Application.Run(new ServiceManager(Title, ServiceName, customButtons));

				svcTestRun?.DoStop();
			}
			else
			{
				ServiceBase[] ServicesToRun;
				ServicesToRun = new ServiceBase[]
				{
				new MonitorSvc()
				};
				ServiceBase.Run(ServicesToRun);
			}
		}

		private static void btnLaunch_Click(object sender, EventArgs e)
		{
			Process.Start("http://" + IPAddress.Loopback.ToString() + ":" + WebServer.settings.webPort + "/default.html");
		}

		private static void btnLoadDefaultSettings_Click(object sender, EventArgs e)
		{
			if (MessageBox.Show("Reset to defaults?", "Reset all settings to defaults? (requires service restart to take effect)", MessageBoxButtons.YesNo) == DialogResult.Yes)
			{
				WebServer.settings = new Settings();
				WebServer.settings.LoadDefaultMonitors();
				WebServer.settings.Save(WebServer.SettingsPath);
			}
		}
	}
}
