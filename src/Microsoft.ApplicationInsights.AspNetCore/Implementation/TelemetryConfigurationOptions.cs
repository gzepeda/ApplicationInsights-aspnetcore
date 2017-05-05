﻿namespace Microsoft.Extensions.DependencyInjection
{
    using System.Collections.Generic;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// The <see cref="IOptions{TelemetryConfiguration}"/> implementation that uses <see cref="TelemetryConfiguration.Active"/> as initial value.
    /// </summary>
    internal class TelemetryConfigurationOptions : IOptions<TelemetryConfiguration>
    {
        public TelemetryConfigurationOptions(IEnumerable<IConfigureOptions<TelemetryConfiguration>> configureOptions)
        {
            foreach (var c in configureOptions)
            {
                c.Configure(this.Value);
            }
        }

        /// <inheritdoc />
        public TelemetryConfiguration Value => TelemetryConfiguration.Active;
    }
}