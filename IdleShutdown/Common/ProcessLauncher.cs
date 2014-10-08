using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;

namespace Orvado.IdleShutdown.Common
{
	public static class ProcessLauncher
	{
		public static string Output { get; private set; }

		private static Process process;
		private static ProcessStartInfo processInfo;
		private static ProcessStartInfo ProcessInfo
		{
			get
			{
				return processInfo ?? (processInfo = new ProcessStartInfo
				{
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					Verb = "runas"
				});
			}
		}

		public static void ExecuteCommandLine(string executableName, string argumentToExecute,
			string workingDirectory)
		{
			const int timeOut = 5 * 60 * 1000;

			LogToFile.Info("Execute: {0} {1}", executableName, argumentToExecute);
			ProcessInfo.FileName = executableName;
			ProcessInfo.Arguments = argumentToExecute;
			if (workingDirectory != null)
			{
				ProcessInfo.WorkingDirectory = workingDirectory;
			}
			process = Process.Start(ProcessInfo);
			if (process != null)
			{
				Output = process.StandardOutput.ReadToEnd();
				process.WaitForExit(timeOut);

				if (!process.HasExited)
				{
					if (process.Responding)
					{
						process.CloseMainWindow();
					}
					else
					{
						process.Kill();
						LogToFile.Error("Process timed out after 5 minutes.");
					}
				}
				if (process.ExitCode != 0)
				{
					LogToFile.Info("\r\nTotal time spend: " + process.TotalProcessorTime.TotalSeconds +
					               "\r\nExit Code: " + process.ExitCode + "\r\n" + argumentToExecute + "\r\n" +
					               "\r\nOutput:\r\n" + Output);
				}
			}
		}

		private static bool EndLocalProcesses(Process[] localProcesses, bool closeWindowFirst)
		{
			string processTitle = "NONE";
			bool success = true;
			try
			{
				if (closeWindowFirst)
				{
					foreach (Process p in localProcesses)
					{
						processTitle = p.ProcessName + " (" + p.Id + ")";
						if (p.Responding)
						{
							LogToFile.Info("Requesting to close main window for process {0} (id = {1})",
										   p.ProcessName, p.Id);
							p.CloseMainWindow();
						}
						else
						{
							LogToFile.Info("Process {0} (id = {1}) is not responding", p.ProcessName, p.Id);
						}
					}
				}

				Thread.Sleep(200);
				foreach (Process p in localProcesses)
				{
					processTitle = p.ProcessName + " (" + p.Id + ")";
					p.Refresh();
					if (!p.HasExited)
					{
						LogToFile.Info("Killing process {0} (id = {1})", p.ProcessName, p.Id);
						p.Kill();
					}
				}
			}
			catch (NotSupportedException ex)
			{
				LogToFile.Info("Unable to kill process \"{0}\"\r\n{1}", processTitle, ex.ToString());
				success = false;
			}
			catch (InvalidOperationException ex)
			{
				LogToFile.Error("Unable to kill process \"{0}\"\r\n{1}", processTitle, ex.ToString());
				success = false;
			}
			return success;
		}

		/// <summary>
		/// Kill all processes matching the name "processName".  This name should
		/// not include the extension (like ".exe")
		/// </summary>
		/// <param name="processId">ID of the process to kill</param>
		/// <param name="closeWindowFirst">Attempt the close window first before killing the process</param>
		private static bool KillProcessById(int processId, bool closeWindowFirst)
		{
			bool success;
			try
			{
				LogToFile.Info("Killing processes matching ID \"{0}\"", processId);

				// Get all instances of process (matching processName)
				Process localProcess = Process.GetProcessById(processId);

				success = EndLocalProcesses(new[] { localProcess }, closeWindowFirst);
			}
			catch (Exception ex)
			{
				LogToFile.Error("Unable to kill processes by ID \"{0}\"\r\n{1}",
							 processId, ex.ToString());
				success = false;
			}

			// check that the process is gone
			if (success)
			{
				try
				{
					Process checkProcess = null;
					for (int count = 0; count < 10; count++)
					{
						Thread.Sleep(200);
						checkProcess = Process.GetProcessById(processId);
					}

					LogToFile.Error("Error checking process was ended \"{0}\"\r\n    {1}",
						processId, (checkProcess != null) ? checkProcess.ToString() : "(NULL)");
					success = false;
				}
				catch (ArgumentException)
				{
					//Expected result because the ProcessID no longer exists
				}
				catch (Exception ex)
				{
					LogToFile.Error("Unable to get processes by ID \"{0}\"\r\n{1}",
								 processId, ex.ToString());
					success = false;
				}
			}
			return success;
		}

		private static string GetProcessUserName(string procName)
		{
			string query = "SELECT * FROM Win32_Process WHERE Name = \'" + procName + ".exe\'";
			var procs = new ManagementObjectSearcher(query);
			foreach (ManagementBaseObject o in procs.Get())
			{
				var p = (ManagementObject) o;
				var path = p["ExecutablePath"];
				if (path != null)
				{
					string executablePath = path.ToString();
					object[] ownerInfo = new object[2];
					p.InvokeMethod("GetOwner", ownerInfo);
					return (string) ownerInfo[0];
				}
			}
			return null;
		}

		public static void KillProcessesMatchingLoggedOnUser()
		{
			string loggedOnUser = Environment.UserName;
			if (String.IsNullOrEmpty(loggedOnUser))
			{
				LogToFile.Error("Unable to kill user process because current logged on user is empty");
				return;
			}
			LogToFile.Info("Killing all processes owned by user \"{0}\"", loggedOnUser);

			int attemptCount = 0;
			int successCount = 0;
			string pattern = @"^.*" + Regex.Escape(loggedOnUser) + @".*$";
			Process[] runningProcesses = Process.GetProcesses();
			foreach (Process p in runningProcesses)
			{
				if (p.ProcessName.StartsWith("shutdown", StringComparison.InvariantCulture))
				{
					continue;
				}
				string processOwner = GetProcessUserName(p.ProcessName) ?? "";
				if (Regex.IsMatch(processOwner, pattern, RegexOptions.IgnoreCase))
				{
					bool success = KillProcessById(p.Id, true);
					attemptCount++;
					successCount += (success ? 1 : 0);
				}
			}

			LogToFile.Info("Successfully killed {0} processes out of {1} owned by user \"{2}\"",
				successCount, attemptCount, loggedOnUser);
		}
	}
}