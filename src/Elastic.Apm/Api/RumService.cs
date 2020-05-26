// Licensed to Elasticsearch B.V under
// one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using Newtonsoft.Json;

namespace Elastic.Apm.Api
{
	public class RumService
	{
		//[JsonProperty("fw")]
		public RumFramework Framework { get; set; }

		//[JsonProperty("a")]
		public RumAgent Agent { get; set; }

		//[JsonProperty("n")]
		public string Name { get; set; }
	}

	public class RumAgent
	{
		//[JsonProperty("n")]
		public string Name { get; set; }

		//[JsonProperty("ve")]
		public string Version { get; set; }
	}

	public class RumFramework
	{
		//[JsonProperty("n")]
		public string Name { get; set; }

		//[JsonProperty("ve")]
		public string Version { get; set; }
	}
}
