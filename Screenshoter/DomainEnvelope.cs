using System.Net.Sockets;

namespace Screenshoter
{
    public struct DomainEnvelope : IDomainEnvelope
    {
        public string url { get; set; }
        public TcpClient client { get; set; }

        public DomainEnvelope(string url, TcpClient client)
        {
            this.url = url;
            this.client = client;
        }
    }

}
