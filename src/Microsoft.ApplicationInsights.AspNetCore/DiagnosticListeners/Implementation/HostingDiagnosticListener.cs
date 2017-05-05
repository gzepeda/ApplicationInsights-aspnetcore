﻿namespace Microsoft.ApplicationInsights.AspNetCore.DiagnosticListeners
{
    using System;
    using System.Diagnostics;
    using System.Net.Http.Headers;
    using System.Reflection;
    using Extensibility.Implementation.Tracing;
    using Microsoft.ApplicationInsights.AspNetCore.Extensions;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DiagnosticAdapter;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// <see cref="IApplicationInsightDiagnosticListener"/> implementation that listens for events specific to AspNetCore hosting layer.
    /// </summary>
    internal class HostingDiagnosticListener : IApplicationInsightDiagnosticListener
    {
        /// <summary>
        /// Determine whether the running AspNetCore Hosting version is 2.0 or higher. This will affect what DiagnosticSource events we receive.
        /// To support AspNetCore 1.0 and 2.0, we listen to both old and new events.
        /// If the running AspNetCore version is 2.0, both old and new events will be sent. In this case, we will ignore the old events.
        /// </summary>
        public static bool IsAspNetCore20 = typeof(WebHostBuilder).GetTypeInfo().Assembly.GetName().Version.Major >= 2;

        private readonly TelemetryClient client;
        private readonly ICorrelationIdLookupHelper correlationIdLookupHelper;
        private readonly string sdkVersion;
        private const string ActivityCreatedByHostingDiagnosticListener = "ActivityCreatedByHostingDiagnosticListener";

        /// <summary>
        /// Initializes a new instance of the <see cref="T:HostingDiagnosticListener"/> class.
        /// </summary>
        /// <param name="client"><see cref="TelemetryClient"/> to post traces to.</param>
        /// <param name="correlationIdLookupHelper">A store for correlation ids that we don't have to query it everytime.</param>
        public HostingDiagnosticListener(TelemetryClient client, ICorrelationIdLookupHelper correlationIdLookupHelper)
        {
            this.client = client;
            this.correlationIdLookupHelper = correlationIdLookupHelper;
            this.sdkVersion = SdkVersionUtils.VersionPrefix + SdkVersionUtils.GetAssemblyVersion();
        }

        /// <inheritdoc/>
        public string ListenerName { get; } = "Microsoft.AspNetCore";

        /// <summary>
        /// Diagnostic event handler method for 'Microsoft.AspNetCore.Hosting.HttpRequestIn' event.
        /// </summary>
        [DiagnosticName("Microsoft.AspNetCore.Hosting.HttpRequestIn")]
        public void OnHttpRequestIn()
        {
            // do nothing, just enable the diagnotic source
        }

        /// <summary>
        /// Diagnostic event handler method for 'Microsoft.AspNetCore.Hosting.HttpRequestIn.Start' event.
        /// </summary>
        [DiagnosticName("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start")]
        public void OnHttpRequestInStart(HttpContext httpContext)
        {
            if (this.client.IsEnabled())
            {
                if (Activity.Current == null)
                {
                    AspNetCoreEventSource.Instance.LogHostingDiagnosticListenerOnHttpRequestInStartActivityNull();
                    return;
                }

                var currentActivity = Activity.Current;
                var isActivityCreatedFromRequestIdHeader = false;

                StringValues xmsRequestRootId;
                if (currentActivity.ParentId != null)
                {
                    isActivityCreatedFromRequestIdHeader = true;
                }
                else if (httpContext.Request.Headers.TryGetValue(RequestResponseHeaders.StandardRootIdHeader, out xmsRequestRootId))
                {
                    var activity = new Activity(ActivityCreatedByHostingDiagnosticListener);
                    activity.SetParentId(xmsRequestRootId);
                    activity.Start();
                    httpContext.Features.Set(activity);
                }

                var requestTelemetry = InitializeRequestTelemetryFromActivity(httpContext, Activity.Current, isActivityCreatedFromRequestIdHeader, Stopwatch.GetTimestamp());
                SetAppIdInResponseHeader(httpContext, requestTelemetry);
            }
        }

        /// <summary>
        /// Diagnostic event handler method for 'Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop' event.
        /// </summary>
        [DiagnosticName("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop")]
        public void OnHttpRequestInStop(HttpContext httpContext)
        {
            EndRequest(httpContext, Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Diagnostic event handler method for 'Microsoft.AspNetCore.Hosting.BeginRequest' event.
        /// </summary>
        [DiagnosticName("Microsoft.AspNetCore.Hosting.BeginRequest")]
        public void OnBeginRequest(HttpContext httpContext, long timestamp)
        {
            if (this.client.IsEnabled() && !IsAspNetCore20)
            {
                var activity = new Activity(ActivityCreatedByHostingDiagnosticListener);
                var isActivityCreatedFromRequestIdHeader = false;

                StringValues requestId;
                StringValues standardRootId;
                IHeaderDictionary requestHeaders = httpContext.Request.Headers;
                if (requestHeaders.TryGetValue(RequestResponseHeaders.RequestIdHeader, out requestId))
                {
                    isActivityCreatedFromRequestIdHeader = true;
                    activity.SetParentId(requestId);

                    string[] baggage = requestHeaders.GetCommaSeparatedValues(RequestResponseHeaders.CorrelationContextHeader);
                    if (baggage != StringValues.Empty)
                    {
                        foreach (var item in baggage)
                        {
                            NameValueHeaderValue baggageItem;
                            if (NameValueHeaderValue.TryParse(item, out baggageItem))
                            {
                                activity.AddBaggage(baggageItem.Name, baggageItem.Value);
                            }
                        }
                    }
                }
                else if (requestHeaders.TryGetValue(RequestResponseHeaders.StandardRootIdHeader, out standardRootId))
                {
                    activity.SetParentId(standardRootId);
                }

                activity.Start();
                httpContext.Features.Set(activity);

                var requestTelemetry = InitializeRequestTelemetryFromActivity(httpContext, activity, isActivityCreatedFromRequestIdHeader, timestamp);
                SetAppIdInResponseHeader(httpContext, requestTelemetry);
            }
        }

        /// <summary>
        /// Diagnostic event handler method for 'Microsoft.AspNetCore.Hosting.EndRequest' event.
        /// </summary>
        [DiagnosticName("Microsoft.AspNetCore.Hosting.EndRequest")]
        public void OnEndRequest(HttpContext httpContext, long timestamp)
        {
            if (!IsAspNetCore20)
            {
                EndRequest(httpContext, timestamp);
            }
        }

        /// <summary>
        /// Diagnostic event handler method for 'Microsoft.AspNetCore.Hosting.UnhandledException' event.
        /// </summary>
        [DiagnosticName("Microsoft.AspNetCore.Hosting.UnhandledException")]
        public void OnHostingException(HttpContext httpContext, Exception exception)
        {
            this.OnException(httpContext, exception);

            // In AspNetCore 1.0, when an exception is unhandled it will only send the UnhandledException event, but not the EndRequest event, so we need to call EndRequest here.
            // In AspNetCore 2.0, after sending UnhandledException, it will stop the created activity, which will send HttpRequestIn.Stop event, so we will just end the request there.
            if (!IsAspNetCore20)
            {
                this.EndRequest(httpContext, Stopwatch.GetTimestamp());
            }
        }

        /// <summary>
        /// Diagnostic event handler method for 'Microsoft.AspNetCore.Hosting.HandledException' event.
        /// </summary>
        [DiagnosticName("Microsoft.AspNetCore.Diagnostics.HandledException")]
        public void OnDiagnosticsHandledException(HttpContext httpContext, Exception exception)
        {
            this.OnException(httpContext, exception);
        }

        /// <summary>
        /// Diagnostic event handler method for 'Microsoft.AspNetCore.Diagnostics.UnhandledException' event.
        /// </summary>
        [DiagnosticName("Microsoft.AspNetCore.Diagnostics.UnhandledException")]
        public void OnDiagnosticsUnhandledException(HttpContext httpContext, Exception exception)
        {
            this.OnException(httpContext, exception);
        }

        private RequestTelemetry InitializeRequestTelemetryFromActivity(HttpContext httpContext, Activity activity, bool isActivityCreatedFromRequestIdHeader, long timestamp)
        {
            var requestTelemetry = new RequestTelemetry();

            StringValues standardParentId;
            if (isActivityCreatedFromRequestIdHeader)
            {
                requestTelemetry.Context.Operation.ParentId = activity.ParentId;

                foreach (var prop in activity.Baggage)
                {
                    if (!requestTelemetry.Context.Properties.ContainsKey(prop.Key))
                    {
                        requestTelemetry.Context.Properties[prop.Key] = prop.Value;
                    }
                }
            }
            else if (httpContext.Request.Headers.TryGetValue(RequestResponseHeaders.StandardParentIdHeader, out standardParentId))
            {
                requestTelemetry.Context.Operation.ParentId = standardParentId;
            }

            requestTelemetry.Id = activity.Id;
            requestTelemetry.Context.Operation.Id = activity.RootId;

            this.client.Initialize(requestTelemetry);
            requestTelemetry.Start(timestamp);
            httpContext.Features.Set(requestTelemetry);

            return requestTelemetry;
        }

        private void SetAppIdInResponseHeader(HttpContext httpContext, RequestTelemetry requestTelemetry)
        {
            IHeaderDictionary responseHeaders = httpContext.Response?.Headers;
            if (responseHeaders != null &&
                !string.IsNullOrEmpty(requestTelemetry.Context.InstrumentationKey) &&
                (!responseHeaders.ContainsKey(RequestResponseHeaders.RequestContextHeader) || HttpHeadersUtilities.ContainsRequestContextKeyValue(responseHeaders, RequestResponseHeaders.RequestContextTargetKey)))
            {
                string correlationId = null;
                if (this.correlationIdLookupHelper.TryGetXComponentCorrelationId(requestTelemetry.Context.InstrumentationKey, out correlationId))
                {
                    HttpHeadersUtilities.SetRequestContextKeyValue(responseHeaders, RequestResponseHeaders.RequestContextTargetKey, correlationId);
                }
            }
        }

        private void EndRequest(HttpContext httpContext, long timestamp)
        {
            if (this.client.IsEnabled())
            {
                var telemetry = httpContext?.Features.Get<RequestTelemetry>();

                if (telemetry == null)
                {
                    return;
                }

                telemetry.Stop(timestamp);
                telemetry.ResponseCode = httpContext.Response.StatusCode.ToString();

                var successExitCode = httpContext.Response.StatusCode < 400;
                if (telemetry.Success == null)
                {
                    telemetry.Success = successExitCode;
                }
                else
                {
                    telemetry.Success &= successExitCode;
                }

                if (string.IsNullOrEmpty(telemetry.Name))
                {
                    telemetry.Name = httpContext.Request.Method + " " + httpContext.Request.Path.Value;
                }

                telemetry.HttpMethod = httpContext.Request.Method;
                telemetry.Url = httpContext.Request.GetUri();
                telemetry.Context.GetInternalContext().SdkVersion = this.sdkVersion;
                this.client.TrackRequest(telemetry);

                var activity = httpContext?.Features.Get<Activity>();
                if (activity != null && activity.OperationName == ActivityCreatedByHostingDiagnosticListener)
                {
                    activity.Stop();
                }
            }
        }

        private void OnException(HttpContext httpContext, Exception exception)
        {
            if (this.client.IsEnabled())
            {
                var telemetry = httpContext?.Features.Get<RequestTelemetry>();
                if (telemetry != null)
                {
                    telemetry.Success = false;
                }

                var exceptionTelemetry = new ExceptionTelemetry(exception);
                exceptionTelemetry.HandledAt = ExceptionHandledAt.Platform;
                exceptionTelemetry.Context.GetInternalContext().SdkVersion = this.sdkVersion;
                this.client.Track(exceptionTelemetry);
            }
        }
    }
}