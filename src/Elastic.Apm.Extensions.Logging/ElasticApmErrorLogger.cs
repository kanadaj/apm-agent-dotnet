using System;
using Microsoft.Extensions.Logging;
using Elastic.Apm.Model;
using Elastic.Apm.Helpers;

namespace Elastic.Apm.Extensions.Logging
{
	public class ElasticApmErrorLogger : ILogger
	{
		public IDisposable BeginScope<TState>(TState state)
		{
			//throw new NotImplementedException();
			return null;
		}

		public bool IsEnabled(LogLevel logLevel)
			=> logLevel >= LogLevel.Error;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			if (!Agent.IsConfigured) return;
			if (logLevel < LogLevel.Error) return;

			//TODO: do not capture agent errors as APM error


			var logLine = formatter(state, exception);
			var logOnError = new LogOnError(logLine);

			if (exception != null)
				logOnError.StackTrace = StacktraceHelper.GenerateApmStackTrace(exception, null, "fsdfds", null);

			logOnError.Level = logLevel.ToString();
			if (Agent.Tracer.CurrentSpan != null)
			{
				(Agent.Tracer.CurrentSpan as Span)?.CaptureLogError(logOnError);
			}
			else if (Agent.Tracer.CurrentTransaction != null)
			{
				(Agent.Tracer.CurrentTransaction as Transaction)?.CaptureLogError(logOnError);
			}
		}
	}
}
