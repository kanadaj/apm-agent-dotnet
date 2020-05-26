// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Threading.Tasks;
using Elastic.Apm.Config;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;
using Elastic.Apm.Model;
using Elastic.Apm.Report;

namespace Elastic.Apm.Api
{
	internal class RumTracer : ITracer
	{
		private ScopedLogger _logger;
		private RumService _service;
		private IPayloadSender _sender;
		private IConfigSnapshotProvider _configProvider;

		public RumTracer(
		IApmLogger logger,
			RumService service,
		IPayloadSender payloadSender,
			IConfigSnapshotProvider configProvider,
		ICurrentExecutionSegmentsContainer currentExecutionSegmentsContainer
		)
		{
			_logger = logger?.Scoped(nameof(Tracer));
			_service = service;
			_sender = payloadSender.ThrowIfArgumentNull(nameof(payloadSender));
			_configProvider = configProvider.ThrowIfArgumentNull(nameof(configProvider));
			CurrentExecutionSegmentsContainer = currentExecutionSegmentsContainer.ThrowIfArgumentNull(nameof(currentExecutionSegmentsContainer));
		}

		public ITransaction StartTransaction(string name, string type, DistributedTracingData distributedTracingData)
		{
			var currentConfig = _configProvider.CurrentSnapshot;
			var retVal = new RumTransaction(_logger, name, type, new Sampler(currentConfig.TransactionSampleRate), distributedTracingData
					, _sender, currentConfig, CurrentExecutionSegmentsContainer)
				{ RumService = _service };

			_logger.Debug()?.Log("Starting {TransactionValue}", retVal);
			return retVal;
		}

		internal ICurrentExecutionSegmentsContainer CurrentExecutionSegmentsContainer { get; }

		public ISpan CurrentSpan => CurrentExecutionSegmentsContainer.CurrentSpan;
		public ITransaction CurrentTransaction => CurrentExecutionSegmentsContainer.CurrentTransaction;

		public void CaptureTransaction(string name, string type, Action<ITransaction> action, DistributedTracingData distributedTracingData = null) => throw new NotImplementedException();

		public void CaptureTransaction(string name, string type, Action action, DistributedTracingData distributedTracingData = null) => throw new NotImplementedException();

		public T CaptureTransaction<T>(string name, string type, Func<ITransaction, T> func, DistributedTracingData distributedTracingData = null) => throw new NotImplementedException();

		public T CaptureTransaction<T>(string name, string type, Func<T> func, DistributedTracingData distributedTracingData = null) => throw new NotImplementedException();

		public Task CaptureTransaction(string name, string type, Func<Task> func, DistributedTracingData distributedTracingData = null) => throw new NotImplementedException();

		public Task CaptureTransaction(string name, string type, Func<ITransaction, Task> func, DistributedTracingData distributedTracingData = null) => throw new NotImplementedException();

		public Task<T> CaptureTransaction<T>(string name, string type, Func<Task<T>> func, DistributedTracingData distributedTracingData = null) => throw new NotImplementedException();

		public Task<T> CaptureTransaction<T>(string name, string type, Func<ITransaction, Task<T>> func, DistributedTracingData distributedTracingData = null) => throw new NotImplementedException();

	}
}
