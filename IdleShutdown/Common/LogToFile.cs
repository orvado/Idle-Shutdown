using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Orvado.IdleShutdown.Common
{
	public static class LogToFile
	{
		public static void Error(Exception ex)
		{
			WriteEntry(ex.ToString(), "ERROR");
		}

		public static void Error(string message, params object[] parameters)
		{
			if (parameters.Length > 0)
			{
				message = String.Format(message, parameters);
			}
			WriteEntry(message, "ERROR");
		}

		public static void Warning(string message, params object[] parameters)
		{
			if (parameters.Length > 0)
			{
				message = String.Format(message, parameters);
			}
			WriteEntry(message, "WARN");
		}

		public static void Info(string message, params object[] parameters)
		{
			if (parameters.Length > 0)
			{
				message = String.Format(message, parameters);
			}
			WriteEntry(message, "INFO");
		}

		public static void SkipLine()
		{
			lock (LockInstance)
			{
				Trace.WriteLine(String.Empty);
			}
		}

		private static void WriteEntry(string message, string type)
		{
			lock (LockInstance)
			{
				Trace.WriteLine(
					string.Format("{0}  {1,-7}{2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
						type + ":", message));
			}
		}

		private static readonly object LockInstance = new object();
	}
}
