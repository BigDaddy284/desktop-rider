#nullable enable

using System;
using System.Text;

namespace DesktopRider.Core.Logging
{
    /// <summary>
    /// Shorthand for writing log entries. A message may contain named gaps such as <c>{Name}</c>, which are
    /// filled, in order, from the values that follow it.
    /// </summary>
    public static class LoggerExtensions
    {
        public static void LogTrace(this ILogger logger, string message, params object?[] args) =>
            Write(logger, LogLevel.Trace, null, message, args);

        public static void LogDebug(this ILogger logger, string message, params object?[] args) =>
            Write(logger, LogLevel.Debug, null, message, args);

        public static void LogInformation(this ILogger logger, string message, params object?[] args) =>
            Write(logger, LogLevel.Information, null, message, args);

        public static void LogWarning(this ILogger logger, string message, params object?[] args) =>
            Write(logger, LogLevel.Warning, null, message, args);

        public static void LogWarning(this ILogger logger, Exception? exception, string message, params object?[] args) =>
            Write(logger, LogLevel.Warning, exception, message, args);

        public static void LogError(this ILogger logger, string message, params object?[] args) =>
            Write(logger, LogLevel.Error, null, message, args);

        public static void LogError(this ILogger logger, Exception? exception, string message, params object?[] args) =>
            Write(logger, LogLevel.Error, exception, message, args);

        public static void LogCritical(this ILogger logger, Exception? exception, string message, params object?[] args) =>
            Write(logger, LogLevel.Critical, exception, message, args);

        /// <summary>Fills each <c>{Name}</c> gap in <paramref name="message"/> with the next value. <c>{{</c> and <c>}}</c> give plain braces.</summary>
        internal static string Fill(string message, object?[]? args)
        {
            if (args is null || args.Length == 0 || message.IndexOf('{') < 0)
            {
                return message;
            }

            var text = new StringBuilder(message.Length + 16);
            var next = 0;
            for (var i = 0; i < message.Length; i++)
            {
                var c = message[i];
                if (c == '{' && i + 1 < message.Length && message[i + 1] == '{')
                {
                    text.Append('{');
                    i++;
                }
                else if (c == '}' && i + 1 < message.Length && message[i + 1] == '}')
                {
                    text.Append('}');
                    i++;
                }
                else if (c == '{')
                {
                    var end = message.IndexOf('}', i + 1);
                    if (end < 0 || next >= args.Length)
                    {
                        text.Append(message, i, message.Length - i);
                        break;
                    }

                    text.Append(LogText.Of(args[next++]));
                    i = end;
                }
                else
                {
                    text.Append(c);
                }
            }

            return text.ToString();
        }

        private static void Write(ILogger logger, LogLevel level, Exception? exception, string message, object?[] args)
        {
            Guard.NotNull(logger, nameof(logger));
            Guard.NotNull(message, nameof(message));
            if (logger.IsEnabled(level))
            {
                logger.Log(level, Fill(message, args), exception);
            }
        }
    }
}
