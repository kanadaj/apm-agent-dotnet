// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Elastic.Apm.Api;
using Elastic.Apm.BackendComm;
using Elastic.Apm.Config;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;
using Elastic.Apm.Metrics;
using Elastic.Apm.Model;
using Elastic.Apm.Report.Serialization;

namespace Elastic.Apm.Report
{
	/// <summary>
	/// A common base for payload senders. Encapsulates managing the internal queue, creating of HttpClient and serialization
	/// </summary>
	internal abstract class PayloadSenderBase : BackendCommComponentBase, IPayloadSender
	{
		private const string ThisClassName = nameof(PayloadSenderV2);

		internal readonly Api.System System;

		private readonly BatchBlock<object> _eventQueue;

		private readonly TimeSpan _flushInterval;
		protected Uri IntakeEventsAbsoluteUrl;

		private readonly int _maxQueueEventCount;
		private readonly Metadata _metadata;

		private readonly PayloadItemSerializer _payloadItemSerializer;

		internal readonly List<Func<ITransaction, ITransaction>> TransactionFilters = new List<Func<ITransaction, ITransaction>>();
		internal readonly List<Func<ISpan, ISpan>> SpanFilters = new List<Func<ISpan, ISpan>>();
		internal readonly List<Func<IError, IError>> ErrorFilters = new List<Func<IError, IError>>();

		/// <summary>
		///
		/// </summary>
		/// <param name="logger"></param>
		/// <param name="config"></param>
		/// <param name="service"></param>
		/// <param name="system"></param>
		/// <param name="httpMessageHandler"></param>
		/// <param name="dbgName"></param>
		/// <param name="useSingleThreadTaskScheduler">In some environments managing <see cref="Thread"/> manually is not supported (e.g. mono on WASM) - by passing <code>false</code> into this argument, those Thread related APIs won't be used</param>
		protected PayloadSenderBase(IApmLogger logger, IConfigSnapshot config, Service service, Api.System system,
			HttpMessageHandler httpMessageHandler = null, string dbgName = null, bool useSingleThreadTaskScheduler = true
		)
			: base( /* isEnabled: */ true, logger, ThisClassName, service, config, httpMessageHandler, dbgName, useSingleThreadTaskScheduler)
		{
			_payloadItemSerializer = new PayloadItemSerializer(config);
			System = system;

			if (service != null)
				_metadata = new Metadata { Service = service, System = System };

			foreach (var globalLabelKeyValue in config.GlobalLabels) _metadata.Labels.Add(globalLabelKeyValue.Key, globalLabelKeyValue.Value);

			if (config.MaxQueueEventCount < config.MaxBatchEventCount)
			{
				Logger?.Error()
					?.Log(
						"MaxQueueEventCount is less than MaxBatchEventCount - using MaxBatchEventCount as MaxQueueEventCount."
						+ " MaxQueueEventCount: {MaxQueueEventCount}."
						+ " MaxBatchEventCount: {MaxBatchEventCount}.",
						config.MaxQueueEventCount, config.MaxBatchEventCount);

				_maxQueueEventCount = config.MaxBatchEventCount;
			}
			else
				_maxQueueEventCount = config.MaxQueueEventCount;

			_flushInterval = config.FlushInterval;

			Logger?.Debug()
				?.Log(
					"Using the following configuration options:"
					+ " Events intake API absolute URL: {EventsIntakeAbsoluteUrl}"
					+ ", FlushInterval: {FlushInterval}"
					+ ", MaxBatchEventCount: {MaxBatchEventCount}"
					+ ", MaxQueueEventCount: {MaxQueueEventCount}"
					, IntakeEventsAbsoluteUrl, _flushInterval.ToHms(), config.MaxBatchEventCount, _maxQueueEventCount);

			_eventQueue = new BatchBlock<object>(config.MaxBatchEventCount);

			StartWorkLoop();
		}

		private string _cachedMetadataJsonLine;

		private long _eventQueueCount;

		public void QueueTransaction(ITransaction transaction) => EnqueueEvent(transaction, "Transaction");

		public void QueueSpan(ISpan span) => EnqueueEvent(span, "Span");

		public void QueueMetrics(IMetricSet metricSet) => EnqueueEvent(metricSet, "MetricSet");

		public void QueueError(IError error) => EnqueueEvent(error, "Error");

		private bool EnqueueEvent(object eventObj, string dbgEventKind)
		{
			ThrowIfDisposed();

			// Enforce _maxQueueEventCount manually instead of using BatchBlock's BoundedCapacity
			// because of the issue of Post returning false when TriggerBatch is in progress. For more details see
			// https://stackoverflow.com/questions/35626955/unexpected-behaviour-tpl-dataflow-batchblock-rejects-items-while-triggerbatch
			var newEventQueueCount = Interlocked.Increment(ref _eventQueueCount);
			if (newEventQueueCount > _maxQueueEventCount)
			{
				Logger.Debug()
					?.Log("Queue reached max capacity - " + dbgEventKind + " will be discarded."
						+ " newEventQueueCount: {EventQueueCount}."
						+ " MaxQueueEventCount: {MaxQueueEventCount}."
						+ " " + dbgEventKind + ": {" + dbgEventKind + "}."
						, newEventQueueCount, _maxQueueEventCount, eventObj);
				Interlocked.Decrement(ref _eventQueueCount);
				return false;
			}

			var enqueuedSuccessfully = _eventQueue.Post(eventObj);
			if (!enqueuedSuccessfully)
			{
				Logger.Debug()
					?.Log("Failed to enqueue " + dbgEventKind + "."
						+ " newEventQueueCount: {EventQueueCount}."
						+ " MaxQueueEventCount: {MaxQueueEventCount}."
						+ " " + dbgEventKind + ": {" + dbgEventKind + "}."
						, newEventQueueCount, _maxQueueEventCount, eventObj);
				Interlocked.Decrement(ref _eventQueueCount);
				return false;
			}

			Logger.Debug()
				?.Log("Enqueued " + dbgEventKind + "."
					+ " newEventQueueCount: {EventQueueCount}."
					+ " MaxQueueEventCount: {MaxQueueEventCount}."
					+ " " + dbgEventKind + ": {" + dbgEventKind + "}."
					, newEventQueueCount, _maxQueueEventCount, eventObj);

			if (_flushInterval == TimeSpan.Zero) _eventQueue.TriggerBatch();

			return true;
		}

		protected override async Task WorkLoopIteration() => await ProcessQueueItems(await ReceiveBatchAsync());

		private async Task<object[]> ReceiveBatchAsync()
		{
			var receiveAsyncTask = _eventQueue.ReceiveAsync(CtsInstance.Token);

			if (_flushInterval == TimeSpan.Zero)
				Logger.Trace()?.Log("Waiting for data to send... (not using FlushInterval timer because FlushInterval is 0)");
			else
			{
				Logger.Trace()?.Log("Waiting for data to send... FlushInterval: {FlushInterval}", _flushInterval.ToHms());
				while (true)
				{
					if (await TryAwaitOrTimeout(receiveAsyncTask, _flushInterval, CtsInstance.Token)) break;

					_eventQueue.TriggerBatch();
				}
			}

			var eventBatchToSend = await receiveAsyncTask;
			var newEventQueueCount = Interlocked.Add(ref _eventQueueCount, -eventBatchToSend.Length);
			Logger.Trace()
				?.Log("There's data to be sent. Batch size: {BatchSize}. newEventQueueCount: {newEventQueueCount}. First event: {Event}."
					, eventBatchToSend.Length, newEventQueueCount, eventBatchToSend.Length > 0 ? eventBatchToSend[0].ToString() : "<N/A>");
			return eventBatchToSend;
		}

		/// <summary>
		/// It's recommended to use this method (or another TryAwaitOrTimeout or AwaitOrTimeout method)
		/// instead of just Task.WhenAny(taskToAwait, Task.Delay(timeout))
		/// because this method cancels the timer for timeout while <c>Task.Delay(timeout)</c>.
		/// If the number of “zombie” timer jobs starts becoming significant, performance could suffer.
		/// For more detailed explanation see https://devblogs.microsoft.com/pfxteam/crafting-a-task-timeoutafter-method/
		/// </summary>
		/// <returns><c>true</c> if <c>taskToAwait</c> completed before the timeout, <c>false</c> otherwise</returns>
		private static async Task<bool> TryAwaitOrTimeout(Task taskToAwait, TimeSpan timeout, CancellationToken cancellationToken = default)
		{
			var timeoutDelayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var timeoutDelayTask = Task.Delay(timeout, timeoutDelayCts.Token);
			try
			{
				var completedTask = await Task.WhenAny(taskToAwait, timeoutDelayTask);
				if (completedTask == taskToAwait)
				{
					await taskToAwait;
					return true;
				}

				Assertion.IfEnabled?.That(completedTask == timeoutDelayTask
					, $"{nameof(completedTask)}: {completedTask}, {nameof(timeoutDelayTask)}: timeOutTask, {nameof(taskToAwait)}: taskToAwait");
				// no need to cancel timeout timer if it has been triggered
				timeoutDelayTask = null;
				return false;
			}
			finally
			{
				if (timeoutDelayTask != null) timeoutDelayCts.Cancel();
				timeoutDelayCts.Dispose();
			}
		}

		private async Task ProcessQueueItems(object[] queueItems)
		{
			try
			{
				var ndjson = new StringBuilder();

				_cachedMetadataJsonLine ??= "{\"metadata\": " + _payloadItemSerializer.SerializeObject(_metadata) + "}";
				ndjson.AppendLine(_cachedMetadataJsonLine);

				foreach (var item in queueItems)
				{
					switch (item)
					{
						case Transaction transaction:
							if (TryExecuteFilter(TransactionFilters, transaction) != null) SerializeAndSend(item, "transaction");
							break;
						case Span span:
							if (TryExecuteFilter(SpanFilters, span) != null) SerializeAndSend(item, "span");
							break;
						case Error error:
							if (TryExecuteFilter(ErrorFilters, error) != null) SerializeAndSend(item, "error");
							break;
						case MetricSet _:
							SerializeAndSend(item, "metricset");
							break;
					}
				}

				var content = new StringContent(ndjson.ToString(), Encoding.UTF8, "application/x-ndjson");

				var result = await HttpClientInstance.PostAsync(IntakeEventsAbsoluteUrl, content, CtsInstance.Token);

				if (result != null && !result.IsSuccessStatusCode)
				{
					Logger?.Error()
						?.Log("Failed sending event."
							+ " Events intake API absolute URL: {EventsIntakeAbsoluteUrl}."
							+ " APM Server response: status code: {ApmServerResponseStatusCode}"
							+ ", content: \n{ApmServerResponseContent}"
							, IntakeEventsAbsoluteUrl, result.StatusCode, await result.Content.ReadAsStringAsync());
				}
				else
				{
					Logger?.Debug()
						?.Log("Sent items to server:\n{SerializedItems}",
							TextUtils.Indent(string.Join($",{Environment.NewLine}", queueItems.ToArray())));
				}

				void SerializeAndSend(object item, string eventType)
				{
					var serialized = _payloadItemSerializer.SerializeObject(item);
					ndjson.AppendLine($"{{\"{eventType}\": " + serialized + "}");
					Logger?.Trace()?.Log("Serialized item to send: {ItemToSend} as {SerializedItem}", item, serialized);
				}
			}
			catch (Exception e)
			{
				Logger?.Warning()
					?.LogException(
						e,
						"Failed sending events. Following events were not transferred successfully to the server ({ApmServerUrl}):\n{SerializedItems}"
						, HttpClientInstance.BaseAddress
						, TextUtils.Indent(string.Join($",{Environment.NewLine}", queueItems.ToArray()))
					);
			}

			// Executes filters for the given filter collection and handles return value and errors
			T TryExecuteFilter<T>(IEnumerable<Func<T, T>> filters, T item) where T : class
			{
				var enumerable = filters as Func<T, T>[] ?? filters.ToArray();
				if (!enumerable.Any()) return item;

				foreach (var filter in enumerable)
				{
					try
					{
						Logger?.Trace()?.Log("Start executing filter on transaction");
						var itemAfterFilter = filter(item);
						if (itemAfterFilter != null)
						{
							item = itemAfterFilter;
							continue;
						}

						Logger?.Debug()?.Log("Filter returns false, item won't be sent, {filteredItem}", item);
						return null;
					}
					catch (Exception e)
					{
						Logger.Warning()?.LogException(e, "Exception during execution of the filter on transaction");
					}
				}

				return item;
			}
		}
	}
}
