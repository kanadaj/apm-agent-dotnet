using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elastic.Apm.AspNetCore.DiagnosticListener;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;

namespace Elastic.Apm.AspNetCore.Extensions
{
	internal static class HttpRequestExtensions
	{
		/// <summary>
		/// Extracts the request body. In case content type is application/x-www-form-urlencoded or multipart/form-data it builds
		/// the body with <code>request.ReadFormAsync()</code> otherwise it just reads <code>request.body</code>.
		/// </summary>
		/// <param name="request"></param>
		/// <param name="logger"></param>
		/// <param name="wildcardMatchers"></param>
		/// <returns></returns>
		public static async Task<string> ExtractRequestBodyAsync(this HttpRequest request, IApmLogger logger, List<WildcardMatcher> wildcardMatchers)
		{
			string body = null;
			try
			{
				if (request.ContentType.ToLower() != "application/x-www-form-urlencoded" && request.ContentType.ToLower() != "multipart/form-data")
					return ExtractRequestBody(request, logger, wildcardMatchers);

				logger.Trace()?.Log("Capturing request body, WildcardMatcher: {WildcardMatcherStrings}",
					string.Join(",", wildcardMatchers.Select(i => i.GetMatcher()).ToArray()));

				logger.Trace()?.Log("Reading request body via HttpRequest.ReadFormAsync()");

				var formsCollection = await request.ReadFormAsync();
				body = BuildBodyStringFromIFormCollection(formsCollection, wildcardMatchers);
			}
			catch (Exception e)
			{
				logger.Error()?.LogException(e, "Error reading request body");
			}

			return body;
		}

		/// <summary>
		/// Extracts the request body using measure to prevent the 'read once' problem (cannot read after the body ha been already
		/// read).
		/// Prefer <see cref="ExtractRequestBodyAsync" /> if possible.
		/// This is a non-async method because this is used in the <see cref="AspNetCoreDiagnosticListener" /> and in an exception
		/// filter (which also must
		/// be non-async).
		/// </summary>
		/// <param name="request"></param>
		/// <param name="logger"></param>
		/// <param name="wildcardMatchers"></param>
		/// <returns></returns>
		public static string ExtractRequestBody(this HttpRequest request, IApmLogger logger, List<WildcardMatcher> wildcardMatchers)
		{
			logger.Trace()?.Log("Capturing request body, WildcardMatcher: {WildcardMatcherStrings}",
				string.Join(",", wildcardMatchers.Select(i => i.GetMatcher()).ToArray()));

			string body = null;

			try
			{
				if (request.ContentType.ToLower() == "application/x-www-form-urlencoded"
					|| request.ContentType.ToLower() == "multipart/form-data" && request.Form != null)
				{
					logger.Trace()?.Log("Reading request body via HttpRequest.Form");

					// request.Form is not ideal, request.ReadFormAsync() would be better, which is used in
					// ExtractRequestBodyAsync.
					body = BuildBodyStringFromIFormCollection(request.Form, wildcardMatchers);
				}
				else
				{
					logger.Trace()?.Log("Reading request body via HttpRequest.Body");
					request.EnableRewind();
					request.Body.Position = 0;

					using (var reader = new StreamReader(request.Body,
						Encoding.UTF8,
						false,
						1024 * 2,
						true))
					{
						body = reader.ReadToEnd();
						// Truncate the body to the first 2kb if it's longer
						if (body.Length > Consts.RequestBodyMaxLength) body = body.Substring(0, Consts.RequestBodyMaxLength);
						request.Body.Position = 0;
					}
				}
			}
			catch (IOException ioException)
			{
				logger.Error()?.LogException(ioException, "IO Error reading request body");
			}
			catch (Exception e)
			{
				logger.Error()?.LogException(e, "Error reading request body");
			}

			return body;
		}

		/// <summary>
		/// Builds a string from the <code>IFormCollection</code> collection and also applies sanitization.
		/// </summary>
		/// <param name="formsCollection"></param>
		/// <param name="wildcardMatchers"></param>
		/// <returns></returns>
		private static string BuildBodyStringFromIFormCollection(IFormCollection formsCollection, List<WildcardMatcher> wildcardMatchers)
		{
			var sb = new StringBuilder();

			foreach (var form in formsCollection)
			{
				var newItem = new StringBuilder();

				if (sb.Length > 0)
					newItem.Append("&");

				newItem.Append(form.Key);
				newItem.Append("=");

				newItem.Append(WildcardMatcher.IsAnyMatch(wildcardMatchers, form.Key) ? "[REDACTED]" : form.Value.ToString());

				if (newItem.Length <= Consts.RequestBodyMaxLength)
					sb.Append(newItem);
				else
					break;
			}

			return sb.ToString();
		}
	}
}
