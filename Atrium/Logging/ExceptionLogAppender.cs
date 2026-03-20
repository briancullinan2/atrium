#if false
using System;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using log4net.Appender;
using log4net.Core;

namespace EPIC.Utilities.Logging
{
	// Token: 0x02000066 RID: 102
	public class ExceptionLogAppender : AdoNetAppender
	{
		// Token: 0x17000097 RID: 151
		// (set) Token: 0x0600031C RID: 796 RVA: 0x00019CCE File Offset: 0x00017ECE
		public string ConnectionStringName
		{
			set
			{
				base.ConnectionString = ConfigurationManager.ConnectionStrings[value].ToString();
			}
		}

		// Token: 0x0600031D RID: 797 RVA: 0x00019CE8 File Offset: 0x00017EE8
		protected override void SendBuffer(LoggingEvent[] events)
		{
			foreach (LoggingEvent loggingEvent in events)
			{
				try
				{
					string applicationVersion = string.Join(",", Assembly.GetExecutingAssembly().FullName.Split(new char[]
					{
						','
					}).Take(2));
					ExceptionLogEntity exceptionLogEntity = new ExceptionLogEntity
					{
						Title = loggingEvent.RenderedMessage.Substring(0, Math.Min(loggingEvent.RenderedMessage.Length, ExceptionLogFields.Title.MaxLength)),
						Message = ((loggingEvent.ExceptionObject != null) ? loggingEvent.ExceptionObject.Message.Substring(0, Math.Min(loggingEvent.ExceptionObject.Message.Length, ExceptionLogFields.Message.MaxLength)) : ""),
						StackTrace = ((loggingEvent.ExceptionObject != null) ? loggingEvent.ExceptionObject.StackTrace.Substring(0, Math.Min(loggingEvent.ExceptionObject.StackTrace.Length, ExceptionLogFields.StackTrace.MaxLength)) : ""),
						LogTime = loggingEvent.TimeStamp,
						User = loggingEvent.Identity,
						FormName = loggingEvent.LoggerName,
						MachineName = Environment.MachineName,
						MachineOS = Environment.OSVersion.VersionString,
						ApplicationVersion = applicationVersion,
						CLRVersion = Environment.Version.ToString(),
						MemoryUsage = Environment.WorkingSet.ToString(CultureInfo.InvariantCulture),
						ReceivedTime = DateTime.UtcNow
					};
					exceptionLogEntity.Save();
				}
				catch
				{
				}
			}
		}
	}
}
#endif
