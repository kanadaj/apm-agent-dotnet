// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Apm.Api;
using Elastic.Apm.Config;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;
using Elastic.Apm.Report;
using Newtonsoft.Json;

namespace Elastic.Apm.Model
{
	internal class RumTransaction : ITransaction
	{
		private readonly IApmLogger _logger;
		private readonly Sampler _sampler;
		private readonly IPayloadSender _payloadSender;
		private readonly IConfigSnapshot _configSnapshot;
		private readonly ICurrentExecutionSegmentsContainer _executionSegmentsContainer;

		//[JsonProperty("k")]
		public Dictionary<string, Dictionary<string, int>> Marks { get;  } = new Dictionary<string, Dictionary<string,int>>();

		internal RumService RumService { get; set; }

		internal RumTransaction(IApmLogger logger, string name, string type, Sampler sampler, DistributedTracingData distributedTracingData, IPayloadSender sender, IConfigSnapshot currentConfig, ICurrentExecutionSegmentsContainer currentExecutionSegmentsContainer)
		{
			_logger = logger;
			Name = name;
			Type = type;
			_sampler = sampler;
			_payloadSender = sender;
			_configSnapshot = currentConfig;
			_executionSegmentsContainer = currentExecutionSegmentsContainer;


			var idBytes = new byte[8];
			Id = RandomGenerator.GenerateRandomBytesAsString(idBytes);

			TraceId = RandomGenerator.GenerateRandomBytesAsString(idBytes);

			RumSpanCount = new SpanCount();

			_executionSegmentsContainer.CurrentTransaction = this;

			// _logger = logger?.Scoped($"{nameof(Transaction)}.{Id}");
			//
			// _sender = sender;
			// _currentExecutionSegmentsContainer = currentExecutionSegmentsContainer;
			//
			// Name = name;
			// HasCustomName = false;
			// Type = type;
			//
			// StartActivity();
			//
			// var isSamplingFromDistributedTracingData = false;
			// if (distributedTracingData == null)
			// {
			// 	// Here we ignore Activity.Current.ActivityTraceFlags because it starts out without setting the IsSampled flag, so relying on that would mean a transaction is never sampled.
			// 	IsSampled = sampler.DecideIfToSample(idBytes);
			//
			// 	if (Activity.Current != null && Activity.Current.IdFormat == ActivityIdFormat.W3C)
			// 	{
			// 		TraceId = Activity.Current.TraceId.ToString();
			// 		ParentId = Activity.Current.ParentId;
			//
			// 		// Also mark the sampling decision on the Activity
			// 		if (IsSampled)
			// 			Activity.Current.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
			// 	}
			// 	else
			// 		TraceId = _activity.TraceId.ToString();
			// }
			// else
			// {
			// 	TraceId = distributedTracingData.TraceId;
			// 	ParentId = distributedTracingData.ParentId;
			// 	IsSampled = distributedTracingData.FlagRecorded;
			// 	isSamplingFromDistributedTracingData = true;
			// 	_traceState = distributedTracingData.TraceState;
			// }

			Timestamp = TimeUtils.TimestampNow();


			//TODO: sync with Activity
		}

		public Context Context { get; }
		public Dictionary<string, string> Custom { get; }

		//[JsonProperty("d")]
		public double? Duration { get; set; }

		public string Id { get; }
		public bool IsSampled { get; }
		public Dictionary<string, string> Labels { get; }

		//[JsonProperty("n")]
		public string Name { get; set; }

		public DistributedTracingData OutgoingDistributedTracingData { get; }

		[JsonProperty("parent_id")]
		public string ParentId { get; }

		public string Result { get; set; }

		[JsonProperty("span_count")]
		public SpanCount RumSpanCount { get; set; }

		[JsonProperty("trace_id")]
		public string TraceId { get; }

		//[JsonProperty("t")]
		public string Type { get; set; }

		//TODO: where to send this?
		public long Timestamp { get; }

		public void CaptureError(string message, string culprit, StackFrame[] frames, string parentId = null) => throw new NotImplementedException();

		public void CaptureException(Exception exception, string culprit = null, bool isHandled = false, string parentId = null) =>
			throw new NotImplementedException();

		public void CaptureSpan(string name, string type, Action<ISpan> capturedAction, string subType = null, string action = null) =>
			throw new NotImplementedException();

		public void CaptureSpan(string name, string type, Action capturedAction, string subType = null, string action = null) =>
			throw new NotImplementedException();

		public T CaptureSpan<T>(string name, string type, Func<ISpan, T> func, string subType = null, string action = null) =>
			throw new NotImplementedException();

		public T CaptureSpan<T>(string name, string type, Func<T> func, string subType = null, string action = null) =>
			throw new NotImplementedException();

		public Task CaptureSpan(string name, string type, Func<Task> func, string subType = null, string action = null) =>
			throw new NotImplementedException();

		public Task CaptureSpan(string name, string type, Func<ISpan, Task> func, string subType = null, string action = null) =>
			throw new NotImplementedException();

		public Task<T> CaptureSpan<T>(string name, string type, Func<Task<T>> func, string subType = null, string action = null) =>
			throw new NotImplementedException();

		public Task<T> CaptureSpan<T>(string name, string type, Func<ISpan, Task<T>> func, string subType = null, string action = null) =>
			throw new NotImplementedException();

		public void End()
		{
			var endTimestamp = TimeUtils.TimestampNow();

			Duration = TimeUtils.DurationBetweenTimestamps(Timestamp, endTimestamp);
			_payloadSender.QueueTransaction(this);
			_executionSegmentsContainer.CurrentTransaction = null;
			// if (Duration.HasValue)
			// {
			// 	_logger.Trace()
			// 		?.Log("Ended {Transaction} (with Duration already set)." +
			// 			" Start time: {Time} (as timestamp: {Timestamp}), Duration: {Duration}ms",
			// 			this, TimeUtils.FormatTimestampForLog(Timestamp), Timestamp, Duration);
			// }
			// else
			// {
			// 	Assertion.IfEnabled?.That(!_isEnded,
			// 		$"Transaction's Duration doesn't have value even though {nameof(End)} method was already called." +
			// 		$" It contradicts the invariant enforced by {nameof(End)} method - Duration should have value when {nameof(End)} method exits" +
			// 		$" and {nameof(_isEnded)} field is set to true only when {nameof(End)} method exits." +
			// 		$" Context: this: {this}; {nameof(_isEnded)}: {_isEnded}");
			//
			// 	var endTimestamp = TimeUtils.TimestampNow();
			// 	Duration = TimeUtils.DurationBetweenTimestamps(Timestamp, endTimestamp);
			// 	_logger.Trace()
			// 		?.Log("Ended {Transaction}. Start time: {Time} (as timestamp: {Timestamp})," +
			// 			" End time: {Time} (as timestamp: {Timestamp}), Duration: {Duration}ms",
			// 			this, TimeUtils.FormatTimestampForLog(Timestamp), Timestamp,
			// 			TimeUtils.FormatTimestampForLog(endTimestamp), endTimestamp, Duration);
			// }
			//
			// _activity?.Stop();
			//
			// var isFirstEndCall = !_isEnded;
			// _isEnded = true;
			// if (!isFirstEndCall) return;
			//
			// _sender.QueueTransaction(this);
			// _currentExecutionSegmentsContainer.CurrentTransaction = null;
		}

		public ISpan StartSpan(string name, string type, string subType = null, string action = null) =>
			StartSpanInternal(name, type, subType, action);

		internal RumSpan StartSpanInternal(string name, string type, string subType = null, string action = null,
			InstrumentationFlag instrumentationFlag = InstrumentationFlag.None, bool captureStackTraceOnStart = false
		)
		{
			var retVal = new RumSpan(name, type, Id, TraceId, this, _payloadSender, _logger, _executionSegmentsContainer,
				instrumentationFlag: instrumentationFlag, captureStackTraceOnStart: captureStackTraceOnStart);

			if (!string.IsNullOrEmpty(subType)) retVal.Subtype = subType;

			if (!string.IsNullOrEmpty(action)) retVal.Action = action;

			_logger.Trace()?.Log("Starting {SpanDetails}", retVal.ToString());
			return retVal;
		}

		public string EnsureParentId() => throw new NotImplementedException();
	}
}
