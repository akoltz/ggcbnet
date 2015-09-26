using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatchResultsProvider
{
    public class MatchResultsProviderException : Exception
    {
        public MatchResultsProviderException(string providerPlayerId, string providerPlayerToken, Exception Ex)
            : base("Match results provider failed to retrieve matches for player " + providerPlayerId + " with token " + providerPlayerToken, Ex)
        {
        }
    }
}
