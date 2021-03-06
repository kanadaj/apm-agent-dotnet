// Licensed to Elasticsearch B.V under one or more agreements.
// Elasticsearch B.V licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information

using System;
using Elastic.Apm.Report.Serialization;

namespace Elastic.Apm.Api.Constraints
{
	/// <summary>
	/// An attribute to mark fields for sanitization. This attribute is known to <see cref="ElasticApmContractResolver"/> and it applies a Converter
	/// to sanitize field(s) accordingly.
	/// </summary>
	internal sealed class SanitizeAttribute : Attribute { }
}
