using System.Collections.Generic;
using System.Net.Mime;
using System.Threading.Tasks;
using Elastic.Apm.Api;
using Elastic.Apm.Config;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;
using Microsoft.AspNetCore.Http;

namespace Elastic.Apm.AspNetCore.Extensions
{
	internal static class TransactionExtensions
	{
		/// <summary>
		/// Collects the Request body (and possibly additional information in the future)
		/// in the transaction
		/// </summary>
		/// <param name="transaction">Transaction object</param>
		/// <param name="httpContext">Current http context</param>
		/// <param name="configurationReader"></param>
		/// <param name="logger">Logger object</param>
		/// <param name="matcherList"></param>
		public static void CollectRequestInfo(this ITransaction transaction, HttpContext httpContext, IConfigurationReader configurationReader,
			IApmLogger logger, List<WildcardMatcher> matcherList
		)
		{
			var body = Consts.BodyRedacted; // According to the documentation - the default value of 'body' is '[Redacted]'

			// We need to parse the content type and check it's not null and is of valid value
			if (!string.IsNullOrEmpty(httpContext?.Request?.ContentType))
			{
				var contentType = new ContentType(httpContext.Request.ContentType);

				//Request must not be null and the content type must be matched with the 'captureBodyContentTypes' configured
				if (httpContext?.Request != null && configurationReader.CaptureBodyContentTypes.ContainsLike(contentType.MediaType))
				{
					body = httpContext.Request.ExtractRequestBody(logger, matcherList);
				}
				transaction.Context.Request.Body = string.IsNullOrEmpty(body) ? Consts.BodyRedacted : body;
			}
		}
	}
}
