using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Elastic.Apm.Config;
using Elastic.Apm.Logging;
using Elastic.Apm.Tests.Mocks;
using Elastic.Apm.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using SampleAspNetCoreApp;
using Xunit;
using Xunit.Abstractions;

namespace Elastic.Apm.AspNetCore.Tests
{
	public class SanitizeFieldNamesTests : LoggingTestBase, IClassFixture<WebApplicationFactory<Startup>>
	{
		private MockPayloadSender _capturedPayload;
		private HttpClient _client;
		private readonly IApmLogger _logger;
		private readonly WebApplicationFactory<Startup> _factory;

		public SanitizeFieldNamesTests(WebApplicationFactory<Startup> factory, ITestOutputHelper xUnitOutputHelper) : base(xUnitOutputHelper)
		{
			_logger = LoggerBase.Scoped(nameof(SanitizeFieldNamesTests));
			_factory = factory;

			// We need to ensure Agent.Instance is created because we need _agent to use Agent.Instance CurrentExecutionSegmentsContainer
			AgentSingletonUtils.EnsureInstanceCreated();
		}

		private void CreateAgent(string sanitizeFieldNames = null)
		{
			var agentComponents = sanitizeFieldNames == null
				? new TestAgentComponents(
					_logger,
					new MockConfigSnapshot(_logger),
					currentExecutionSegmentsContainer: Agent.Instance.TracerInternal.CurrentExecutionSegmentsContainer)
				: new TestAgentComponents(
					_logger,
					new MockConfigSnapshot(_logger, sanitizeFieldNames: sanitizeFieldNames),
					currentExecutionSegmentsContainer: Agent.Instance.TracerInternal.CurrentExecutionSegmentsContainer);

			var agent = new ApmAgent(agentComponents);
			ApmMiddlewareExtension.UpdateServiceInformation(agent.Service);

			_capturedPayload = agent.PayloadSender as MockPayloadSender;
			_client = Helper.GetClient(agent, _factory);
		}


		[InlineData("*mySecurityHeader", new[] { "abcmySecurityHeader", "aSecurityHeader", "mySecurityHeader" })]
		[InlineData("mySecurityHeader*", new[] { "mySecurityHeaderAbc", "mySecurityHeaderAbc1", "mySecurityHeader" })]
		[InlineData("*mySecurityHeader*", new[] { "AbcmySecurityHeaderAbc", "aSecurityHeaderA", "mySecurityHeader" })]
		[InlineData("mysecurityheader", new[] { "mySECURITYHeader" })]
		[Theory]
		public async Task CustomSanitizeFieldNameSetting(string sanitizeFieldNames, string[] headerNames)
		{
			CreateAgent(sanitizeFieldNames);

			foreach (var header in headerNames) _client.DefaultRequestHeaders.Add(header, "123");

			await _client.GetAsync("/Home/SimplePage");

			_capturedPayload.Transactions.Should().ContainSingle();
			_capturedPayload.FirstTransaction.Context.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Headers.Should().NotBeNull();

			foreach (var header in headerNames)
				_capturedPayload.FirstTransaction.Context.Request.Headers[header].Should().Be("[REDACTED]");
		}


		[InlineData("mysecurityheader", "mySECURITYHeader", true)]
		[InlineData("(?-i)mysecurityheader", "mySECURITYHeader", false)]
		[InlineData("(?-i)mySECURITYheader", "mySECURITYheader", true)]
		[InlineData("(?-i)*mySECURITYheader", "TestmySECURITYheader", true)]
		[InlineData("(?-i)*mySECURITYheader", "TestmysecURITYheader", false)]
		[InlineData("(?-i)mySECURITYheader*", "mySECURITYheaderTest", true)]
		[InlineData("(?-i)mySECURITYheader*", "mysecURITYheaderTest", false)]
		[InlineData("(?-i)*mySECURITYheader*", "TestmySECURITYheaderTest", true)]
		[InlineData("(?-i)*mySECURITYheader*", "TestmysecURITYheaderTest", false)]
		[Theory]
		public async Task CustomSanitizeFieldNameSettingWithCaseSensitivity(string sanitizeFieldNames, string headerName, bool shouldBeSanitized)
		{
			CreateAgent(sanitizeFieldNames);
			const string headerValue = "123";

			_client.DefaultRequestHeaders.Add(headerName, headerValue);

			await _client.GetAsync("/Home/SimplePage");

			_capturedPayload.Transactions.Should().ContainSingle();
			_capturedPayload.FirstTransaction.Context.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Headers.Should().NotBeNull();

			_capturedPayload.FirstTransaction.Context.Request.Headers[headerName].Should().Be(shouldBeSanitized ? "[REDACTED]" : headerValue);
		}

		/// <summary>
		/// Tests the default SanitizeFieldNames -
		/// It sends an HTTP GET with the given headers and makes sure all of them are
		/// sanitized.
		/// </summary>
		/// <param name="headerName"></param>
		/// <returns></returns>
		[InlineData("password")]
		[InlineData("pwd")]
		[InlineData("passwd")]
		[InlineData("secret")]
		[InlineData("secretkey")] //*key
		[InlineData("usertokensecret")] //*token*
		[InlineData("usersessionid")] //*session
		[InlineData("secretcreditcard")] //*credit*
		[InlineData("creditcardnumber")] //*card
		[Theory]
		public async Task SanitizeFieldNamesDefaults(string headerName)
		{
			CreateAgent();
			_client.DefaultRequestHeaders.Add(headerName, "123");
			await _client.GetAsync("/Home/SimplePage");

			_capturedPayload.Transactions.Should().ContainSingle();
			_capturedPayload.FirstTransaction.Context.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Headers.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Headers[headerName].Should().Be("[REDACTED]");
		}

		/// <summary>
		/// ASP.NET Core seems to rewrite the name of these headers (so <code>authorization</code> becomes <code>Authorization</code>).
		/// Our "by default case insensitivity" still works, the only difference is that if we send a header with name
		/// <code>authorization</code> it'll be captured as <code>Authorization</code> (capital letter).
		///
		/// Otherwise same as <see cref="SanitizeFieldNamesDefaults"/>.
		///
		/// </summary>
		/// <param name="headerName">The original header name sent in the HTTP GET</param>
		/// <param name="returnedHeaderName">The header name (with capital letter) seen on the request in ASP.NET Core</param>
		/// <returns></returns>
		[InlineData("authorization", "Authorization")]
		[InlineData("set-cookie", "Set-Cookie")]
		[Theory]
		public async Task SanitizeFieldNamesDefaultsKnownHeaders(string headerName, string returnedHeaderName)
		{
			CreateAgent();
			_client.DefaultRequestHeaders.Add(headerName, "123");
			await _client.GetAsync("/Home/SimplePage");

			_capturedPayload.Transactions.Should().ContainSingle();
			_capturedPayload.FirstTransaction.Context.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Headers.Should().NotBeNull();
			_capturedPayload.FirstTransaction.Context.Request.Headers[returnedHeaderName].Should().Be("[REDACTED]");
		}
	}
}
