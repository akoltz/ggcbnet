using BattleNetApi;
using LogCore;
using MatchResults;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MatchResultsProvider
{
    public class StarCraftMatchResultsProvider : IMatchResultsProvider
    {
        private readonly string _appId;
        private LongLivedTraceSource Log;

        public StarCraftMatchResultsProvider(string appId)
        {
            _appId = appId;
            Log = Logging.GetLongLivedLog("StarCraftMatchResultsProvider", "MatchScanner");
        }

        public async Task<IMatchResultsProviderResult> GetMatchesForPlayerAsync(string providerPlayerId, string continuationToken)
        {
            Log.TraceInformation("GetMatchesForPlayerAsync called for player {0} with token {1}", providerPlayerId, continuationToken);

            if (!String.IsNullOrWhiteSpace(continuationToken))
            {
                throw new ArgumentException("Continuation token is not supported for this results provider.");
            }

            string[] idSplit = providerPlayerId.Split(';');
            if (idSplit.Count() != 3)
            {
                throw new ArgumentException("Invalid player ID: " + providerPlayerId);
            }

            try
            {
                string playerName = idSplit[0];
                string playerId = idSplit[1];
                string regionIdString = idSplit[2];
                Region region = (Region)int.Parse(regionIdString);

                MatchResultsProviderResult result = new MatchResultsProviderResult();
                result.Order = MatchOrdering.MostRecentlyPlayedFirst;
                result.ContinuationToken = null; // Not used in sc2

                IEnumerable<BattleNetApi.StarCraft.Match> matches = await BattleNetApi.StarCraft.Api.GetMatchHistoryAsync(region, playerId, playerName, _appId);
                result.Matches = (from m in matches select ConvertToMatchResult(m)).ToList();

                return result;
            }
            catch (Exception Ex)
            {
                throw new MatchResultsProviderException(providerPlayerId, continuationToken, Ex);
            }
        }

        private IMatchResult ConvertToMatchResult(BattleNetApi.StarCraft.Match match)
        {
            bool isWin = match.Decision.ToUpperInvariant().Equals("WIN");
            string map = match.Map;
            DateTime datePlayed = UnixTimeStampToDateTime(Double.Parse(match.Date));
            string type = match.Type;
            bool isAcceptedMatchType = IsTypeAccepted(match.Type);
            string decision = match.Decision;

            Log.TraceEvent(TraceEventType.Verbose, 0, 
                @"Creating StarCraftMatch from JSON: isWin:{0}, map:'{1}', datePlayed:'{2}', type:{3}, isAcceptedMatchType:{4}, decision:{5}",
                isWin,
                map,
                datePlayed,
                type,
                isAcceptedMatchType,
                decision);

            return new StarCraftMatchResult(isWin && isAcceptedMatchType, datePlayed, map, type, isAcceptedMatchType, decision);
        }

        private static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToUniversalTime();
            return dtDateTime;
        }

        private string[] _acceptedMatchTypes = 
        {
            "SOLO",
            "TWOS",
            "THREES",
            "FOURS",
            "FFA"
        };

        private bool IsTypeAccepted(string type)
        {
            return _acceptedMatchTypes.Contains(type.ToUpperInvariant());
        }

        public static string GetProviderIdForPlayer(string battleNetName, string battleNetId, string regionString)
        {
            return String.Format(CultureInfo.InvariantCulture, "{0};{1};{2}", battleNetName, battleNetId, regionString);
        }
    }
}
