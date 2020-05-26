// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Elastic.Apm.Api;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;
using Elastic.Apm.Report;
using Newtonsoft.Json;

namespace Elastic.Apm.Model
{
	internal class RumSpan : ISpan
	{
		private readonly RumTransaction _enclosingTransaction;
		private readonly IPayloadSender _payloadSender;
		private readonly IApmLogger _logger;
		private readonly ICurrentExecutionSegmentsContainer _currentExecutionSegmentsContainer;
		private readonly Span _parentSpan;
		public RumSpan(string name,
			string type,
			string parentId,
			string traceId,
			RumTransaction enclosingTransaction,
			IPayloadSender payloadSender,
			IApmLogger logger,
			ICurrentExecutionSegmentsContainer currentExecutionSegmentsContainer,
			Span parentSpan = null,
			InstrumentationFlag instrumentationFlag = InstrumentationFlag.None,
			bool captureStackTraceOnStart = false
		)
		{
			_enclosingTransaction = enclosingTransaction;
			_payloadSender = payloadSender;
			_logger = logger;
			_currentExecutionSegmentsContainer = currentExecutionSegmentsContainer;
			_parentSpan = parentSpan;
			Type = type;
			Name = name;
			ParentId = parentId;

			Timestamp = TimeUtils.TimestampNow();
			Id = RandomGenerator.GenerateRandomBytesAsString(new byte[8]);
			TraceId = enclosingTransaction.TraceId;

			_currentExecutionSegmentsContainer.CurrentSpan = this;
		}

		public double? Duration { get; set; }
		public string Id { get; }
		public bool IsSampled { get; }
		public Dictionary<string, string> Labels { get; }
		public string Name { get; set; }
		public DistributedTracingData OutgoingDistributedTracingData { get; }

		[JsonProperty("parent_id")]
		public string ParentId { get; }

		[JsonProperty("trace_id")]
		public string TraceId { get; }

		public void CaptureError(string message, string culprit, StackFrame[] frames, string parentId = null) => throw new NotImplementedException();

		public void CaptureException(Exception exception, string culprit = null, bool isHandled = false, string parentId = null) => throw new NotImplementedException();

		public void CaptureSpan(string name, string type, Action<ISpan> capturedAction, string subType = null, string action = null) => throw new NotImplementedException();

		public void CaptureSpan(string name, string type, Action capturedAction, string subType = null, string action = null) => throw new NotImplementedException();

		public T CaptureSpan<T>(string name, string type, Func<ISpan, T> func, string subType = null, string action = null) => throw new NotImplementedException();

		public T CaptureSpan<T>(string name, string type, Func<T> func, string subType = null, string action = null) => throw new NotImplementedException();

		public Task CaptureSpan(string name, string type, Func<Task> func, string subType = null, string action = null) => throw new NotImplementedException();

		public Task CaptureSpan(string name, string type, Func<ISpan, Task> func, string subType = null, string action = null) => throw new NotImplementedException();

		public Task<T> CaptureSpan<T>(string name, string type, Func<Task<T>> func, string subType = null, string action = null) => throw new NotImplementedException();

		public Task<T> CaptureSpan<T>(string name, string type, Func<ISpan, Task<T>> func, string subType = null, string action = null) => throw new NotImplementedException();

		public void End()
		{
			var endTimestamp = TimeUtils.TimestampNow();

			Duration = TimeUtils.DurationBetweenTimestamps(Timestamp, endTimestamp);
			_payloadSender.QueueSpan(this);
			_currentExecutionSegmentsContainer.CurrentSpan = null;
		}

		public ISpan StartSpan(string name, string type, string subType = null, string action = null) => throw new NotImplementedException();

		public string Action { get; set; }
		public SpanContext Context { get; }
		public List<CapturedStackFrame> StackTrace { get; }
		public string Subtype { get; set; }
		public long Timestamp { get; }
		public string TransactionId { get; }
		public string Type { get; set; }
	}
}
