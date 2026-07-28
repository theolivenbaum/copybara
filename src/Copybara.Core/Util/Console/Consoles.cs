/*
 * Copyright (C) 2016 Google Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Text;
using Microsoft.Extensions.Logging;

namespace Copybara.Util.Console;

/// <summary>
/// Utility methods for working with <see cref="Console"/>s.
/// </summary>
public static class Consoles
{
    /// <summary>
    /// Logs text as separate lines using <see cref="Console.Info"/>. If <paramref name="text"/> is an
    /// empty string, does nothing.
    /// </summary>
    public static void LogLines(Console console, string prefix, string text)
    {
        ConsoleLogLines(prefix, text, console.Info);
    }

    /// <summary>
    /// Logs text as separate lines using <see cref="Console.Error"/>. If <paramref name="text"/> is an
    /// empty string, does nothing.
    /// </summary>
    public static void ErrorLogLines(Console console, string prefix, string text)
    {
        ConsoleLogLines(prefix, text, console.Error);
    }

    /// <summary>
    /// Logs text as separate lines using <see cref="Console.Verbose"/> if verbose is enabled.
    /// </summary>
    public static void VerboseLogLines(Console console, string prefix, string text)
    {
        ConsoleLogLines(prefix, text, console.Verbose);
    }

    private static void ConsoleLogLines(string prefix, string text, Action<string> logLevel)
    {
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            bool isLast = i == lines.Length - 1;
            if (line.Length == 0 && isLast)
            {
                break;
            }
            logLevel(prefix + line);
        }
    }

    public static void PrintCauseChain(
        LogLevel level, Console console, string[] args, Exception e, ILogger? logger = null)
    {
        var error = new StringBuilder(e.Message).Append('\n');
        // The list must stay mutable: exceptions suppressed further down the cause chain get
        // appended to it while walking the chain. .NET's closest analogue to Java's suppressed
        // exceptions is AggregateException.InnerExceptions.
        var suppressed = new List<Exception>(GetSuppressed(e));
        Exception? cause = e.InnerException;
        while (cause != null)
        {
            error.Append("  CAUSED BY: ").Append(PrintException(cause)).Append('\n');
            suppressed.AddRange(GetSuppressed(cause));
            cause = cause.InnerException;
        }
        foreach (Exception t in suppressed)
        {
            PrintCauseChain(level, console, args, t, logger);
        }
        console.Error(error.ToString());
        logger?.Log(level, e, "{Message}", FormatLogError(e.Message, args));
    }

    /// <summary>
    /// Returns the exceptions "suppressed" by <paramref name="t"/>. Java records these on any
    /// <c>Throwable</c>; in .NET the equivalent is the set of inner exceptions of an
    /// <see cref="AggregateException"/> other than the one already reported as the cause.
    /// </summary>
    private static IEnumerable<Exception> GetSuppressed(Exception t) =>
        t is AggregateException aggregate
            ? aggregate.InnerExceptions.Where(inner => !ReferenceEquals(inner, t.InnerException))
            : Enumerable.Empty<Exception>();

    private static string? PrintException(Exception t)
    {
        // In the Java original this special-cases EvalException to use getMessageWithStack(); the
        // C# EvalException port only exposes Message, so the plain message is used for all types.
        return t.Message;
    }

    public static string FormatLogError(string message, string[] args)
    {
        return $"{message} (command args: [{string.Join(", ", args)}])";
    }
}
