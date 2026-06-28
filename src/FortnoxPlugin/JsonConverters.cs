using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Fortnox{
    public class DecimalAsStringConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Convert from string to decimal.
            if (reader.TokenType == JsonTokenType.String &&
                decimal.TryParse(reader.GetString(), out var value))
            {
                return value;
            }

            // fallback: if it's already a number, read it directly
            return reader.GetDecimal();
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            // Serialize decimal as string.
            writer.WriteStringValue(value.ToString());
        }
    }

    //To convert "" to null when deserializing nullable string enums
    public class NullableStringEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return Nullable.GetUnderlyingType(typeToConvert)?.IsEnum ?? false;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type enumType = Nullable.GetUnderlyingType(typeToConvert)!;

            return (JsonConverter)Activator.CreateInstance(
                typeof(NullableStringEnumConverter<>).MakeGenericType(enumType),
                new object[] { options }
            )!;
        }

        private class NullableStringEnumConverter<T> : JsonConverter<T?> where T : struct, Enum
        {
            // This field will hold the REAL converter, not the factory.
            private readonly JsonConverter<T> _enumConverter;

            public NullableStringEnumConverter(JsonSerializerOptions options)
            {
                var factory = new JsonStringEnumConverter(options.PropertyNamingPolicy, allowIntegerValues: true);

                _enumConverter = (JsonConverter<T>)factory.CreateConverter(typeof(T), options)!;
            }

            public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // Your special case for empty strings remains the same.
                if (reader.TokenType == JsonTokenType.String && string.IsNullOrEmpty(reader.GetString()))
                {
                    return null;
                }

                // If the JSON token is null, also return null
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return null;
                }

                // Now, delegate to the REAL converter instance. This will work.
                return _enumConverter.Read(ref reader, typeof(T), options);
            }

            public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
            {
                // The logic here is also slightly adjusted for correctness.
                // We handle the null case ourselves and pass the unwrapped value to the real converter.
                if (value.HasValue)
                {
                    _enumConverter.Write(writer, value.Value, options);
                }
                else
                {
                    writer.WriteNullValue();
                }
            }
        }
    }

    // To convert "" to null when deserializing nullable DateTime
    public class NullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Check for an empty string ""
            if (reader.TokenType == JsonTokenType.String && string.IsNullOrEmpty(reader.GetString()))
            {
                return null;
            }

            // Check for a standard null
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            // If it's not an empty string or null, try to parse it as a normal DateTime
            // Using TryGetDateTime is the most performant way.
            if (reader.TryGetDateTime(out DateTime dt))
            {
                return dt;
            }

            // Fallback for non-standard formats that Utf8JsonReader might not handle directly
            // but can be deserialized from a string.
            return JsonSerializer.Deserialize<DateTime>(ref reader, options);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd"));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

    public class StringToNullableDoubleConverter : JsonConverter<double?>
    {
        public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Handle standard null tokens
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            // Handle number tokens
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetDouble();
            }

            // Handle string tokens
            if (reader.TokenType == JsonTokenType.String)
            {
                string? stringValue = reader.GetString();

                // Treat empty or whitespace strings as null
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return null;
                }

                // Try to parse the string to a double.
                // Using InvariantCulture is important for consistent parsing of decimal points ('.').
                if (double.TryParse(stringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                {
                    return value;
                }
            }

            // If the token is not a string, number, or null, it's an error.
            throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
        {
            // Serialize the double? back to a JSON number or null.
            if (value.HasValue)
            {
                writer.WriteNumberValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}


