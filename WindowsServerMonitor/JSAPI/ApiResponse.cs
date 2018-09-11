using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsServerMonitor.JSAPI
{
	public class APIResponse
	{
		public string result;
		public string error = "";
		/// <summary>
		/// Creates a "success" response.
		/// </summary>
		public APIResponse()
		{
			result = "success";
		}
		/// <summary>
		/// Creates an "error" response with the specified error message.
		/// </summary>
		/// <param name="error">The error message to return.</param>
		public APIResponse(string error)
		{
			result = "error";
			this.error = error;
		}
	}
}
