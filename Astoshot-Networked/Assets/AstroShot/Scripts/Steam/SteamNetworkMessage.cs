using Steamworks;

public class SteamNetworkMessage {

    SteamNetworkReader _networkReader;
    CSteamID _senderId;

    #region Properties
    public short MessageType {
        get { return _networkReader.MessageType; }
    }

    public SteamNetworkReader NetworkReader {
        get { return _networkReader; }
    }

    public CSteamID SenderId {
        get { return _senderId; }
        set { _senderId = value; }
    }
    #endregion

    public SteamNetworkMessage(byte[] bytes) {
        _networkReader = new SteamNetworkReader(bytes);
    }
}

public delegate void SteamNetworkMessageDelegate(SteamNetworkMessage message);