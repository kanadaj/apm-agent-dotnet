// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Elastic.Apm.Api;
using Elastic.Apm.BackendComm;
using Elastic.Apm.Config;
using Elastic.Apm.Logging;
using Elastic.Apm.Report.Serialization;

namespace Elastic.Apm.Report
{
	/// <summary>
	///  Responsible for sending the data to the server. It sends data to the RUM endpoint and implements RUM Intake V2.
	/// </summary>
	internal class RumPayloadSenderV2 : PayloadSenderBase
	{
		public RumPayloadSenderV2(IApmLogger logger, string dbgDerivedClassName, Service service, IConfigSnapshot config, Api.System system,
			HttpMessageHandler httpMessageHandler = null
		) : base(logger, config, service, system, httpMessageHandler, dbgDerivedClassName, false)
		 => IntakeEventsAbsoluteUrl = BackendCommUtils.ApmServerEndpoints.BuildIntakeV2RumEventsAbsoluteUrl(config.ServerUrls.First());
	}
}
