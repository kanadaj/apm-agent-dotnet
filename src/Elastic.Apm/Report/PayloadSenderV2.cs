// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Elastic.Apm.Api;
using Elastic.Apm.BackendComm;
using Elastic.Apm.Config;
using Elastic.Apm.Filters;
using Elastic.Apm.Helpers;
using Elastic.Apm.Logging;
using Elastic.Apm.Metrics;
using Elastic.Apm.Model;
using Elastic.Apm.Report.Serialization;
using Newtonsoft.Json;

namespace Elastic.Apm.Report
{
	/// <summary>
	/// Responsible for sending the data to the server. Implements Intake V2.
	/// Each instance creates its own thread to do the work. Therefore, instances should be reused if possible.
	/// </summary>
	internal class PayloadSenderV2 : PayloadSenderBase
	{
		public PayloadSenderV2(IApmLogger logger, IConfigSnapshot config, Service service, Api.System system,
			HttpMessageHandler httpMessageHandler = null, string dbgName = null
		)
			: base(logger, config, service, system, httpMessageHandler, dbgName) =>
			IntakeEventsAbsoluteUrl =  BackendCommUtils.ApmServerEndpoints.BuildIntakeV2EventsAbsoluteUrl(config.ServerUrls.First())
	}

	internal class Metadata
	{
		[JsonConverter(typeof(LabelsJsonConverter))]
		public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

		// ReSharper disable once UnusedAutoPropertyAccessor.Global - used by Json.Net
		public Service Service { get; set; }

		// ReSharper disable once UnusedAutoPropertyAccessor.Global
		public Api.System System { get; set; }

		/// <summary>
		/// Method to conditionally serialize <see cref="Labels" /> - serialize only when there is at least one label.
		/// See
		/// <a href="https://www.newtonsoft.com/json/help/html/ConditionalProperties.htm">the relevant Json.NET Documentation</a>
		/// </summary>
		public bool ShouldSerializeLabels() => !Labels.IsEmpty();
	}
}
