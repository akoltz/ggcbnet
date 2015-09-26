using MatchResults;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MatchResultsProvider
{
    public enum MatchOrdering
    {
        MostRecentlyPlayedFirst = 0,
        LeastRecentlyPlayedFirst = 1
    }

    public interface IMatchResultsProviderResult
    {
        List<IMatchResult> Matches { get; }
        string ContinuationToken { get; }
        MatchOrdering Order { get; }
    }

    public interface IMatchResultsProvider
    {
        Task<IMatchResultsProviderResult> GetMatchesForPlayerAsync(string providerPlayerId, string providerPlayerToken);
    }

    public interface IMatchResultsProviderFactory
    {
        IMatchResultsProvider CreateInstance(GGCharityData.GameID gameId);
    }

    public class MatchResultsProviderResult : IMatchResultsProviderResult
    {
        public MatchResultsProviderResult()
        {
            Matches = new List<IMatchResult>();
        }

        public List<IMatchResult> Matches
        {
            get;
            set;
        }

        public string ContinuationToken
        {
            get;
            set;
        }

        public MatchOrdering Order
        {
            get;
            set;
        }
    }
}
