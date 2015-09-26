using LogCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BattleNetApi
{
    internal static class ApiDispatcher
    {
        /// <summary>
        /// The current backoff period which the dispatcher will wait between requests.
        /// </summary>
        private static TimeSpan? BackoffPeriod = null;

        /// <summary>
        /// The maximum backoff period between requests.
        /// </summary>
        private static readonly TimeSpan MaxBackoffPeriod = TimeSpan.FromSeconds(3);

        /// <summary>
        /// The minimum backoff period between requests.
        /// </summary>
        private static readonly TimeSpan MinBackoffPeriod = TimeSpan.FromSeconds(0.1);

        /// <summary>
        /// The last time a request was throttled by the server.
        /// </summary>
        private static DateTime? LastThrottledRequest = null;

        /// <summary>
        /// The last time we sent a request to the server.
        /// </summary>
        private static DateTime LastRequest = DateTime.MinValue;

        /// <summary>
        /// The length of time after a 403 response is received that we'll apply the backoff
        /// timeout.  After this time expires, the backoff drops by half, eventually disappearing
        /// if we continue to issue successful requests.
        /// </summary>
        private static readonly TimeSpan BackoffRecoveryPeriod = TimeSpan.FromMinutes(10);

        /// <summary>
        /// The maximum number of retries for a single request.
        /// </summary>
        private const int MaxRetriesPerRequest = 5;

        /// <summary>
        /// The backoff period applied to all individual requests, regardless of the global
        /// backoff.
        /// </summary>
        private static readonly TimeSpan RetryBackoffPeriod = TimeSpan.FromSeconds(0.5);

        private static LongLivedTraceSource Log;

        private static object BackoffLock = new object();

        private enum RequestFailureType
        {
            /// <summary>
            /// The request should not be retried.  Ex: 404.
            /// </summary>
            Nonretriable,

            /// <summary>
            /// The request should be retrived. Ex: network error.
            /// </summary>
            Retriable,

            /// <summary>
            /// The request should be retried.  Ex: 403 Account Over Rate Limit
            /// </summary>
            RetriableWithThrottling,

            /// <summary>
            /// The request should not be retried and the error that we got back is unexpected.  Ex: 401 Not Authorized.
            /// </summary>
            NonretriableUnexpected,
        }

        static ApiDispatcher()
        {
            Log = Logging.GetLongLivedLog("BattleNetApiDispatcher", "BattleNetApiDispatcher");
        }

        internal static async Task<string> MakeRequestAsync(Uri uri)
        {
            string apiResult = null;

            using (WebClient web = new WebClient())
            {
                apiResult = await ApiRequestWorkerAsync(uri, web);
            }

            return apiResult;
        }

        private static async Task<string> ApiRequestWorkerAsync(Uri uri, WebClient web)
        {
            int attempts = 0;
            while (attempts < MaxRetriesPerRequest)
            {
                TimeSpan? localBackoff;
                lock(BackoffLock)
                {
                    localBackoff = BackoffPeriod;
                }

                await BackoffIfNecessary(localBackoff);

                try
                {
                    ++attempts;
                    LastRequest = DateTime.UtcNow;
                    string apiResult = await web.DownloadStringTaskAsync(uri);

                    HandleSuccessfulRequest();
                    return apiResult;
                }
                catch (WebException webException)
                {
                    if (!HandleFailedRequest(webException, uri)
                        || (attempts == MaxRetriesPerRequest))
                    {
                        throw;
                    }
                }

                // Regardless of global rate limiting, back off this request
                // for a short period of time.
                await Task.Delay(RetryBackoffPeriod);
            }

            // Shouldn't be possible to hit this path as we would have thrown above after
            // we ran out of retries.
            Debug.Assert(false, "Hit unexpected path in ApiRequestWorkerAsync");
            throw new InvalidOperationException();
        }

        private static void HandleSuccessfulRequest()
        {
            lock (BackoffLock)
            {
                if (BackoffPeriod.HasValue &&
                    ((LastThrottledRequest + BackoffRecoveryPeriod) < DateTime.UtcNow))
                {
                    ReduceBackoffPeriod();
                }
            }
        }

        private static void ReduceBackoffPeriod()
        {
            BackoffPeriod = TimeSpan.FromMilliseconds(BackoffPeriod.Value.TotalMilliseconds / 2);
            if (BackoffPeriod < MinBackoffPeriod)
            {
                BackoffPeriod = null;
            }
            else
            {
                // Move the LastThrottledRequest timer forward to the current time,
                // which basically resets our recovery period timer.
                LastThrottledRequest = DateTime.UtcNow;
            }
        }

        private static async Task BackoffIfNecessary(TimeSpan? localBackoff)
        {
            if (localBackoff.HasValue)
            {
                DateTime nextRequestTime = (LastRequest + localBackoff.Value);
                DateTime utcNow = DateTime.UtcNow;
                if (nextRequestTime > DateTime.UtcNow)
                {
                    await Task.Delay(nextRequestTime - utcNow);
                }
            }
        }

        private static bool HandleFailedRequest(WebException webException, Uri uri)
        {
            string message;
            RequestFailureType failureType = GetFailureType(webException, out message);
            bool retriable = false;

            if (failureType == RequestFailureType.RetriableWithThrottling)
            {
                Log.TraceEvent(TraceEventType.Warning, 0, "API dispatcher is being throttled after encountering an error while requesting URI '{0}': {1}", uri, message);
                IncreaseBackoffPeriod();
                retriable = true;
            }
            else if (failureType == RequestFailureType.Nonretriable)
            {
                Log.TraceEvent(TraceEventType.Error, 0, "API encountered an unretriable error while requesting URI '{0}': {1}", uri, message);
            }
            else if (failureType == RequestFailureType.NonretriableUnexpected)
            {
                Log.TraceEvent(TraceEventType.Critical, 0, "API encountered an unretriable & unexpected error while requesting URI '{0}': {1}", uri, message);
            }
            else if (failureType == RequestFailureType.Retriable)
            {
                Log.TraceEvent(TraceEventType.Verbose, 0, "API dispatcher encountered a retriable error while requesting URI '{0}': {1}", uri, message);
                retriable = true;
            }
            else
            {
                Debug.Assert(false);
            }

            return retriable;
        }

        private static void IncreaseBackoffPeriod()
        {
            lock (BackoffLock)
            {
                LastThrottledRequest = DateTime.UtcNow;

                // Update the backoff.  if there isn't one yet, set it to MinBackoffPeriod.  
                // Otherwise double it. 
                // Cap the value at MaxBackoffPeriod.
                if (!BackoffPeriod.HasValue)
                {
                    BackoffPeriod = MinBackoffPeriod;
                }
                else
                {
                    BackoffPeriod = TimeSpan.FromMilliseconds(BackoffPeriod.Value.TotalMilliseconds * 2);
                    if (BackoffPeriod > MaxBackoffPeriod)
                    {
                        BackoffPeriod = MaxBackoffPeriod;
                    }
                }
            }
        }

        private static RequestFailureType GetFailureType(WebException webException, out string message)
        {
            RequestFailureType failureType;

            // ProtocolError is the result that we get back if there was an HTTP error code.
            // Other error types will be returned if, for example, the transport fails.
            if (webException.Status == WebExceptionStatus.ProtocolError)
            {
                var httpResponse = webException.Response as HttpWebResponse;
                if (httpResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    failureType = HandleForbiddenResponse(httpResponse);
                }
                else if (httpResponse.StatusCode == HttpStatusCode.InternalServerError)
                {
                    failureType = RequestFailureType.Retriable;
                }
                else
                {
                    failureType = RequestFailureType.NonretriableUnexpected;
                }

                message = String.Format(CultureInfo.InvariantCulture, @"Received HTTP Error {0} ({1}) from an API request.", httpResponse.StatusCode, httpResponse.StatusDescription);
            }
            else
            {
                failureType = RequestFailureType.Retriable;
                message = @"Received a web exception: " + webException.Message;
            }

            return failureType;
        }

        private static RequestFailureType HandleForbiddenResponse(HttpWebResponse httpResponse)
        {
            RequestFailureType failureType = RequestFailureType.NonretriableUnexpected;

            if (httpResponse.StatusDescription == "Account Over Queries Per Second Limit" ||
                httpResponse.StatusDescription == "Account Over Rate Limit" ||
                httpResponse.StatusDescription == "Rate Limit Exceeded")
            {
                failureType = RequestFailureType.RetriableWithThrottling;
            }
            else if (httpResponse.StatusDescription == "Forbidden" ||
                     httpResponse.StatusDescription == "Not Authorized" ||
                     httpResponse.StatusDescription == "Account Inactive")
            {
                failureType = RequestFailureType.NonretriableUnexpected;
            }
            else
            {
                // A 403 error was returned with a description we don't recognize.  We'll retry this anyway,
                // and behave just like we would for other throttling responses.
                failureType = RequestFailureType.RetriableWithThrottling;
            }
            return failureType;
        }
    }
}
