// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using Elastic.Apm.Api;
using Elastic.Apm.BackendComm.CentralConfig;
using Elastic.Apm.Config;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;
using Elastic.Apm.Metrics;
using Elastic.Apm.Report;

namespace Elastic.Apm
{
	public class RumConfig
	{
		public AgentComponents RumAgentComponents { get; }

		public RumConfig()
		{

			var tempLogger = new ConsoleLogger(LogLevel.Trace);
			var configReader = new EnvironmentConfigurationReader(tempLogger);
			var logger  = ConsoleLogger.LoggerOrDefault(LogLevel.Trace);
			//Service = Service.GetDefaultService(ConfigurationReader, Logger);

			var systemInfoHelper = new SystemInfoHelper(logger);
			var system = systemInfoHelper.ParseSystemInfo();

			var rumService = Service.GetDefaultService(configReader, logger);
			rumService.Agent.Name = "C# RUM Agent";
			rumService.Agent.Version = "0.1";
			// var rumService = new Service()
			// {
			// 	Agent = new Service.AgentC { Name = "C# RUM Agent", Version = "0.1" },
			// 	Framework = new Framework { Name = "TestFw", Version = "0.1" },
			// 	Name = "MySampleRumService"
			// };

			var configStore = new ConfigStore(new ConfigSnapshotFromReader(configReader, "local"), logger);

			var rumPayloadSender = new RumPayloadSenderV2(logger, "RumPayloadSenderV2", rumService, configReader, system);

			// MetricsCollector = new MetricsCollector(Logger, PayloadSender, ConfigurationReader);
			// MetricsCollector.StartCollecting();

			//CentralConfigFetcher = new CentralConfigFetcher(Logger, ConfigStore, Service);
			//RumTracerInternal = new RumTracer(Logger, rumService, rumPayloadSender, configStore, new CurrentExecutionSegmentsContainer());

			RumAgentComponents = new AgentComponents(logger, configReader, rumPayloadSender, new NoOpMetricsCollector(), new CurrentExecutionSegmentsContainer(), new NoOpCentralConfigFetcher(), rumService);
		}



	}

	internal class NoOpMetricsCollector : IMetricsCollector
	{
		public void StartCollecting() { }
	}

	internal class NoOpCentralConfigFetcher : ICentralConfigFetcher
	{
		public void Dispose() { }
	}
}
