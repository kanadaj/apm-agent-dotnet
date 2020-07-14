using System;
using Microsoft.Extensions.Logging;
using Elastic.Apm.Model;
using Elastic.Apm.Helpers;
using System.Diagnostics;

namespace Elastic.Apm.Extensions.Logging
{
	public class ElasticApmErrorLogger : ILogger
	{
		public IDisposable BeginScope<TState>(TState state)
		  => null; //TODO
		

		public bool IsEnabled(LogLevel logLevel)
			=> logLevel >= LogLevel.Error;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			if (!Agent.IsConfigured) return;
			if (logLevel < LogLevel.Error) return;

			var configReader = Agent.Instance.ConfigurationReader;
			var logger = Agent.Instance.Logger;

			//TODO: do not capture agent errors as APM error

			var logLine = formatter(state, exception);
			var logOnError = new LogOnError(logLine)
			{
				StackTrace = StacktraceHelper.GenerateApmStackTrace(new StackTrace(true).GetFrames(), logger, configReader, nameof(ElasticApmErrorLogger)),
				Level = logLevel.ToString()
			};
			if (Agent.Tracer.CurrentSpan != null)
			{
				(Agent.Tracer.CurrentSpan as Span)?.CaptureLogError(logOnError, exception: exception);
			}
			else if (Agent.Tracer.CurrentTransaction != null)
			{
				(Agent.Tracer.CurrentTransaction as Transaction)?.CaptureLogError(logOnError, exception: exception);
			}
		}
	}
}
