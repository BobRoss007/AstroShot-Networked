public class SteamNetworkMessage {

    SteamNetworkReader _networkReader;

    #region Properties
    public short MessageType {
        get { return _networkReader.MessageType; }
    }

    public SteamNetworkReader NetworkReader {
        get { return _networkReader; }
    } 
    #endregion

    public SteamNetworkMessage(byte[] bytes) {
        _networkReader = new SteamNetworkReader(bytes);
    }
}

public delegate void SteamNetworkMessageDelegate(SteamNetworkMessage message);