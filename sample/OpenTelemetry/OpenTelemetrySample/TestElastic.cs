using System;
using System.Collections.Generic;
using System.Threading;
using OpenTelemetry.Exporter.ElasticApm;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Sampler;

namespace OpenTelemetrySample
{
	public class TestElastic
	{
		internal static void Run(string host, int port)
		{
			// 1. Configure exporter to export traces to Elastic APM
			var exporter = new ElasticApmExporter(
				Tracing.SpanExporter);

			exporter.Start();
			// 2. Configure 100% sample rate for the purposes of the demo
			var traceConfig = Tracing.TraceConfig;
			var currentConfig = traceConfig.ActiveTraceParams;
			var newConfig = currentConfig.ToBuilder()
				.SetSampler(Samplers.AlwaysSample).Build();
			traceConfig.UpdateActiveTraceParams(newConfig);

			// 3. Tracer is global singleton. You can register it via dependency injection if it exists
			// but if not - you can use it as follows:
			var tracer = Tracing.Tracer;

			// 4. Create a scoped span. It will end automatically when using statement ends
			using (tracer.WithSpan(tracer.SpanBuilder("Main").StartSpan()))
			{
				tracer.CurrentSpan.SetAttribute("custom-attribute", 55);
				Console.WriteLine("About to do a busy work");
				for (var i = 0; i < 10; i++) DoWork(i);
			}

			// 5. Gracefully shutdown the exporter so it'll flush queued traces to Zipkin.
			Tracing.SpanExporter.Dispose();
		}
		private static void DoWork(int i)
		{
			// 6. Get the global singleton Tracer object
			var tracer = Tracing.Tracer;

			// 7. Start another span. If another span was already started, it'll use that span as the parent span.
			// In this example, the main method already started a span, so that'll be the parent span, and this will be
			// a child span.
			using (tracer.WithSpan(tracer.SpanBuilder("DoWork").StartSpan()))
			{
				// Simulate some work.
				var span = tracer.CurrentSpan;

				try
				{
					Console.WriteLine("Doing busy work");
					Thread.Sleep(1000);
				}
				catch (ArgumentOutOfRangeException e)
				{
					// 6. Set status upon error
					span.Status = Status.Internal.WithDescription(e.ToString());
				}

				// 7. Annotate our span to capture metadata about our operation
				var attributes = new Dictionary<string, object>();
				attributes.Add("use", "demo");
				span.AddEvent("Invoking DoWork", attributes);
			}
		}
	}
}
