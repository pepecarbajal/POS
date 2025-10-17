using System;

namespace POS.Interfaces
{
    public interface INFCReaderService : IDisposable
    {
        event EventHandler<string>? CardScanned;
        bool Connect();
        void Disconnect();
        bool IsConnected { get; }
    }
}
