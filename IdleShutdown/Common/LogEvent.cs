using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Orvado.IdleShutdown.Common
{
	/// <summary>
	/// This class is used to write messages to the event log.
	/// </summary>
	public static class LogEvent
	{
		private static readonly Dictionary<string, DateTime> LastLogTime =
			new Dictionary<string, DateTime>();

		private static readonly string AppVersion =
			Assembly.GetExecutingAssembly().GetName().Version.ToString();

		private static bool ShouldSuppressLog(string message, TimeSpan timeBetweenEntries)
		{
			bool suppressLog = LastLogTime.ContainsKey(message) &&
				DateTime.Now.Subtract(LastLogTime[message]) < timeBetweenEntries;
			LastLogTime[message] = DateTime.Now;
			return suppressLog;
		}

		/// <summary>
		/// Writes an event in the Event Log (and also to the log file found
		/// in the application folder)
		/// </summary>
		/// <param name="logName">Name of the log to write</param>
		/// <param name="source">Source of the log message</param>
		/// <param name="message">The message to be logged.</param>
		/// <param name="severity">The severity of the event.</param>
		/// <param name="timeBetweenEntries"></param>
		private static void WriteLog(string logName, string source, string message,
			ESeverityLevel severity, TimeSpan timeBetweenEntries)
		{
			if (SourceExists(source, logName))
			{
				if (SeverityLevel != ESeverityLevel.SeverityNone &&
					(timeBetweenEntries == TimeSpan.Zero ||
					!ShouldSuppressLog(message, timeBetweenEntries)))
				{
					message = string.Format("[{0}] {1} ", AppVersion, message);
					switch (severity)
					{
						case ESeverityLevel.SeverityError:
							LogToFile.Error(message);
							EventLog.WriteEntry(source, message, EventLogEntryType.Error, 1, 1);
							break;

						case ESeverityLevel.SeverityWarning:
							LogToFile.Warning(message);
							EventLog.WriteEntry(source, message, EventLogEntryType.Warning, 1, 1);
							break;

						case ESeverityLevel.SeverityInfo:
							LogToFile.Info(message);
							EventLog.WriteEntry(source, message, EventLogEntryType.Information, 1, 1);
							break;
					}
				}
			}
		}


		/// <summary>
		/// Writes an event in the Event Log.
		/// </summary>
		/// <param name="message">The message to be logged. </param>
		/// <param name="severity">The severity of the event. </param>
		/// <param name="timeBetweenEntries"></param>
		private static void Write(string message, ESeverityLevel severity, TimeSpan timeBetweenEntries)
		{
			string sourceNameSpace = Process.GetCurrentProcess().MainModule.ModuleName;

			WriteLog(Settings.Default.EventLogName, sourceNameSpace, message, severity,
				timeBetweenEntries);
		}

		/// <summary>
		/// Logs the specified exception.
		/// </summary>
		/// <param name="ex">The exception. </param>
		/// <param name="severity">The severity. </param>
		public static void Exception(Exception ex, ESeverityLevel severity)
		{
			if (ex == null)
			{
				Write(Resources.LogEventNullException, ESeverityLevel.SeverityWarning,
					TimeSpan.Zero);
			}
			else
			{
				Write(ex.ToString(), severity, TimeSpan.Zero);
			}
		}


		public static void Info(string message, params object[] parameters)
		{
			Write(message == null ? Resources.LogEventNullMessage : String.Format(message, parameters),
				ESeverityLevel.SeverityInfo, TimeSpan.Zero);
		}

		public static void InfoLimited(string message, TimeSpan timeBetweenEntries,
			params object[] parameters)
		{
			Write(message == null ? Resources.LogEventNullMessage : String.Format(message, parameters),
				ESeverityLevel.SeverityInfo, timeBetweenEntries);
		}

		public static void Warning(string message, params object[] parameters)
		{
			Write(message == null ? Resources.LogEventNullMessage : String.Format(message, parameters),
				ESeverityLevel.SeverityWarning, TimeSpan.Zero);
		}

		public static void WarningLimited(string message, TimeSpan timeBetweenEntries,
			params object[] parameters)
		{
			Write(message == null ? Resources.LogEventNullMessage : String.Format(message, parameters),
				ESeverityLevel.SeverityWarning, timeBetweenEntries);
		}

		/// <summary>
		/// Logs the specified message with severity Error.
		/// </summary>
		/// <param name="message">The message we want to log.</param>
		/// <param name="parameters">Variable list of parameters for string.format of message.</param>
		public static void Error(string message, params object[] parameters)
		{
			Write(message == null ? Resources.LogEventNullMessage : String.Format(message, parameters),
				ESeverityLevel.SeverityError, TimeSpan.Zero);
		}

		public static void ErrorLimited(string message, TimeSpan timeBetweenEntries,
			params object[] parameters)
		{
			Write(message == null ? Resources.LogEventNullMessage : String.Format(message, parameters),
				ESeverityLevel.SeverityError, timeBetweenEntries);
		}

		/// <summary>
		/// Gets or sets the severity level at application level.
		/// </summary>
		private static ESeverityLevel SeverityLevel { get; set; }

		/// <summary>
		/// Determines if an event sources is registered on the computer.
		/// </summary>
		/// <param name="logName">Name of the log</param>
		/// <param name="source">The Sources name to be checked or to be created.</param>
		/// <returns>True if the source exists, false otherwise.</returns>
		private static bool SourceExists(string source, string logName)
		{
			lock (SyncObj)
			{
				if (!defaultSourceExists)
				{
					Initialize();
				}

				if (!EventLog.SourceExists(source))
				{
					EventLog.CreateEventSource(source, logName);
				}
			}

			return EventLog.SourceExists(source);
		}

		/// <summary>
		/// Creates default source for event logging and
		/// sets the Overflow policy to OverwriteAsNeeded.
		/// </summary>       
		private static void Initialize()
		{
			try
			{
				if (!EventLog.SourceExists(Resources.LogEventDefaultSource))
				{
					EventLog.CreateEventSource(Resources.LogEventDefaultSource,
												Settings.Default.EventLogName);

					EventLog ev = new EventLog(Settings.Default.EventLogName);
					ev.ModifyOverflowPolicy(OverflowAction.OverwriteAsNeeded, 7);
					ev.MaximumKilobytes = 5120;
				}

				defaultSourceExists = EventLog.SourceExists(Resources.LogEventDefaultSource);
			}
			catch
			{
				defaultSourceExists = false;
			}
		}

		/// Represents the severity level set at application level. 
		private static bool defaultSourceExists;
		private static readonly object SyncObj = new object();

		static LogEvent()
		{
			SeverityLevel = ESeverityLevel.SeverityWarning;
		}
	}

	/// <summary>
	/// Enumerates the possible levels of the log severity
	/// </summary>
	public enum ESeverityLevel
	{
		/// No messages is written to the log.
		SeverityNone,
		/// Error and info messages are written. Represents an error message.
		SeverityError,
		/// Error, info and warning messages are written.Represents a warning message.
		SeverityWarning,
		/// Represents an info message.
		SeverityInfo
	}
}