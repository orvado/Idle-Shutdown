using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Orvado.IdleShutdown.Common
{
	public static class IdleDetection
	{
		[StructLayout(LayoutKind.Sequential)]
		struct LASTINPUTINFO
		{
			public static readonly int SizeOf = Marshal.SizeOf(typeof(LASTINPUTINFO));

			[MarshalAs(UnmanagedType.U4)]
			public UInt32 cbSize;
			[MarshalAs(UnmanagedType.U4)]
			public UInt32 dwTime;
		}

		// see: http://www.pinvoke.net/default.aspx/user32.GetLastInputInfo

		[DllImport("user32.dll")]
		static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

		/// <summary>
		/// Seconds since the last user input
		/// </summary>
		/// <returns>Seconds since last input</returns>
		public static uint GetLastInputTime()
		{
			uint idleTime = 0;
			LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
			lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
			lastInputInfo.dwTime = 0;

			uint envTicks = (uint)Environment.TickCount;

			if (GetLastInputInfo(ref lastInputInfo))
			{
				uint lastInputTick = lastInputInfo.dwTime;

				idleTime = envTicks - lastInputTick;
			}

			return (idleTime > 0) ? (idleTime / 1000) : 0;
		}
	}
}
