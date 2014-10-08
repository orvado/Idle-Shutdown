using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Orvado.IdleShutdown.Common;

namespace Orvado.IdleShutdown
{
	public partial class IdleService : ServiceBase
	{
		public IdleService()
		{
			InitializeComponent();
			ProcessState = ServiceState.None;
		}

		#region Service Events

		protected override void OnStart(string[] args)
		{
			LogToFile.Info("Service start requested, ending the service");
			Start();
		}

		public void Start()
		{
			//if (!Environment.UserInteractive)
			//{
			//	RequestAdditionalTime(120000);
			//}
			LogToFile.Info("Start method called");
			myServiceStatus.currentState = (int)State.SERVICE_START_PENDING;
			SetServiceStatus(ServiceHandle, ref myServiceStatus);
			LogToFile.Info("Service status set to pending");

			//if (String.IsNullOrEmpty(Settings.Default.SqlDatabaseName))
			//{
			//	LogEvent.InfoLimited("Backup database name is not set in config file, " +
			//						 "unable to continue", new TimeSpan(1, 0, 0));
			//}
			//else
			if (workerThread == null ||
				threadStatus == ThreadStatus.None ||
				threadStatus == ThreadStatus.Stopped)
			{
				// Start a separate thread that does the actual work.
				try
				{
					LogToFile.Info("Creating worker thread");
					workerThread = new Thread(ServiceWorkerMethod);
					workerThread.Start();
					LogToFile.Info("Started worker thread");
				}
				catch (Exception ex)
				{
					LogEvent.Exception(ex, ESeverityLevel.SeverityError);
				}
			}
			else
			{
				LogEvent.Info("Unable to start thread, due to following:" + Environment.NewLine +
					"\tWorker thread NULL: " + (workerThread == null ? "True" : "False") + Environment.NewLine +
					"\tThread status: " + threadStatus);
			}
			if (workerThread != null)
			{
				LogEvent.Info("Start - Worker thread state: {0}\r\nThread status: {1}",
					workerThread.ThreadState.ToString(), threadStatus.ToString());
			}
			myServiceStatus.currentState = (int)State.SERVICE_RUNNING;
			SetServiceStatus(ServiceHandle, ref myServiceStatus);
		}

		private enum ServiceState
		{
			None = 0,
			IdleWait,
			ShutdownWait,
			KillUserProcess
		}
		private ServiceState ProcessState { get; set; }
		private DateTime WaitUntil { get; set; }

		public void ServiceWorkerMethod()
		{
			try
			{
				LogToFile.Info("Service worker running"); 
				bool endThread = false;

				threadStatus = ThreadStatus.Running;
				int sleepSeconds;
				int secondsToSleep = Int32.TryParse(Resources.WorkerThreadIntervalSeconds, out sleepSeconds)
										 ? sleepSeconds
										 : 60;

				LogToFile.Info("IdleService was started successfully", ESeverityLevel.SeverityInfo);
				ProcessState = ServiceState.IdleWait;
				WaitUntil = DateTime.MinValue;
				int idleMinimumSeconds = Settings.Default.IdleBeforeShutdown * 60;

				while (!endThread)
				{
					if (pauseEvent.WaitOne(0))
					{
						threadStatus = ThreadStatus.Paused;
						LogEvent.Info("Pause signal received at " + DateTime.Now);
					}
					else if (threadStatus != ThreadStatus.Paused)
					{
						//if (somethingHasFailed)
						//{
						//	endThread = true;
						//}

						switch (ProcessState)
						{
							case ServiceState.IdleWait:
								if (WaitUntil != DateTime.MinValue && DateTime.Now < WaitUntil)
								{
									break;
								}
								uint secondsIdle = IdleDetection.GetLastInputTime();
								if (secondsIdle > idleMinimumSeconds)
								{
									LogToFile.Info("Exceeded idle time limit of " + idleMinimumSeconds + Environment.NewLine +
									               "Calling shutdown to halt the computer");
									string systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
									string shutdownExe = Path.Combine(systemFolder, "shutdown.exe");

									if (File.Exists(shutdownExe))
									{
										LogEvent.Info("Executing shutdown after {0} seconds of inactivity.",
											secondsIdle);

										ProcessLauncher.ExecuteCommandLine(shutdownExe, @"/s /t 30", systemFolder);
										WaitUntil = DateTime.Now.AddSeconds(60); //TODO: Convert to constant
										ProcessState = ServiceState.ShutdownWait;
									}
									else
									{
										WaitUntil = DateTime.Now.AddMinutes(60);
										LogEvent.Error("Failed to execute shutdown after {0} seconds of inactivity." +
											Environment.NewLine + "Shutdown not found at: {1}",
											secondsIdle, shutdownExe);
									}
								}
								break;

							case ServiceState.ShutdownWait:
								if (WaitUntil != DateTime.MinValue && DateTime.Now < WaitUntil)
								{
									break;
								}
								LogToFile.Info("Killing all processes matching the logged on user (except shutdown itself)");
								ProcessLauncher.KillProcessesMatchingLoggedOnUser();
								WaitUntil = DateTime.Now.AddSeconds(60); //TODO: Convert to constant
								ProcessState = ServiceState.KillUserProcess;
								break;

							case ServiceState.KillUserProcess:
								if (WaitUntil != DateTime.MinValue && DateTime.Now < WaitUntil)
								{
									break;
								}
								// Nothing left to do but start the process over
								LogToFile.Info("Killing user processes did not seem to work, will retry shutdown in 5 minutes");
								WaitUntil = DateTime.Now.AddMinutes(5);
								ProcessState = ServiceState.IdleWait;
								break;
						}
					}
					else if (continueThread.WaitOne(0))
					{
						threadStatus = ThreadStatus.Running;
					}

					for (int i = 0; i < secondsToSleep && !endThread; i++)
					{
						Thread.Sleep(1000);
						if (stopEvent.WaitOne(0))
						{
							endThread = true;
							LogEvent.Info("Stop event signaled at " + DateTime.Now.ToString());
						}
					}
				}
			}
			catch (ThreadAbortException)
			{
				LogEvent.Info("Worker thread has been aborted, shutting down");
			}
			catch (Exception ex)
			{
				LogEvent.Error("Exception while running main service worker thread:\r\n\r\n" +
							   ex);
			}
			finally
			{
				threadStatus = ThreadStatus.Stopped;
				LogEvent.Info("Main service worker thread exiting");
			}
		}

		protected override void OnStop()
		{
			LogToFile.Info("Service stop requested, ending the service");
			DoStop();
		}

		public void DoStop()
		{
			if (!Environment.UserInteractive)
			{
				RequestAdditionalTime(4000);
			}
			if (workerThread != null && workerThread.IsAlive)
			{
				stopEvent.Set();
				for (int i = 0; i < 50 && threadStatus != ThreadStatus.Stopped; i++)
				{
					Thread.Sleep(100);
				}
				//pauseEvent.Reset();
				//Thread.Sleep(5000);
				//workerThread.Abort();
			}

			if (workerThread != null)
			{
				LogEvent.Info("DoStop - Worker thread state: {0}\r\nThread status: {1}",
					workerThread.ThreadState.ToString(), threadStatus.ToString());
			}

			ExitCode = 0;
		}

		// Pause the service.
		protected override void OnPause()
		{
			// Pause the worker thread.
			if (threadStatus == ThreadStatus.Running)
			{
				LogEvent.Info("OnPause - Pausing the backup worker thread.");

				pauseEvent.Set();
				for (int i = 0; i < 50 && threadStatus != ThreadStatus.Paused; i++)
				{
					Thread.Sleep(100);
				}
			}

			if (workerThread != null)
			{
				LogEvent.Info("OnPause - Worker thread state = {0}\r\nThread status: {1}",
					workerThread.ThreadState.ToString(), threadStatus.ToString());
			}
		}

		// Continue a paused service.
		protected override void OnContinue()
		{
			// Signal the worker thread to continue.
			if (threadStatus == ThreadStatus.Paused)
			{
				LogEvent.Info("OnContinue - Resuming the service worker thread.");

				continueThread.Set();
				for (int i = 0; i < 50 && threadStatus != ThreadStatus.Running; i++)
				{
					Thread.Sleep(100);
				}
			}

			if (workerThread != null)
			{
				LogEvent.Info("OnContinue - Worker thread state = {0}\r\nThread status: {1}",
					workerThread.ThreadState.ToString(), threadStatus.ToString());
			}
		}

		#endregion

		public enum ThreadStatus
		{
			None = 0,
			Running,
			Paused,
			Stopped,
		}

		private Thread workerThread;
		private static readonly ManualResetEvent pauseEvent = new ManualResetEvent(false);
		private static readonly ManualResetEvent continueThread = new ManualResetEvent(false);
		private static readonly ManualResetEvent stopEvent = new ManualResetEvent(false);
		private static volatile ThreadStatus threadStatus;

		private SERVICE_STATUS myServiceStatus;

		#region Service Helpers

		[StructLayout(LayoutKind.Sequential)]
		public struct SERVICE_STATUS
		{
			public int serviceType;
			public int currentState;
			public int controlsAccepted;
			public int win32ExitCode;
			public int serviceSpecificExitCode;
			public int checkPoint;
			public int waitHint;
		}

		public enum State
		{
			SERVICE_STOPPED = 0x00000001,
			SERVICE_START_PENDING = 0x00000002,
			SERVICE_STOP_PENDING = 0x00000003,
			SERVICE_RUNNING = 0x00000004,
			SERVICE_CONTINUE_PENDING = 0x00000005,
			SERVICE_PAUSE_PENDING = 0x00000006,
			SERVICE_PAUSED = 0x00000007,
		}

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern int SetServiceStatus(
			IntPtr hServiceStatus,
			ref SERVICE_STATUS lpServiceStatus);

		#endregion
	}
}
