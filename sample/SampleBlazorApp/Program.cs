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
				Agent.Setup(AgentComponents.RumAgentComponents());
				Agent.Subscribe(new HttpDiagnosticsSubscriber());
			}



            builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

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
