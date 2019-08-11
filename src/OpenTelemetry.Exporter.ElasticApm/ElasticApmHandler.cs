using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Elastic.Apm;
using Elastic.Apm.Api;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.ElasticApm
{
	public class ElasticApmHandler : IHandler
	{
		public Task ExportAsync(IEnumerable<SpanData> spanDataList)
		{
			foreach (var span in spanDataList)
			{
				if (span.ParentSpanId == new ActivitySpanId()) //create transaction
				{
					var transaction = new Elastic.Apm.Api.Transaction();
					transaction.Id = span.Context.SpanId.ToString();
					transaction.Name = span.Name;
					transaction.Type = span.Kind.ToString();
					transaction.Timestamp = ((DateTimeOffset)span.StartTimestamp).ToUnixTimeMilliseconds() * 1000;
					transaction.Duration = (span.EndTimestamp - span.StartTimestamp).TotalMilliseconds;

					transaction.TraceId = span.Context.TraceId.ToString();

					Agent.Instance.PayloadSender.QueueTransaction(transaction);
				}
				else //create span
				{
					var elasticSpan = new Elastic.Apm.Api.Span();
					elasticSpan.Id = span.Context.SpanId.ToString();

					elasticSpan.Name = span.Name;
					elasticSpan.Type = span.Kind.ToString();
					elasticSpan.Timestamp = ((DateTimeOffset)span.StartTimestamp).ToUnixTimeMilliseconds() * 1000;
					elasticSpan.Duration = (span.EndTimestamp - span.StartTimestamp).TotalMilliseconds;

					elasticSpan.TraceId = span.Context.TraceId.ToString();
					elasticSpan.ParentId = span.ParentSpanId.ToString();

					if (span.Kind == SpanKind.Client)
					{
						string data = null;
						string target = null;
						string type = null;
						string userAgent = null;

						string errorAttr = null;
						string httpStatusCodeAttr = null;
						string httpMethodAttr = null;
						string httpPathAttr = null;
						string httpHostAttr = null;

						string httpUserAgentAttr = null;
						string httpRouteAttr = null;
						string httpPortAttr = null;
						string httpUrlAttr = null;

						var httpContext = new Http();
						foreach (var attr in span.Attributes.AttributeMap)
						{
							switch (attr.Key)
							{
								case "http.url":
									httpUrlAttr = attr.Value.ToString();
									break;
								case "error":
									errorAttr = attr.Value.ToString();
									break;
								case "http.method":
									httpContext.Method = attr.Value.ToString();
									break;
								case "http.path":
									httpPathAttr = attr.Value.ToString();
									break;
								case "http.host":
									httpHostAttr = attr.Value.ToString();
									break;
								case "http.status_code":
									if (int.TryParse(attr.Value.ToString(), out var intVal))
									{
										httpContext.StatusCode = intVal;
									}
									break;
								case "http.user_agent":
									httpUserAgentAttr = attr.Value.ToString();
									break;
								case "http.route":
									httpRouteAttr = attr.Value.ToString();
									break;
								case "http.port":
									httpPortAttr = attr.Value.ToString();
									break;
								default:
									var value = attr.Value.ToString();
									//AddPropertyWithAdjustedName(result.Properties, attr.Key, value);

									break;
							}
						}


						Uri url = null;

						if (httpUrlAttr != null)
						{
							var urlString = httpUrlAttr;
							Uri.TryCreate(urlString, UriKind.RelativeOrAbsolute, out url);
						}

						if (url == null)
						{
							var urlString = string.Empty;
							if (!string.IsNullOrEmpty(httpHostAttr))
							{
								urlString += "https://" + httpHostAttr;

								if (!string.IsNullOrEmpty(httpPortAttr))
								{
									urlString += ":" + httpPortAttr;
								}
							}

							if (!string.IsNullOrEmpty(httpPathAttr))
							{
								if (httpPathAttr[0] != '/')
								{
									urlString += '/';
								}

								urlString += httpPathAttr;
							}

							if (!string.IsNullOrEmpty(urlString))
							{
								if (Uri.TryCreate(urlString, UriKind.RelativeOrAbsolute, out url))
								{
									httpContext.Url = url.ToString();
								}
							}
						}

						elasticSpan.Context.Http = httpContext;
					}

					Agent.Instance.PayloadSender.QueueSpan(elasticSpan);
				}
			}

			return Task.CompletedTask;
		}
	}
}
