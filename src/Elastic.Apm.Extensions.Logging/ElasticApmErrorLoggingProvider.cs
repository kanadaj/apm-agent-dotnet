using System;
using Microsoft.Extensions.Logging;

namespace Elastic.Apm.Extensions.Logging
{
	public class ElasticApmErrorLoggingProvider : ILoggerProvider
	{
		public ILogger CreateLogger(string categoryName)
		 => new ElasticApmErrorLogger();

		public void Dispose() { }
	}
}
