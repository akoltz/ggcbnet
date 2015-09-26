using AzureCore.DistributedTask;
using CoreTime;
using DistributedTask;
using GGCharityData;
using LogCore;
using MatchHistoryStorage;
using MatchResultsProvider;
using System;
using System.Threading.Tasks;

namespace MatchResultScanner
{
    public class ScanTraits
    {
        public override string ToString()
        {
            return String.Format("Scan Traits: Type={6}, EventId={0}, Game={1}, Results={2}, EventStart={3}, ScanStart={4}, End={5}, RefreshTime={7}",
                EventId,
                GameId,
                ResultCollection,
                EventStart,
                ScanStartTime,
                EventEnd,
                Type,
                RefreshTime
                );
        }

        public DateTime EventEnd;
        public DateTime EventStart;
        public DateTime? ScanStartTime;
        public ScanType Type;
        public string EventId;
        public string ResultCollection;
        public GameID GameId;
        public TimeSpan RefreshTime;
        public int NumMatchesPerQuery;
    }

    public enum ScanInitiationResult
    {
        ScanIsOver,
        ScanWasStarted,
        ScanWasAlreadyRunning
    }

    public interface IScannerConfiguration
    {
        int? RefreshMaxDegreeOfParallelism();

        int? RefreshSleepBetweenPlayers();
    }

    public class DefaultScannerConfig : IScannerConfiguration
    {
        public int? RefreshMaxDegreeOfParallelism()
        {
            return null;
        }

        public int? RefreshSleepBetweenPlayers()
        {
            return null;
        }
    }

    public class ScanCoordinator : IScanInitiator, IScanMonitor, IDistributedTaskWorkerFactory
    {
        IDistributedTaskManager _taskManager;
        DistributedTaskMonitor _monitor;
        string _workerId;
        MatchScanner _scanner;
        IMatchHistoryStorageFactory _storageFactory;
        bool _startTasksSuspended;
        IMatchResultsProviderFactory _resultsProviderFactory;
        IScannerConfiguration _scannerConfig;

        public event OnScannerStartHandler OnScannerStart;
        public event OnScannerEndHandler OnScannerEnd;

        private LongLivedTraceSource Log;

        public ScanCoordinator
        (
            IDistributedTaskManager taskManager, 
            IMatchHistoryStorageFactory storageFactory, 
            IMatchResultsProviderFactory resultsProviderFactory,
            TimeSpan pollRate, 
            string workerId,
            bool startTasksSuspended = false,
            IScannerConfiguration scannerConfig = null
        )
        {
            Log = Logging.GetLongLivedLog(workerId, "ScanCoordinator", "ScanCoordinator");

            _storageFactory = storageFactory;
            _taskManager = taskManager;
            _workerId = workerId;

            _monitor = new DistributedTaskMonitor(
                pollRate,
                DistributedTaskMonitor.AutoJoinMode.JoinIfHigherPriority,
                GetScanTaskType(),
                _taskManager,
                this,
                workerId
                );

            _startTasksSuspended = startTasksSuspended;
            _resultsProviderFactory = resultsProviderFactory;

            if (scannerConfig == null)
            {
                scannerConfig = new DefaultScannerConfig();
            }
            _scannerConfig = scannerConfig;
        }

        public Task RefreshAsync()
        {
            return _monitor.RefreshAsync();
        }

        /// <summary>
        /// Initiates a scan.
        /// </summary>
        /// <param name="type">
        /// The type of scan to start.
        /// </param>
        /// <param name="scanEvent">
        /// The event for which this scan is taking place.
        /// </param>
        /// <returns>
        /// True if a scan was started, false if the requested scan for the given event
        /// has already completed.
        /// </returns>
        public async Task<InitiateScanResult> InitiateScanAsync(ScanType type, Event scanEvent)
        {
            string scanTaskId = GetScanTaskId(type, scanEvent);
            string scanTaskParamString = GetScanTaskParamString(type, scanEvent);
            string scanTaskType = GetScanTaskType();
            int scanTaskPriority = GetScanTaskPriority(type);

            Log.TraceInformation("Initiating Scan, id={0}, type={1}, priority={2}, paramString={3}", scanTaskId, scanTaskType, scanTaskPriority, scanTaskParamString);

            try
            {
                IDistributedTaskParticipationHandle handle = await _taskManager.JoinOrBeginAsync(scanTaskId, scanTaskType, scanTaskPriority, scanTaskParamString, _workerId + "-initiator-" + type.ToString()).ConfigureAwait(false);

                // We immediately leave after starting the task.  This component isn't responsible
                // for participating in scans, all we need to do is initiate them.  ScanMonitors will
                // pick this up and begin working.
                await handle.LeaveAsync(ParticipantState.Departed).ConfigureAwait(false);
                return handle.WasTaskStarted ? InitiateScanResult.ScanWasStarted : InitiateScanResult.ScanWasAlreadyRunning;
            }
            catch (TaskIsOverException)
            {
                Log.TraceInformation("Scan has already ended");

                // This worker just popped up during a time when a scan is needed.  However,
                // the scan was already completed by other workers.  It's tempting to assert here
                // that this isn't an official run, but the problem is we could technically start
                // just moments before the run finished and hit this condition anyway.
                return InitiateScanResult.ScanWasAlreadyComplete;
            }
        }

        private int GetScanTaskPriority(ScanType type)
        {
            if (type == ScanType.Official)
            {
                return int.MaxValue;
            }
            else if (type == ScanType.TestPass)
            {
                return 10;
            }
            else if (type == ScanType.Manual)
            {
                return 9;
            }
            else
            {
                throw new ArgumentException();
            }
        }

        internal static string GetScanTaskType()
        {
            return "ggcharityScan";
        }

        private string GetScanTaskId(ScanType type, Event scanEvent)
        {
            return string.Format("{0}{1}", type.ToString(), scanEvent.Id);
        }

        private string GetScanTaskParamString(ScanType type, Event scanEvent)
        {
            string resultCollection = type.ToString();
            if (type == ScanType.Official)
            {
                resultCollection = MatchScanner.OfficialResultsCollectionId;
            }
            else if (type == ScanType.Manual)
            {
                // Manual uses a random result collection.
                resultCollection = Guid.NewGuid().ToString("N");
            }

            string gameIdString = scanEvent.Game.GameId.ToString();

            DateTime? startTime;
            if (type == ScanType.TestPass 
                || type == ScanType.Manual)
            {
                startTime = Time.UtcNow;
            }
            else
            {
                startTime = null;
            }

            TimeSpan refreshTime = scanEvent.Game.MatchHistoryRefreshTime;

            return String.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8}", 
                scanEvent.Id, 
                resultCollection, 
                scanEvent.LiveStart.ToFileTimeUtc().ToString(),
                scanEvent.EventEnd.ToFileTimeUtc().ToString(), 
                gameIdString, 
                type.ToString(), 
                startTime.HasValue ? startTime.Value.ToFileTimeUtc().ToString() : "null",
                refreshTime.Ticks.ToString(),
                scanEvent.Game.NumMatchesPerQuery);
        }
        
        public static ScanTraits ParseTaskParamString(string p)
        {
            string[] split = p.Split(';');
            return new ScanTraits
            {
                EventId = split[0],
                ResultCollection = split[1],
                EventStart = DateTime.FromFileTimeUtc(Int64.Parse(split[2])),
                EventEnd = DateTime.FromFileTimeUtc(Int64.Parse(split[3])),
                GameId = (GameID)Enum.Parse(typeof(GameID), split[4]),
                Type = (ScanType)Enum.Parse(typeof(ScanType), split[5]),
                ScanStartTime = (split[6] == "null") ? (DateTime?)null : DateTime.FromFileTimeUtc(Int64.Parse(split[6])),
                RefreshTime = TimeSpan.FromTicks(Int64.Parse(split[7])),
                NumMatchesPerQuery = Int32.Parse(split[8]),
            };
        }

        public static bool IsScanTask(IDistributedTask task)
        {
            return task.TaskTypeId.Equals(GetScanTaskType());
        }

        public void Start()
        {
            Log.TraceInformation("Starting scan coordinator.");
            _monitor.StartAsync().Wait();
        }

        public void Stop()
        {
            Log.TraceInformation("Stopping scan coordinator.");
            _monitor.Stop();
            EndScan(_scanner, false, false);
        }

        async Task<IDistributedTaskWorker> IDistributedTaskWorkerFactory.CreateInstanceAsync(IDistributedTask task)
        {
            Log.TraceInformation("Detected a new scan, creating a scanner");
            if (_scanner != null)
            {
                throw new InvalidOperationException("This coordinator already has a running scanner");
            }

            var storage = await _storageFactory.CreateInstanceAsync().ConfigureAwait(false);
            var participationHandle = await task.JoinAsync(this._workerId).ConfigureAwait(false);

            Log.TraceInformation("Parsing param string: {0}", task.ParamString);
            ScanTraits traits = ParseTaskParamString(task.ParamString);

            Log.TraceInformation("Creating a new scanner, workerid={0}, eventId={1}, resultCollection={2}", _workerId, traits.EventId, traits.ResultCollection);
            _scanner = new MatchScanner(
                this, 
                participationHandle, 
                storage, 
                _resultsProviderFactory.CreateInstance(traits.GameId), 
                traits, 
                _workerId,
                _scannerConfig, 
                _startTasksSuspended);

            if (OnScannerStart != null)
            {
                OnScannerStart(_scanner);
            }

            return _scanner;
        }

        internal void EndScan(MatchScanner matchScanner, bool scanFinished, bool scanHasNoMoreWork)
        {
            Log.TraceInformation("Ending current scan");

            if (_scanner != matchScanner)
            {
                throw new InvalidOperationException("The scanner that's being ended doesn't match the currently active scanner");
            }

            if (OnScannerEnd != null)
            {
                OnScannerEnd(matchScanner, scanFinished, scanHasNoMoreWork);
            }

            _scanner = null;
        }
    }
}
