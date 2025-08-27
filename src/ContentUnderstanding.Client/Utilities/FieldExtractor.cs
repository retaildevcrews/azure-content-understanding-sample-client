using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ContentUnderstanding.Client.Utilities
{
    internal static class FieldExtractor
    {
        public static string ExtractFieldValue(JsonElement field)
        {
            try
            {
                if (field.TryGetProperty("type", out var typeElement))
                {
                    var fieldType = typeElement.GetString();
                    return fieldType?.ToLowerInvariant() switch
                    {
                        "string" when field.TryGetProperty("valueString", out var stringValue) => stringValue.GetString() ?? "N/A",
                        "number" when field.TryGetProperty("valueNumber", out var numberValue) => numberValue.GetDouble().ToString("F2"),
                        "array" when field.TryGetProperty("valueArray", out var arrayValue) => ExtractArrayValue(arrayValue),
                        "object" when field.TryGetProperty("valueObject", out var objectValue) => ExtractObjectValue(objectValue),
                        _ => field.ToString()
                    };
                }

                if (field.TryGetProperty("valueString", out var fallbackString))
                    return fallbackString.GetString() ?? "N/A";

                if (field.TryGetProperty("content", out var content))
                    return content.GetString() ?? "N/A";

                return field.ToString();
            }
            catch
            {
                return "Parse Error";
            }
        }

        private static string ExtractArrayValue(JsonElement arrayElement)
        {
            try
            {
                var items = new List<string>();
                foreach (var item in arrayElement.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var itemType) && itemType.GetString() == "object")
                    {
                        if (item.TryGetProperty("valueObject", out var valueObject))
                        {
                            items.Add(ExtractObjectValue(valueObject));
                        }
                    }
                    else
                    {
                        items.Add(ExtractFieldValue(item));
                    }
                }
                return items.Count > 0 ? string.Join("; ", items) : "Empty Array";
            }
            catch
            {
                return "Array Parse Error";
            }
        }

        private static string ExtractObjectValue(JsonElement objectElement)
        {
            try
            {
                var properties = new List<string>();
                foreach (var property in objectElement.EnumerateObject())
                {
                    var value = ExtractFieldValue(property.Value);
                    properties.Add($"{property.Name}: {value}");
                }
                return properties.Count > 0 ? $"[{string.Join(", ", properties)}]" : "Empty Object";
            }
            catch
            {
                return "Object Parse Error";
            }
        }
    }
}
