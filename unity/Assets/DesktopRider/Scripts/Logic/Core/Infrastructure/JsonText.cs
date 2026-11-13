#nullable enable

using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace DesktopRider.Core.Infrastructure
{
    /// <summary>
    /// Reads and writes JSON text. Version 1 used System.Text.Json, which Unity does not include, so this uses
    /// Newtonsoft.Json set up to produce and accept the same files: camelCase names, enums as camelCase words,
    /// two-space indenting, comments and trailing commas tolerated.
    /// </summary>
    public static class JsonText
    {
        /// <summary>The settings used for every file Desktop Rider keeps on this computer.</summary>
        public static JsonSerializerSettings LocalFileSettings { get; } = CreateLocalFileSettings();

        /// <summary>Plain settings for text received from another program, where names are given explicitly.</summary>
        public static JsonSerializerSettings WireSettings { get; } = new()
        {
            DateParseHandling = DateParseHandling.None,
            CheckAdditionalContent = true,
            Culture = CultureInfo.InvariantCulture,
        };

        /// <summary>
        /// Understands <paramref name="json"/> as a <typeparamref name="T"/>. Returns null for the JSON word <c>null</c>
        /// or empty text. Throws <see cref="JsonException"/>, and nothing else, when the text cannot be understood.
        /// </summary>
        public static T? Read<T>(string json, JsonSerializerSettings settings)
            where T : class
        {
            Guard.NotNull(json, nameof(json));
            Guard.NotNull(settings, nameof(settings));
            try
            {
                return JsonConvert.DeserializeObject<T>(json, settings);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException or InvalidOperationException)
            {
                throw new JsonSerializationException("The text was not in the expected form.", ex);
            }
        }

        public static string Write(object value, JsonSerializerSettings settings)
        {
            Guard.NotNull(value, nameof(value));
            Guard.NotNull(settings, nameof(settings));
            return JsonConvert.SerializeObject(value, settings);
        }

        private static JsonSerializerSettings CreateLocalFileSettings()
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: false),
                },
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                DateParseHandling = DateParseHandling.None,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                CheckAdditionalContent = true,
                Culture = CultureInfo.InvariantCulture,
            };
            settings.Converters.Add(new StringEnumConverter(new CamelCaseNamingStrategy()));
            settings.Converters.Add(new TimeOnlyConverter());
            return settings;
        }

        /// <summary>Stores a time of day as text such as "22:00:00", exactly as version 1 did.</summary>
        private sealed class TimeOnlyConverter : JsonConverter
        {
            private static readonly string[] Formats = { "HH:mm:ss", "HH:mm:ss.FFFFFFF", "HH:mm" };

            public override bool CanConvert(Type objectType) =>
                objectType == typeof(TimeOnly) || objectType == typeof(TimeOnly?);

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value is TimeOnly time)
                {
                    writer.WriteValue(time.ToString(time.Ticks % TimeSpan.TicksPerSecond == 0 ? "HH:mm:ss" : "HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteNull();
                }
            }

            public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null && objectType == typeof(TimeOnly?))
                {
                    return null;
                }

                if (reader.TokenType == JsonToken.String
                    && TimeOnly.TryParseExact((string?)reader.Value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    return time;
                }

                throw new JsonSerializationException("A time of day must be written like \"22:00:00\".");
            }
        }
    }
}
