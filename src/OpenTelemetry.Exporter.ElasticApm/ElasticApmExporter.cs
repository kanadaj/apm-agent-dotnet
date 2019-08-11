using System;
using Elastic.Apm;
using Elastic.Apm.Logging;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.ElasticApm
{
	public class ElasticApmExporter
	{
		private readonly ISpanExporter _exporter;
		private ElasticApmHandler _handler;
		private readonly object _lck = new object();

		/// <summary>
		/// Initializes a new instance of the <see cref="ElasticApmExporter"/> class.
		/// </summary>
		/// <param name="exporter">Exporter to get traces from.</param>
		public ElasticApmExporter(
			ISpanExporter exporter
		)

		{

			Agent.Setup(new AgentComponents(new ConsoleLogger(LogLevel.Trace)));
			_exporter = exporter;
		}

		/// <summary>
		/// Start exporter.
		/// </summary>
		public void Start()
		{
			lock (_lck)
			{
				if (_handler != null)
				{
					return;
				}
				_handler = new ElasticApmHandler();
				_exporter.RegisterHandler("ElasticApmHandler", _handler);
			}
		}

		/// <summary>
		/// Stop exporter.
		/// </summary>
		public void Stop()
		{
            lock (_lck)
            {
                if (_handler == null)
                {
                    return;
                }

                _exporter.UnregisterHandler("ElasticApmHandler");
               // _handler.Dispose();
                _handler = null;
            }
		}
	}
}
