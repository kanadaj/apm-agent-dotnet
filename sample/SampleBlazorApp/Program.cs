using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;
using Elastic.Apm;
using Elastic.Apm.Api;
using Elastic.Apm.DiagnosticSource;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SampleBlazorApp
{
	public class Program
	{
		public static async Task Main(string[] args)
		{

			var builder = WebAssemblyHostBuilder.CreateDefault(args);
			builder.RootComponents.Add<App>("app");

			Console.WriteLine("Program.Main runs");

			if (!Agent.IsConfigured)
			{
				Agent.Setup(new RumConfig().RumAgentComponents);
			}

			builder.Services.AddTransient(sp =>
			{
				var httpClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
				return httpClient;
			});

			builder.Services.AddSingleton<ITracer>(Agent.Tracer);

			await builder.Build().RunAsync();
		}
	}
}
