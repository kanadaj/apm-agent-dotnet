using System;
using System.Collections.Generic;
using System.Text;
using Elastic.Apm.Tests.Mocks;
using FluentAssertions;
using Xunit;

namespace Elastic.Apm.Tests.ApiTests
{
	/// <summary>
	/// Tests related to error capturing
	/// </summary>
	public class ErrorTests
	{
		[Fact]
		public void CaptureErrorOnTracer()
		{
			var payloadSender = new MockPayloadSender();

			using var agent = new ApmAgent(new TestAgentComponents(payloadSender: payloadSender));
			agent.Tracer.CaptureError("Bamm", "JustTest");

			payloadSender.Transactions.Should().BeEmpty();
			payloadSender.Spans.Should().BeEmpty();
			payloadSender.Errors.Count.Should().Be(1);
			payloadSender.FirstError.Exception.Message.Should().Be("Bamm");
			payloadSender.FirstError.Culprit.Should().Be("JustTest");
		}
	}
}
