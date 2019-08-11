using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Elastic.Apm.Api
{
	public class Span : ISpan
	{
		public double? Duration { get; set; }
		public string Id { get; set; }
		public bool IsSampled { get; }
		public Dictionary<string, string> Labels { get; }
		public string Name { get; set; }
		public DistributedTracingData OutgoingDistributedTracingData { get; set; }

		[JsonProperty("parent_id")]
		public string ParentId { get; set; }

		[JsonProperty("trace_id")]
		public string TraceId { get; set; }

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

		public void End() => throw new NotImplementedException();

		public ISpan StartSpan(string name, string type, string subType = null, string action = null) => throw new NotImplementedException();

		public string Action { get; set; }
		public SpanContext Context { get; }
		public string Subtype { get; set; }
		public long Timestamp { get; set; }
		public string TransactionId { get; set; }
		public string Type { get; set; }
	}
}
