using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using Elastic.Apm.Api;
using Elastic.Apm.AspNetCore.Extensions;
using Elastic.Apm.Config;
using Elastic.Apm.DistributedTracing;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;
using Elastic.Apm.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace Elastic.Apm.AspNetCore
{
	/// <summary>
	/// A helper class to capture an <see cref="HttpContext" /> as a transaction.
	/// </summary>
	internal static class WebRequestTransactionCreator
	{
		internal static ITransaction StartTransactionAsync(HttpContext context, IApmLogger logger, Tracer tracer, IConfigSnapshot configSnapshot)
		{
			try
			{
				if (WildcardMatcher.IsAnyMatch(configSnapshot.TransactionIgnoreUrls, context.Request.Path))
				{
					logger.Debug()?.Log("Request ignored based on TransactionIgnoreUrls, url: {urlPath}", context.Request.Path);
					return null;
				}

				ITransaction transaction;
				var transactionName = $"{context.Request.Method} {context.Request.Path}";

				if (context.Request.Headers.ContainsKey(TraceContext.TraceParentHeaderNamePrefixed)
					|| context.Request.Headers.ContainsKey(TraceContext.TraceParentHeaderName))
				{
					var headerValue = context.Request.Headers.ContainsKey(TraceContext.TraceParentHeaderName)
						? context.Request.Headers[TraceContext.TraceParentHeaderName].ToString()
						: context.Request.Headers[TraceContext.TraceParentHeaderNamePrefixed].ToString();

					var tracingData = context.Request.Headers.ContainsKey(TraceContext.TraceStateHeaderName)
						? TraceContext.TryExtractTracingData(headerValue, context.Request.Headers[TraceContext.TraceStateHeaderName].ToString())
						: TraceContext.TryExtractTracingData(headerValue);

					if (tracingData != null)
					{
						logger.Debug()
							?.Log(
								"Incoming request with {TraceParentHeaderName} header. DistributedTracingData: {DistributedTracingData}. Continuing trace.",
								TraceContext.TraceParentHeaderNamePrefixed, tracingData);

						transaction = tracer.StartTransaction(transactionName, ApiConstants.TypeRequest, tracingData);
					}
					else
					{
						logger.Debug()
							?.Log(
								"Incoming request with invalid {TraceParentHeaderName} header (received value: {TraceParentHeaderValue}). Starting trace with new trace id.",
								TraceContext.TraceParentHeaderNamePrefixed, headerValue);

						transaction = tracer.StartTransaction(transactionName, ApiConstants.TypeRequest);
					}
				}
				else
				{
					logger.Debug()?.Log("Incoming request. Starting Trace.");
					transaction = tracer.StartTransaction(transactionName, ApiConstants.TypeRequest);
				}

				return transaction;
			}
			catch (Exception ex)
			{
				logger?.Error()?.LogException(ex, "Exception thrown while trying to start transaction");
				return null;
			}
		}

		internal static void FillSampledTransactionContextRequest(Transaction transaction, HttpContext context, IApmLogger logger)
		{
			if (transaction.IsSampled) FillSampledTransactionContextRequest(context, transaction, logger);
		}

		private static void FillSampledTransactionContextRequest(HttpContext context, Transaction transaction, IApmLogger logger)
		{
			try
			{
				if (context?.Request == null) return;

				var url = new Url
				{
					Full = context.Request.GetEncodedUrl(),
					HostName = context.Request.Host.Host,
					Protocol = GetProtocolName(context.Request.Protocol),
					Raw = GetRawUrl(context.Request, logger) ?? context.Request.GetEncodedUrl(),
					PathName = context.Request.Path,
					Search = context.Request.QueryString.Value.Length > 0 ? context.Request.QueryString.Value.Substring(1) : string.Empty
				};

				transaction.Context.Request = new Request(context.Request.Method, url)
				{
					Socket = new Socket { Encrypted = context.Request.IsHttps, RemoteAddress = context.Connection?.RemoteIpAddress?.ToString() },
					HttpVersion = GetHttpVersion(context.Request.Protocol),
					Headers = GetHeaders(context.Request.Headers, transaction.ConfigSnapshot)
				};

				transaction.CollectRequestBody(false, context.Request, logger, transaction.ConfigSnapshot);
			}
			catch (Exception ex)
			{
				// context.request is optional: https://github.com/elastic/apm-server/blob/64a4ab96ba138050fe496b17d31deb2cf8830deb/docs/spec/request.json#L5
				logger?.Error()
					?.LogException(ex, "Exception thrown while trying to fill request context for sampled transaction {TransactionId}",
						transaction.Id);
			}
		}

		private static Dictionary<string, string> GetHeaders(IHeaderDictionary headers, IConfigSnapshot configSnapshot) =>
			configSnapshot.CaptureHeaders && headers != null
				? headers.ToDictionary(header => header.Key, header => header.Value.ToString())
				: null;

		private static string GetRawUrl(HttpRequest httpRequest, IApmLogger logger)
		{
			try
			{
				var rawPathAndQuery = httpRequest.HttpContext.Features.Get<IHttpRequestFeature>()?.RawTarget;

				if (!string.IsNullOrEmpty(rawPathAndQuery) && rawPathAndQuery.Count() > 0 && rawPathAndQuery[0] != '/')
					return rawPathAndQuery;

				return rawPathAndQuery == null ? null : UriHelper.BuildAbsolute(httpRequest.Scheme, httpRequest.Host, rawPathAndQuery);
			}
			catch (Exception e)
			{
				logger.Warning()?.LogException(e, "Failed reading RawUrl");
				return null;
			}
		}

		private static string GetProtocolName(string protocol)
		{
			switch (protocol)
			{
				case { } s when string.IsNullOrEmpty(s):
					return string.Empty;
				case { } s when s.StartsWith("HTTP", StringComparison.InvariantCulture): //in case of HTTP/2.x we only need HTTP
					return "HTTP";
				default:
					return protocol;
			}
		}

		private static string GetHttpVersion(string protocolString)
		{
			switch (protocolString)
			{
				case "HTTP/1.0":
					return "1.0";
				case "HTTP/1.1":
					return "1.1";
				case "HTTP/2.0":
					return "2.0";
				case null:
					return "unknown";
				default:
					return protocolString.Replace("HTTP/", string.Empty);
			}
		}

		internal static void StopTransaction(Transaction transaction, HttpContext context, IApmLogger logger)
		{
			if (transaction == null) return;

			var grpcCallInfo = CollectGrpcInfo();

			try
			{
				if (!transaction.HasCustomName)
				{
					//fixup Transaction.Name - e.g. /user/profile/1 -> /user/profile/{id}
					var routeData = context.GetRouteData()?.Values;

					if (routeData != null && context.Response.StatusCode != StatusCodes.Status404NotFound)
					{
						logger?.Trace()?.Log("Calculating transaction name based on route data");
						var name = Transaction.GetNameFromRouteContext(routeData);

						if (!string.IsNullOrWhiteSpace(name)) transaction.Name = $"{context.Request.Method} {name}";
					}
					else if (context.Response.StatusCode == StatusCodes.Status404NotFound)
					{
						logger?.Trace()
							?
							.Log("No route data found or status code is 404 - setting transaction name to 'unknown route");
						transaction.Name = $"{context.Request.Method} unknown route";
					}
				}

				if (grpcCallInfo == default)
				{
					transaction.Result = Transaction.StatusCodeToResult(GetProtocolName(context.Request.Protocol), context.Response.StatusCode);

					if (context.Response.StatusCode >= 500)
						transaction.Outcome = Outcome.Failure;
					else
						transaction.Outcome = Outcome.Success;
				}
				else
				{
					transaction.Name = grpcCallInfo.methodname;
					transaction.Result = GrpcHelper.GrpcReturnCodeToString(grpcCallInfo.result);

					if (transaction.Result == "OK")
						transaction.Outcome = Outcome.Success;
					else
						transaction.Outcome = Outcome.Failure;
				}

				if (transaction.IsSampled)
				{
					FillSampledTransactionContextResponse(context, transaction, logger);
					FillSampledTransactionContextUser(context, transaction, logger);
				}
			}
			catch (Exception ex)
			{
				logger?.Error()?.LogException(ex, "Exception thrown while trying to stop transaction");
			}
			finally
			{
				transaction.End();
			}
		}

		/// <summary>
		/// Collects gRPC info for the given request
		/// </summary>
		/// <returns>default if it's not a grpc call, otherwise the Grpc method name and result as a tuple </returns>
		private static (string methodname, string result) CollectGrpcInfo()
		{
			var parentActivty = Activity.Current.Parent;
			(string methodname, string result) grpcCallInfo = default;

			if (parentActivty != null)
			{
				var grpcMethodName = parentActivty.Tags.Where(n => n.Key == "grpc.method").FirstOrDefault().Value;
				var grpcStatusCode = parentActivty.Tags.Where(n => n.Key == "grpc.status_code").FirstOrDefault().Value;

				if (!string.IsNullOrEmpty(grpcMethodName) && !string.IsNullOrEmpty(grpcStatusCode))
					grpcCallInfo = (grpcMethodName, grpcStatusCode);
			}

			return grpcCallInfo;
		}

		private static void FillSampledTransactionContextResponse(HttpContext context, Transaction transaction, IApmLogger logger)
		{
			try
			{
				transaction.Context.Response = new Response
				{
					Finished = context.Response.HasStarted, //TODO ?
					StatusCode = context.Response.StatusCode,
					Headers = GetHeaders(context.Response.Headers, transaction.ConfigSnapshot)
				};

				logger?.Trace()?.Log("Filling transaction.Context.Response, StatusCode: {statuscode}", transaction.Context.Response.StatusCode);
			}
			catch (Exception ex)
			{
				// context.response is optional: https://github.com/elastic/apm-server/blob/64a4ab96ba138050fe496b17d31deb2cf8830deb/docs/spec/context.json#L16
				logger?.Error()
					?.LogException(ex, "Exception thrown while trying to fill response context for sampled transaction {TransactionId}",
						transaction.Id);
			}
		}

		private static void FillSampledTransactionContextUser(HttpContext context, Transaction transaction, IApmLogger logger)
		{
			try
			{
				if (context.User?.Identity != null && context.User.Identity.IsAuthenticated && transaction.Context.User == null)
				{
					transaction.Context.User = new User
					{
						UserName = context.User.Identity.Name,
						Id = GetClaimWithFallbackValue(ClaimTypes.NameIdentifier, Consts.OpenIdClaimTypes.UserId),
						Email = GetClaimWithFallbackValue(ClaimTypes.Email, Consts.OpenIdClaimTypes.Email)
					};

					logger.Debug()?.Log("Captured user - {CapturedUser}", transaction.Context.User);
				}
			}
			catch (Exception ex)
			{
				// context.user is optional: https://github.com/elastic/apm-server/blob/64a4ab96ba138050fe496b17d31deb2cf8830deb/docs/spec/user.json#L5
				logger?.Error()
					?.LogException(ex, "Exception thrown while trying to fill user context for sampled transaction {TransactionId}",
						transaction.Id);
			}

			string GetClaimWithFallbackValue(string claimType, string fallbackClaimType)
			{
				var idClaims = context.User.Claims.Where(n => n.Type == claimType || n.Type == fallbackClaimType);
				var enumerable = idClaims.ToList();
				return enumerable.Any() ? enumerable.First().Value : string.Empty;
			}
		}
	}
}
