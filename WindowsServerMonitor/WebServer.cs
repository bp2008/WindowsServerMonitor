using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BPUtil;
using BPUtil.SimpleHttp;
using Newtonsoft.Json;
using WindowsServerMonitor.JSAPI;

namespace WindowsServerMonitor
{
	public class WebServer : HttpServer
	{
		public static string SettingsPath = Globals.ApplicationDirectoryBase + "Settings.cfg";
		public static Settings settings = new Settings();
		object managersLock = new object();
		PerfMonManager[] managers;
		private long rnd = StaticRandom.Next(int.MinValue, int.MaxValue);

		static WebServer()
		{
			settings.Load(SettingsPath);
			settings.SaveIfNoExist(SettingsPath);
			SimpleHttpLogger.RegisterLogger(Logger.httpLogger, false);
		}
		public WebServer() : base()
		{
			lock (managersLock)
			{
				managers = new PerfMonManager[settings.monitors.Count];
				for (int i = 0; i < settings.monitors.Count; i++)
				{
					managers[i] = new PerfMonManager(settings.monitors[i]);
					managers[i].Start();
				}
			}
		}
		public override void handleGETRequest(HttpProcessor p)
		{
			string pageLower = p.Request.Page.ToLower();
			if (pageLower.StartsWith("api/"))
			{
				p.Response.Simple("405 Method Not Allowed");
			}
			else if (p.Request.Page == "")
			{
				p.Response.Redirect("default.html");
			}
			else if (p.Request.Page == "TEST")
			{
				p.Response.Simple("text/plain", string.Join(", ", Process.GetProcessesByName("svchost").Select(i => ProcessHelper.GetUserWhichOwnsProcess(i.Id))));
			}
			else
			{
				string wwwPath = Globals.ApplicationDirectoryBase + "www/";
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached)
					wwwPath = Globals.ApplicationDirectoryBase + "../../www/";
#endif
				DirectoryInfo WWWDirectory = new DirectoryInfo(wwwPath);
				string wwwDirectoryBase = WWWDirectory.FullName.Replace('\\', '/').TrimEnd('/') + '/';
				FileInfo fi = new FileInfo(wwwDirectoryBase + p.Request.Page);
				string targetFilePath = fi.FullName.Replace('\\', '/');
				if (!targetFilePath.StartsWith(wwwDirectoryBase) || targetFilePath.Contains("../"))
				{
					p.Response.Simple("400 Bad Request");
					return;
				}
				if (!fi.Exists)
					return;
				if ((fi.Extension == ".html" || fi.Extension == ".htm") && fi.Length < 256000)
				{
					string html = File.ReadAllText(fi.FullName);
					html = html.Replace("%%VERSION%%", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
					html = html.Replace("%%RND%%", rnd.ToString());

					byte[] data = Encoding.UTF8.GetBytes(html);
					p.Response.FullResponseBytes(data, Mime.GetMimeType(fi.Extension));
					if (!p.Request.GetBoolParam("nocompress"))
						p.Response.CompressResponseIfCompatible();
				}
				else
				{
					string mime = Mime.GetMimeType(fi.Extension);
					if (pageLower.StartsWith(".well-known/acme-challenge/"))
						mime = "text/plain";
					p.Response.StaticFile(fi);
				}
			}
		}

		private List<KeyValuePair<string, string>> GetCacheLastModifiedHeaders(TimeSpan maxAge, DateTime lastModifiedUTC)
		{
			List<KeyValuePair<string, string>> additionalHeaders = new List<KeyValuePair<string, string>>();
			additionalHeaders.Add(new KeyValuePair<string, string>("Cache-Control", "max-age=" + (long)maxAge.TotalSeconds + ", public"));
			additionalHeaders.Add(new KeyValuePair<string, string>("Last-Modified", lastModifiedUTC.ToString("R")));
			return additionalHeaders;
		}

		public override void handlePOSTRequest(HttpProcessor p)
		{
			string pageLower = p.Request.Page.ToLower();
			if (pageLower.StartsWith("api/"))
			{
				JSAPI.APIResponse apiResponse = null;
				string cmd = p.Request.Page.Substring("api/".Length);
				if (cmd.StartsWith("PerformanceCounterCategoryDetails/"))
				{
					string name = Uri.UnescapeDataString(cmd.Substring("PerformanceCounterCategoryDetails/".Length));
					try
					{
						PerformanceCounterCategory cat = new PerformanceCounterCategory(name);
						apiResponse = new CategoryDetailsResponse(cat);
					}
					catch (Exception ex)
					{
						apiResponse = new APIResponse(ex.ToString());
					}
				}
				else
				{
					switch (cmd)
					{
						case "getCounterRecords":
							{
								JSAPI.PerfDataRecordsResponse response = new JSAPI.PerfDataRecordsResponse();
								response.machineName = Environment.MachineName;
								lock (managersLock)
								{
									response.collections = new List<PerfMonValueCollection>(managers.Length);

									long time = p.Request.GetLongParam("time");
									for (int i = 0; i < managers.Length; i++)
										response.collections.Add(new PerfMonValueCollection(managers[i], time));
								}
								apiResponse = response;
							}
							break;
						case "getPerformanceCounterCategories":
							{
								PerformanceCounterCategory[] categories = PerformanceCounterCategory.GetCategories();
								PerfDataCategoriesResponse response = new PerfDataCategoriesResponse();
								response.categories = new List<PerfDataCategory>();
								foreach (PerformanceCounterCategory c in categories)
								{
									try
									{
										response.categories.Add(new PerfDataCategory(c));
									}
									catch { }
								}
								response.categories.Sort(new Comparison<PerfDataCategory>((a, b) => string.Compare(a.name, b.name)));
								apiResponse = response;
							}
							break;

					}
				}
				if (apiResponse == null)
					apiResponse = new JSAPI.APIResponse("Response was null, so this response was generated instead.");
				p.Response.FullResponseUTF8(JsonConvert.SerializeObject(apiResponse), "application/json");
				if (!p.Request.GetBoolParam("nocompress"))
					p.Response.CompressResponseIfCompatible();
			}
		}

		protected override void stopServer()
		{
			lock (managersLock)
			{
				for (int i = 0; i < managers.Length; i++)
					managers[i].Stop();
			}
		}
	}
}
