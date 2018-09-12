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
		public WebServer() : base(settings.webPort)
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
			string pageLower = p.requestedPage.ToLower();
			if (pageLower.StartsWith("api/"))
			{
				p.writeFailure("405 Method Not Allowed");
			}
			else if (p.requestedPage == "")
			{
				p.writeRedirect("default.html");
			}
			else if (p.requestedPage == "TEST")
			{
				StringBuilder sb = new StringBuilder();
				p.writeSuccess("text/plain");
				p.outputStream.Write(string.Join(", ", Process.GetProcessesByName("svchost").Select(i => ProcessHelper.GetUserWhichOwnsProcess(i.Id))));
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
				FileInfo fi = new FileInfo(wwwDirectoryBase + p.requestedPage);
				string targetFilePath = fi.FullName.Replace('\\', '/');
				if (!targetFilePath.StartsWith(wwwDirectoryBase) || targetFilePath.Contains("../"))
				{
					p.writeFailure("400 Bad Request");
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
					if (!p.GetBoolParam("nocompress"))
						p.CompressResponseIfCompatible();
					p.writeSuccess(Mime.GetMimeType(fi.Extension));
					p.outputStream.Flush();
					p.tcpStream.Write(data, 0, data.Length);
					p.tcpStream.Flush();
				}
				else
				{
					string mime = Mime.GetMimeType(fi.Extension);
					if (pageLower.StartsWith(".well-known/acme-challenge/"))
						mime = "text/plain";
					if (fi.LastWriteTimeUtc.ToString("R") == p.GetHeaderValue("if-modified-since"))
					{
						p.writeSuccess(mime, -1, "304 Not Modified");
						return;
					}
					if (!p.GetBoolParam("nocompress"))
						p.CompressResponseIfCompatible();
					p.writeSuccess(mime, additionalHeaders: GetCacheLastModifiedHeaders(TimeSpan.FromHours(1), fi.LastWriteTimeUtc));
					p.outputStream.Flush();
					using (FileStream fs = fi.OpenRead())
					{
						fs.CopyTo(p.tcpStream);
					}
					p.tcpStream.Flush();
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

		public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
		{
			string pageLower = p.requestedPage.ToLower();
			if (pageLower.StartsWith("api/"))
			{
				JSAPI.APIResponse apiResponse = null;
				string cmd = p.requestedPage.Substring("api/".Length);
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

									long time = p.GetLongParam("time");
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
				if (!p.GetBoolParam("nocompress"))
					p.CompressResponseIfCompatible();
				p.writeSuccess("application/json");
				if (apiResponse == null)
					apiResponse = new JSAPI.APIResponse("Response was null, so this response was generated instead.");
				p.outputStream.Write(JsonConvert.SerializeObject(apiResponse));
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
