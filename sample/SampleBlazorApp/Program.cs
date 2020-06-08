using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;
using Elastic.Apm;
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

			DiagnosticListener.AllListeners.Subscribe(new MyListener());

			Console.WriteLine("Program.Main runs");


			if(!Agent.IsConfigured)
			{
				Agent.Setup(new RumConfig().RumAgentComponents);
				Agent.Subscribe(new HttpDiagnosticsSubscriber());
			}



            builder.Services.AddTransient(sp =>
			{
				var httpClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
				httpClient.DefaultRequestHeaders.Add("Access-Control-Allow-Headers", "Access-Control-Allow-Headers, Access-Control-Allow-Headers, Access-Control-Allow-Methods, Access-Control-Allow-Origin, Content-Type, Content-Encoding, Accept, Referer, User-Agent, traceparent");
				httpClient.DefaultRequestHeaders.Add("Access-Control-Allow-Methods", "GET");
				httpClient.DefaultRequestHeaders.Add("Access-Control-Allow-Origin", "*");

				// HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "");

				//httpClient.DefaultRequestHeaders.Add("Access-Control-Allow-Origin", "http://localhost:5050");
				return httpClient;
			});

            await builder.Build().RunAsync();
        }
    }

	public class MyListener : IObserver<DiagnosticListener>
	{
		public void OnCompleted() => throw new NotImplementedException();

		public void OnError(Exception error) => throw new NotImplementedException();

		public void OnNext(DiagnosticListener value) => Console.WriteLine("Listener: " + value.Name);
	}
}
