using System;
using System.Collections.Generic;
using Elastic.Apm.Config;
using Newtonsoft.Json;

namespace Elastic.Apm.Report.Serialization
{
	internal class DictionarySanitizerConverter : JsonConverter<Dictionary<string, string>>
	{
		public DictionarySanitizerConverter(IConfigurationReader configurationReader)
		{
			
			//throw new NotImplementedException();
		}

		public override void WriteJson(JsonWriter writer, Dictionary<string, string> labels, JsonSerializer serializer)
		{
			writer.WriteStartObject();
			foreach (var keyValue in labels)
			{
				writer.WritePropertyName(SerializationUtils.TrimToPropertyMaxLength(keyValue.Key));

				if (keyValue.Value != null)
					writer.WriteValue(SerializationUtils.TrimToPropertyMaxLength(keyValue.Value));
				else
					writer.WriteNull();
			}
			writer.WriteEndObject();
		}

		public override Dictionary<string, string> ReadJson(JsonReader reader, Type objectType, Dictionary<string, string> existingValue,
			bool hasExistingValue, JsonSerializer serializer
		)
			=> serializer.Deserialize<Dictionary<string, string>>(reader);
	}
}
