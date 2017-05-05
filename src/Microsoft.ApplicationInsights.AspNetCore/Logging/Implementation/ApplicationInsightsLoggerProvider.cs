﻿namespace Microsoft.ApplicationInsights.AspNetCore.Logging
{
    using System;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// <see cref="ILoggerProvider"/> implementation that creates returns instances of <see cref="ApplicationInsightsLogger"/>
    /// </summary>
    internal class ApplicationInsightsLoggerProvider : ILoggerProvider
    {
        private readonly TelemetryClient telemetryClient;
        private readonly Func<string, LogLevel, bool> filter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationInsightsLoggerProvider"/> class.
        /// </summary>
        public ApplicationInsightsLoggerProvider(TelemetryClient telemetryClient, Func<string, LogLevel, bool> filter)
        {
            this.telemetryClient = telemetryClient;
            this.filter = filter;
        }

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName)
        {
            return new ApplicationInsightsLogger(categoryName, this.telemetryClient, filter);
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}