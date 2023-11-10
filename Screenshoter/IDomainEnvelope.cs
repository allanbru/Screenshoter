using System.Net.Sockets;

namespace Screenshoter
{
    public interface IDomainEnvelope
    {
        TcpClient client { get; set; }
        string url { get; set; }
    }
}