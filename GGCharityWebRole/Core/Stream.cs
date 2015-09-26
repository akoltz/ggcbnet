using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace GGCharityWebRole
{
    public enum StreamType
    {
        Unknown = 0,
        Twitch = 1,
    }

    public abstract class PlayerStream
    {
        public static async Task<PlayerStream> FromStreamUrlAsync(string StreamUrl)
        {
            if (!StreamUrl.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase))
            {
                StreamUrl = "http://" + StreamUrl;
            }

            Uri StreamUri = new Uri(StreamUrl, UriKind.Absolute);

            if (StreamUri.Host.ToLower().Equals("www.twitch.tv"))
            {
                var stream = new TwitchStream(StreamUri, StreamUrl);
                stream.IsStreamOnline = await stream.IsStreamOnlineAsync().ConfigureAwait(false);
                return stream;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        protected PlayerStream(StreamType Type)
        {

        }

        protected abstract Task<bool> IsStreamOnlineAsync();
        public abstract string Username { get; }
        public StreamType Type { get; private set; }
        public bool IsStreamOnline { get; private set; }
        public static bool ValidateStreamUrl(string StreamUrl)
        {
            Uri URI;

            /// Determine whether this is a relative or absolute URI
            /// 

            if (StreamUrl == null || StreamUrl == String.Empty)
            {
                return true;
            }

            if (!Uri.TryCreate(StreamUrl, UriKind.Absolute, out URI)
                || !URI.IsAbsoluteUri)
            {
                return false;
            }

            return true;
        }
    }

    public class TwitchStream : PlayerStream
    {
        internal TwitchStream(Uri StreamUri, string StreamUrl)
            : base(StreamType.Twitch)
        {
            if (StreamUri.PathAndQuery.Count(c => c.Equals('/')) > 1)
            {
                _username = StreamUri.PathAndQuery.Substring(1, StreamUri.PathAndQuery.IndexOf('/', 1) - 1);
            }
            else
            {
                _username = StreamUri.PathAndQuery.Substring(1);
            }

            ChannelUrl = StreamUrl;
        }

        public string ChannelUrl { get; private set; }

        protected override async Task<bool> IsStreamOnlineAsync()
        {
            try
            {
                using (System.Net.WebClient wc = new System.Net.WebClient())
                {
                    string RequestString;
                    RequestString = await wc.DownloadStringTaskAsync(new Uri("https://api.twitch.tv/kraken/streams/" + Username)).ConfigureAwait(false);

                    if (!RequestString.Contains("\"stream\":null"))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
#if DEBUG
                // Outside of debug mode, we're willing to completely ignore stream retrieval failures.
                throw;
#endif
            }

            return false;
        }

        string _username;
        public override string Username
        {
            get
            {
                return _username;
            }
        }
    }
}