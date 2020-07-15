using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Elastic.Apm.Extensions.Logging
{
	public static class LoggingBuilderExtensions
	{
		public static ILoggingBuilder AddElasticApmErrorCapturing(this ILoggingBuilder builder)
		  => builder.AddProvider(new ElasticApmErrorLoggingProvider());
	}
}
