using DistributedTask;
using MatchHistoryStorage;
using MatchResults;
using MatchResultsProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreTime;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AsyncCore;
using LogCore;
using GGCharityCore;
using System.Globalization;
using System.Text;

namespace MatchResultScanner
{
    internal static class DateTimeForProfiling
    {
        public static DateTime UtcNow
        {
            get { return DateTime.UtcNow; }
        }
    }

    public class RefTimeSpan
    {
        public TimeSpan Value { get; set; }
    }

    internal class DurationMeasurement : IDisposable
    {
        private long _startTime;
        private RefTimeSpan _result;
        public DurationMeasurement(RefTimeSpan result)
        {
            _startTime = DateTimeForProfiling.UtcNow.Ticks;
            _result = result;
        }

        public void Dispose()
        {
            _result.Value = TimeSpan.FromTicks(DateTimeForProfiling.UtcNow.Ticks - _startTime);
        }
    }

    public enum ScannerEndedReason
    {
        /// <summary>
        /// Indicates the scanner left due to a fault.
        /// </summary>
        Faulted = 0,

        /// <summary>
        /// Indicates that the scan is completely finished.  This scanner was the last
        /// one to leave, it completed its work, and there's nothing left to do.
        /// </summary>
        ScanIsTotallyFinished = 1,

        /// <summary>
        /// Indicates that there are still scanners running but that once they complete
        /// there is no further work to do.
        /// </summary>
        ScanHasNoOutstandingWork = 2,
    }

    public class TestScanRanLongException : System.Exception
    {
        public TestScanRanLongException(ScanTraits traits, string workerId)
            : base(String.Format("The test scan for event {0} by worker {1} has run too long", traits.EventId, workerId))
        {

        }
    }

    public class PartitionUpdateResult
    {
        public string PartitionId;
        public TimeSpan? TimeTilNextUpdate;
        public RefTimeSpan TotalDuration = new RefTimeSpan();
        public RefTimeSpan GetPartitionDuration = new RefTimeSpan();
        public RefTimeSpan UpdatePartitionDuration = new RefTimeSpan();
        public int NumPlayersUpdated;
        public int NumNewMatchesFound;
        public int NumNewWinsFound;
        public int NumRetrieveMatchesFailures;
        public int NumMatchResultsProviderFailures;
        public int NumPlayersFailed; 
        public int NumSaveMatchesFailures;
        public int NumUpdateRegistrationFailures;
        public bool AreResultsOfficial;
    }

    internal enum MatchScannerState
    {
        TestScan,
        ManualScan,
        OfficialBaseline,
        OfficialInProgress,
        OfficialFinishing,
    }

    public class MatchScanner : IDistributedTaskWorker
    {
        IMatchHistoryStorage _matchHistory;
        IDistributedTaskParticipationHandle _participationHandle;
        Task _backgroundWorker;
        IMatchResultsProvider _resultsProvider;
        bool _cancelScan = false;
        ScanCoordinator _monitor;
        ManualResetEvent _cancelScanEvent = new ManualResetEvent(false);
        TimeSpan _scannerHeartbeatPeriod = TimeSpan.FromMinutes(10);
        string _workerId;
        IMatchHistoryEvent _mainEvent;
        IMatchHistoryResultCollection _collection;
        bool _suspended;
        bool _scanEnded;
        ScanTraits _scanTraits;
        IScannerConfiguration _scannerConfig;
        LongLivedTraceSource Log;
        MatchScannerState _scannerState;

        public static readonly string OfficialResultsCollectionId = ScanType.Official.ToString();

        public ScanTraits Traits
        {
            get
            {
                return _scanTraits;
            }
        }

        internal MatchScanner(
            ScanCoordinator monitor, 
            IDistributedTaskParticipationHandle participationHandle, 
            IMatchHistoryStorage storage, 
            IMatchResultsProvider resultsProvider,
            ScanTraits traits,
            string workerId,
            IScannerConfiguration scannerConfig,
            bool suspended = false)
        {
            Log = Logging.GetLongLivedLog(workerId, "MatchScanner", "MatchScanner");

            _participationHandle = participationHandle;
            _matchHistory = storage;
            _resultsProvider = resultsProvider;
            _monitor = monitor;

            _scanTraits = traits;
            _workerId = workerId;

            _mainEvent = _matchHistory.GetEventAsync(_scanTraits.EventId).Result;
            _collection = _mainEvent.GetResultCollectionAsync(_scanTraits.ResultCollection).Result;

            _suspended = suspended;
            _scannerConfig = scannerConfig;
        }

        void IDistributedTaskWorker.BeginWork()
        {
            Log.TraceInformation("Scanner is beginning work, " + Traits.ToString());
            if (!_suspended)
            {
                StartScan();
            }
            else
            {
                Log.TraceEvent(TraceEventType.Critical, 0, "Scan didn't begin because the scanner was launched in suspended mode");
            }
        }

        private void StartScan()
        {
            _backgroundWorker = Task.Run(() => RunScannerAsync());
        }

        private async Task UpdateAppState(string state)
        {
            string fullState = String.Format(CultureInfo.InvariantCulture, "[{0}] - {1}", _scannerState.ToString(), state);
            await _participationHandle.SetAppStateAsync(fullState).ConfigureAwait(false);
        }

        public async Task<PartitionUpdateResult> RefreshNextPartitionAsync()
        {
            Log.TraceInformation("Refreshing the next partition...");

            await UpdateAppState("Searching for stale partitions").ConfigureAwait(false);

            PartitionUpdateResult result = new PartitionUpdateResult();
            using (new DurationMeasurement(result.TotalDuration))
            {
                TimeSpan getEntriesStalerThan = GetRefreshTime();
                Log.TraceInformation("The refresh time for this iteration is {0}", getEntriesStalerThan);

                // Get the most stale collection that hasn't been refreshed in the specified
                // time period.
                IMatchHistoryGetNextPlayerCollectionResult nextCollectionResult;
                using (new DurationMeasurement(result.GetPartitionDuration))
                {
                    Log.TraceInformation("Getting the next collection");
                    nextCollectionResult = await _collection.GetNextPlayerCollectionForUpdateAsync(getEntriesStalerThan, TimeSpan.FromMinutes(2)).ConfigureAwait(false);
                }

                // The partition is empty, which means there are currently no accounts that 
                // are staler than the specified time period.  For test passes, or for official
                // runs after the event has ended, this means we're done.  For official runs, 
                // while the event is in progress, this means we sleep.
                if (nextCollectionResult.Players == null)
                {
                    Log.TraceInformation("Did not find a stale collection");
                    if ((Traits.Type == ScanType.TestPass)
                        || (Time.UtcNow > Traits.EventEnd))
                    {
                        Log.TraceInformation("Found that the scan is over");
                        // Only hit each player once.  If there are no more
                        // stale entries then we're done.
                        result.TimeTilNextUpdate = null;
                    }
                    else
                    {
                        result.TimeTilNextUpdate = nextCollectionResult.timeTilNextUpdate.Value;
                        Log.TraceInformation("No work to do at the moment, but the scan is ongoing.  Wait time of {0}", result.TimeTilNextUpdate);
                    }
                }
                else
                {
                    Log.TraceInformation("Found stale player collection {0}, refreshing...", nextCollectionResult.CollectionId);
                    result.TimeTilNextUpdate = TimeSpan.Zero;
                    result.PartitionId = nextCollectionResult.CollectionId;
                    result.AreResultsOfficial = ShouldCountOfficialResults();
                    result.NumPlayersUpdated = nextCollectionResult.Players.Count;

                    // There are players that we need to scan.  The lock is already held.
                    using (new DurationMeasurement(result.UpdatePartitionDuration))
                    {
                        using (nextCollectionResult.Lock)
                        {
                            Log.TraceInformation("Refreshing partition {0}", nextCollectionResult.CollectionId);
                            try
                            {
                                await UpdatePartitionAsync(nextCollectionResult, result).ConfigureAwait(false);
                            }
                            catch (LockHasExpiredException)
                            {
                                // Lock expired, we need to move on to the next partition.
                                Log.TraceEvent(TraceEventType.Critical, 0, "The lock expired while updating collection {0}", nextCollectionResult.CollectionId);
                            }
                        }
                    }
                }
            }

            await RecordLatestRefreshResult(result);

            return result;
        }

        private async Task RunScannerAsync()
        {
            Log.TraceInformation("Starting up the scanner loop");
            bool scanIsTotallyCompleted = false;

            if (_scanTraits.Type == ScanType.Manual)
            {
                _scannerState = MatchScannerState.ManualScan;
            }
            else if (_scanTraits.Type == ScanType.TestPass)
            {
                _scannerState = MatchScannerState.TestScan;
            }

            try
            {
                TimeSpan? tilNextUpdate = TimeSpan.Zero;
                while (tilNextUpdate.HasValue)
                {
                    AbortIfTestScanRunsLong();

                    UpdateScannerState();

                    // Sleep until there's more work to do or until the scan is canceled.
                    Log.TraceInformation("Sleeping for {0}", tilNextUpdate);
                    await UpdateAppState("Sleeping for " + tilNextUpdate.ToString()).ConfigureAwait(false);
                    if (_cancelScanEvent.WaitOne(tilNextUpdate.Value))
                    {
                        Log.TraceInformation("Breaking out of scan loop because the event was signaled");
                        break;
                    }

                    await _participationHandle.HeartbeatAsync().ConfigureAwait(false);

                    // Update the next partition.  The return value tells us whether or not there's 
                    // any more work to do.
                    var refreshResult = await RefreshNextPartitionAsync().ConfigureAwait(false);
                    tilNextUpdate = refreshResult.TimeTilNextUpdate;
                    if (!tilNextUpdate.HasValue)
                    {
                        Log.TraceInformation("Breaking out of scan loop because a next update time was not returned.");
                        break;
                    }

                    if (tilNextUpdate > _scannerHeartbeatPeriod)
                    {
                        tilNextUpdate = _scannerHeartbeatPeriod;
                    }

                    TimeSpan timeTilEnd = _scanTraits.EventEnd - Time.UtcNow;
                    if ((tilNextUpdate > timeTilEnd)
                        && (timeTilEnd > TimeSpan.Zero))
                    {
                        tilNextUpdate = _scanTraits.EventEnd - Time.UtcNow;
                    }
                    else if (timeTilEnd <= TimeSpan.Zero)
                    {
                        tilNextUpdate = TimeSpan.Zero;
                    }
                }

                // We broke out of the loop.  Determine the reason and report the result.
                scanIsTotallyCompleted = await WrapUpScanAsync().ConfigureAwait(false);
            }
            catch(Exception Ex)
            {
                // Make a best effort to report the exception data.
                Log.TraceEvent(TraceEventType.Critical, 0, "Scanner for event {0} encountered an exception and will terminate: {1}", this.Traits.EventId, Ex.ToString());

                string data = null;
                if (Ex is System.AggregateException)
                {
                    if (Ex.InnerException is System.Data.Entity.Validation.DbEntityValidationException)
                    {
                        StringBuilder sb = new StringBuilder();
                        var dbException = Ex.InnerException as System.Data.Entity.Validation.DbEntityValidationException;
                        foreach (var entry in dbException.EntityValidationErrors)
                        {
                            sb.AppendFormat("Entity Validation error: {0}", entry.Entry.ToString());
                            foreach (var subentry in entry.ValidationErrors)
                            {
                                sb.AppendFormat("Db validation error for property {0}: {1}", subentry.PropertyName, subentry.ErrorMessage);
                            }
                        }
                        data = sb.ToString();
                        Log.TraceEvent(TraceEventType.Critical, 0, "Validation errors: {0}", sb.ToString());
                    }
                }

                _participationHandle.AddInfoAsync(InfoLevel.Error, Ex.ToString(), data).Wait();
                _participationHandle.LeaveAsync(ParticipantState.Faulted).Wait();
                throw;
            }
            finally
            {
                // At this point, we've exited the scan loop.  It was either because the scan is done
                // or because we were canceled.  We may have already called out to our subscribers, but
                // if we didn't, make sure it happens now.
                if (!_scanEnded)
                {
                    Log.TraceInformation("Notifying subscribers that scan has ended, totallyComplete={0}, _canceled={1}", scanIsTotallyCompleted, _cancelScan);
                    _monitor.EndScan(this, scanIsTotallyCompleted, !_cancelScan);
                }

                if (OnWorkerFinished != null)
                {
                    // This is intentionally run asynchronously and not awaited.
                    Task.Run(() =>
                    {
                        OnWorkerFinished(scanIsTotallyCompleted);
                    });
                }
            }
        }

        private void UpdateScannerState()
        {
            DateTime now = Time.UtcNow;
            if (_scanTraits.Type == ScanType.Official)
            {
                if (now < _scanTraits.EventStart)
                {
                    _scannerState = MatchScannerState.OfficialBaseline;
                }
                else if (now < _scanTraits.EventEnd)
                {
                    _scannerState = MatchScannerState.OfficialInProgress;
                }
                else
                {
                    _scannerState = MatchScannerState.OfficialFinishing;
                }
            }
        }

        private async Task RecordLatestRefreshResult(PartitionUpdateResult refreshResult)
        {
            var record = _participationHandle.CreateDetailedInfoRecord(InfoLevel.Progress);

            record.AddEntry("PartitionId", refreshResult.PartitionId);
            record.AddEntry("TimeTilNextUpdate", refreshResult.TimeTilNextUpdate.HasValue ? (long)refreshResult.TimeTilNextUpdate.Value.TotalSeconds : -1);
            record.AddEntry("TotalDuration", (long)refreshResult.TotalDuration.Value.TotalMilliseconds);
            record.AddEntry("GetPartitionDuration", (long)refreshResult.GetPartitionDuration.Value.TotalMilliseconds);
            record.AddEntry("UpdatePartitionDuration", (long)refreshResult.UpdatePartitionDuration.Value.TotalMilliseconds);
            record.AddEntry("NumPlayersUpdated", refreshResult.NumPlayersUpdated);
            record.AddEntry("NumNewMatchesFound", refreshResult.NumNewMatchesFound);
            record.AddEntry("NumNewWinsFound", refreshResult.NumNewWinsFound);
            record.AddEntry("NumRetrieveMatchesFailures", refreshResult.NumRetrieveMatchesFailures);
            record.AddEntry("NumMatchResultsProviderFailures", refreshResult.NumMatchResultsProviderFailures);
            record.AddEntry("NumPlayersFailed", refreshResult.NumPlayersFailed);
            record.AddEntry("NumSaveMatchesFailures", refreshResult.NumSaveMatchesFailures);
            record.AddEntry("NumUpdateRegistrationFailures", refreshResult.NumUpdateRegistrationFailures);
            record.AddEntry("AreResultsOfficial", refreshResult.AreResultsOfficial ? 1 : 0);

            await _participationHandle.IncrementWorkItemsCompletedAsync(refreshResult.NumPlayersUpdated).ConfigureAwait(false);

            await record.SaveAsync().ConfigureAwait(false);
        }

        public async Task<bool> WrapUpScanAsync()
        {
            Log.TraceInformation("Wrapping up scan");

            bool scanIsTotallyCompleted = false;
            if (_cancelScan)
            {
                Log.TraceInformation("Leaving the task due to cancelation");
                await _participationHandle.LeaveAsync(ParticipantState.Departed).ConfigureAwait(false);
            }
            else
            {
                Log.TraceInformation("Leaving the task due to scan completion.");
                scanIsTotallyCompleted = await _participationHandle.LeaveAsync(ParticipantState.Complete).ConfigureAwait(false);
            }

            Log.TraceInformation("Notifying subscribers that scan is over, totallyComplete={0}, cancel={1}", scanIsTotallyCompleted, _cancelScan);
            _monitor.EndScan(this, scanIsTotallyCompleted, !_cancelScan);
            _scanEnded = true;
            return scanIsTotallyCompleted;
        }

        // This is used in test cases which move the clock around in order to avoid
        // accidently triggering the Missing worker detection in the distributed task
        // code.
        public async Task Heartbeat()
        {
            await _participationHandle.HeartbeatAsync().ConfigureAwait(false);
        }

        private async Task<int> UpdatePlayerAsync(IMatchHistoryWritablePlayerResults player, PartitionUpdateResult stats)
        {
            Log.TraceInformation("Updating player {0}", player.Registration.PlayerId);

            IMatchResultsProviderResult latestMatches = null;
            List<IMatchResult> previousMatches = null;
            const int maxRetries = 3;
            int numAttempts = 0;
            int previousErrorCount = player.ErrorCount;

            while ((numAttempts < maxRetries)
                    && (latestMatches == null || previousMatches == null))
            {
                if (numAttempts > 0)
                {
                    Thread.Sleep(500);
                }

                numAttempts++;

                ConfiguredTaskAwaitable<IMatchResultsProviderResult> latestMatchesTask = Task.FromResult<IMatchResultsProviderResult>(null).ConfigureAwait(false);
                ConfiguredTaskAwaitable<List<IMatchResult>> previousMatchesTask = Task.FromResult<List<IMatchResult>>(null).ConfigureAwait(false);

                Log.TraceInformation("Starting the task to retrieve player {0}'s stored matches", player.Registration.PlayerId);

                // Start the task to grab the matches for this player from our storage.
                if (previousMatches == null)
                {
                    previousMatchesTask = player.GetMatchesAsync(Traits.NumMatchesPerQuery).ConfigureAwait(false);
                }

                Log.TraceInformation("Starting the task to retrieve player {0}'s latest results", player.Registration.PlayerId);

                // Start the task to grab the most recent matches for this player.
                if (latestMatches == null)
                {
                    latestMatchesTask = _resultsProvider.GetMatchesForPlayerAsync(
                        player.Registration.ResultsToken, 
                        player.ContinuationToken).ConfigureAwait(false);
                }

                Log.TraceInformation("Finishing the task to retrieve player {0}'s stored matches", player.Registration.PlayerId);

                try
                {
                    previousMatches = await previousMatchesTask;
                }
                catch (Exception exception)
                {
                    Log.TraceEvent(TraceEventType.Error, 0, "Failed to retrieve the stored matches for player {0} with the following exception: {1}", player.Registration.PlayerId, exception);
                    
                    _participationHandle.AddInfoAsync(InfoLevel.Error,
                        String.Format("Worker {0} failed to get old matches from storage for player {1}", _workerId, player.Registration.PlayerId), 
                        ExtractExceptionMessage(exception))
                        .Wait();

                    if (IsCriticalException(exception))
                    {
                        throw;
                    }
                }

                Log.TraceInformation("Finishing the task to retrieve player {0}'s latest results", player.Registration.PlayerId);

                try
                {
                    latestMatches = await latestMatchesTask;
                }
                catch (Exception exception)
                {
                    Log.TraceEvent(TraceEventType.Error, 0, "Failed to retrieve the latest matches for player {0} with the following exception: {1}", player.Registration.PlayerId, exception);
                    _participationHandle.AddInfoAsync(InfoLevel.Error,
                        String.Format("Worker {0} failed to get new matches for player {1}", _workerId, player.Registration.PlayerId), 
                        ExtractExceptionMessage(exception))
                        .Wait();

                    if (IsCriticalException(exception))
                    {
                        throw;
                    }
                }
            }

            // Count the error and then abandon updating this player.
            if (latestMatches == null)
            {
                Interlocked.Increment(ref stats.NumMatchResultsProviderFailures);
            }
            if (previousMatches == null)
            {
                Interlocked.Increment(ref stats.NumRetrieveMatchesFailures);
            }
            if (latestMatches == null || previousMatches == null)
            {
                Log.TraceEvent(TraceEventType.Error, 0, "Abandoning the update for player {0}", player.Registration.PlayerId);

                await player.UpdateErrorCountAsync(previousErrorCount + 1).ConfigureAwait(false);

                return 0;
            }

            // I decided to force the provider to specify the order so that it was always an explicit decision.
            // Without doing this, I often forgot which order was used.  But only one is really supported.  Better
            // to explicitly throw if the wrong one is used rather than silently fail.
            if (latestMatches.Order == MatchOrdering.LeastRecentlyPlayedFirst)
            {
                throw new NotSupportedException("This ordering is not supported.  You must return matches in most-recently-played order.");
            }

            Log.TraceInformation("Merging player {0}'s matches", player.Registration.PlayerId);
            // Merge the two lists, and extract the matches that are new.
            var newMatches = MergeMatches(previousMatches, latestMatches.Matches);

            Log.TraceInformation("Found {0} new matches for player {1}", newMatches.Count, player.Registration.PlayerId);

            Interlocked.Add(ref stats.NumNewMatchesFound, newMatches.Count);

            // We may be in the baseline phase, in which case we want to add matches
            // to the history, but we don't want to count them towards their goal
            // yet.
            bool shouldCountOfficialResults = ShouldCountOfficialResults();

            if (shouldCountOfficialResults)
            {
                Log.TraceInformation("Adding wins to the official result count for player {0}", player.Registration.PlayerId);
                Interlocked.Add(ref stats.NumNewWinsFound, newMatches.Where(m => m.IsWin).Count());
            }

            // We also should only count matches that occurred in the event timeframe. 
            // When we're finishing the event, we'll do a scan after it completes to make sure
            // we don't miss any results, but we might pick up matches that were played after
            // the deadline.  Exclude those.  Similarly, during baselining we exclude matches
            // that occurred before the event started.
            int numWinsToCountTowardsEvent = (!shouldCountOfficialResults) ? 0 :
                  newMatches.Where(m => (m.IsWin)
                                   && (m.DatePlayed > _scanTraits.EventStart) 
                                   && (m.DatePlayed < _scanTraits.EventEnd)).Count();

            // Flush the matches we've found for the current player.
            Log.TraceInformation("Flushing matches for player {0}", player.Registration.PlayerId);
            try
            {
                await player.AddMatchesAndFlushAsync(newMatches, latestMatches.ContinuationToken, numWinsToCountTowardsEvent).ConfigureAwait(false);
                Interlocked.Decrement(ref stats.NumPlayersFailed);
            }
            catch(Exception exception)
            {
                Log.TraceEvent(TraceEventType.Error, 0, "Failed to flush the new matches for player {0} with exception {1}", 
                    player.Registration.PlayerId, 
                    ExtractExceptionMessage(exception));
                
                Interlocked.Increment(ref stats.NumSaveMatchesFailures);
                player.UpdateErrorCountAsync(previousErrorCount + 1).Wait();
                previousErrorCount++;

                if (IsCriticalException(exception))
                {
                    throw;
                }
            }

            Log.TraceInformation("Updating the registration for player {0}", player.Registration.PlayerId);
            try
            {
                await GGCharityDatabase.GetRetryPolicy().ExecuteAsync(async () =>
                {
                    using (GGCharityDatabase db = new GGCharityDatabase(null, null))
                    {
                        await db.PerformAsync(async () =>
                        {
                            var eventRegistration = await db.EventRegistrations.FindAsync(player.Registration.PlayerId, Int32.Parse(_scanTraits.EventId));
                            eventRegistration.WinsAchieved += numWinsToCountTowardsEvent;
                            await db.SaveChangesAsync();
                        }).ConfigureAwait(false);
                    }
                });
            }
            catch (Exception exception)
            {
                Log.TraceEvent(TraceEventType.Error, 0, "Failed to update the registration for player {0} with exception {1}", 
                    player.Registration.PlayerId, 
                    ExtractExceptionMessage(exception));
                
                Interlocked.Increment(ref stats.NumUpdateRegistrationFailures);
                player.UpdateErrorCountAsync(previousErrorCount + 1).Wait();
                previousErrorCount++;
                
                if (IsCriticalException(exception))
                {
                    throw;
                }
            }

            return numWinsToCountTowardsEvent;
        }

        private string ExtractExceptionMessage(Exception exception)
        {
            if (exception is AggregateException)
            {
                return exception.InnerException.ToString();
            }
            else
            {
                return exception.ToString();
            }
        }

        private async Task UpdatePartitionAsync(IMatchHistoryGetNextPlayerCollectionResult partition, PartitionUpdateResult stats)
        {
            // We'll decrement this each time we update one.
            stats.NumPlayersFailed = partition.Players.Count;

            // This scanner may support performance tuning options that allow the administrator
            // to configure the number of simultaneous threads used in scanning.  This is so that
            // we can safely turn on scanning in the web worker roles without worrying about
            // too much of a perf impact on the site itself.  We refresh this value each time
            // so that we adjust when the configuration is changed.
            int? degreeOfParallelism = _scannerConfig.RefreshMaxDegreeOfParallelism();

            await UpdateAppState("Updating Players in set " + partition.CollectionId).ConfigureAwait(false);

            Log.TraceInformation("Updating partition running up to {0} simultaneous workers", degreeOfParallelism.HasValue ? degreeOfParallelism.ToString() : "unlimited");

            int numWinsForPartition = 0;

            // Update the players in this partition.
            await Async.ForEachAsync(partition.Players, degreeOfParallelism, async (player) =>
            {
                int numWinsForPlayer = await UpdatePlayerAsync(player, stats).ConfigureAwait(false);
                Interlocked.Add(ref numWinsForPartition, numWinsForPlayer);
            }).ConfigureAwait(false);

            // This will naturally be updated in table storage when the lock is released.  
            partition.PlayerSet.TotalWinsForEvent += numWinsForPartition;
        }

        private TimeSpan GetRefreshTime()
        {
            // Official runs use the game's specified refresh period in order to make sure we 
            // don't miss any results.  
            // Test runs are special in that they only hit each account once.  So, we only look
            // for accounts that haven't been updated since the scan started.  This is also true
            // if the event has ended, since that means we're making the final results pass.
            TimeSpan getEntriesStalerThan;
            DateTime now = Time.UtcNow;
            if (Traits.Type == ScanType.Official)
            {
                if (Time.UtcNow > Traits.EventEnd)
                {
                    getEntriesStalerThan = now - Traits.EventEnd;
                }
                else
                {
                    getEntriesStalerThan = Traits.RefreshTime;
                }
            }
            else
            {
                getEntriesStalerThan = now - Traits.ScanStartTime.Value;
            }
            return getEntriesStalerThan;
        }

        private void AbortIfTestScanRunsLong()
        {
#if !DEBUG
            // If we're in the midst of a test pass and the event has started, something is terribly wrong.
            // Abort the scan.
            if ((Traits.Type == ScanType.TestPass)
                && ((Time.UtcNow + Traits.RefreshTime) > Traits.EventStart))
            {
                Log.TraceEvent(TraceEventType.Critical, 0, "Scanner is running a test pass close to the event start time");
                throw new TestScanRanLongException(Traits, _workerId);
            }
#endif
        }

        private bool IsCriticalException(Exception ex)
        {
            if (ex is AggregateException)
            {
                return (ex as AggregateException).InnerExceptions.Any(e => IsCriticalException(e));
            }
            else
            {
                Type[] nonCriticalExceptions = 
                {
                    typeof(System.Net.WebException),
#if !DEBUG
                    typeof(Microsoft.WindowsAzure.Storage.StorageException),
#endif
                };

                return !nonCriticalExceptions.Contains(ex.GetType());
            }
        }

        private bool ShouldCountOfficialResults()
        {
            bool isOfficialScanType =
                (_scanTraits.Type == ScanType.Official);

            bool hasEventStarted = (Time.UtcNow > _scanTraits.EventStart);

            return isOfficialScanType && hasEventStarted;
        }

        public List<IMatchResult> MergeMatches(List<IMatchResult> previousMatches, List<IMatchResult> latestMatches)
        {
            if (previousMatches.Count > latestMatches.Count)
            {
                throw new ArgumentException();
            }

            int previousIndex = 0;
            int latestIndex = 0;
            int latestOffset = latestMatches.Count - previousMatches.Count;

            while (latestIndex + latestOffset < latestMatches.Count)
            {
                if (!previousMatches[previousIndex].Equals(latestMatches[latestIndex + latestOffset]))
                {
                    latestIndex = 0;
                    previousIndex = 0;
                    ++latestOffset;
                }
                else
                {
                    ++latestIndex;
                    ++previousIndex;
                }
            }

            return latestMatches.Take(latestOffset).ToList();
        }

        void IDistributedTaskWorker.EndWork()
        {
            Log.TraceInformation("Ending work");
            if (!_scanEnded)
            {
                Log.TraceInformation("Ending work");
                _cancelScanEvent.Set();
                _cancelScan = true;
            }
            if (_backgroundWorker != null)
            {
                _backgroundWorker.Wait();
            }
        }

        public event OnWorkerFinishedHandler OnWorkerFinished;
    }
}
