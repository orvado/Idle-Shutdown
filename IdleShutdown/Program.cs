using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using Orvado.IdleShutdown.Common;

namespace Orvado.IdleShutdown
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
				// This used to run the service as a console (development phase only)
				var idleService = new IdleService();
				idleService.Start();

				Console.WriteLine(@"Press Enter to terminate ...");
				Console.ReadLine();

				idleService.DoStop();
				Console.WriteLine(@"Service terminated.");
				Console.ReadLine();
			}
			else
			{
				ServiceBase[] servicesToRun =
				{
					new IdleService()
				};
				ServiceBase.Run(servicesToRun);
			}
		}
	}
}
