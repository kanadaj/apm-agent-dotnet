// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Apm.Extensions.Logging;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace SampleAspNetCoreApp
{
	public class Program
	{
		public static void Main(string[] args) => CreateWebHostBuilder(args).Build().Run();

		public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
			WebHost.CreateDefaultBuilder(args)
			.ConfigureLogging(n =>
			{
				n.AddConsole();
				n.AddProvider(new ElasticApmErrorLoggingProvider());
			})
			.UseStartup<Startup>();
	}
}
