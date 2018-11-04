using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class SteamPlayer {

    Dictionary<short, SteamNetworkMessageDelegate> _registeredHandlers;
    CSteamID _steamId;
    bool _ready;
    bool _receivedObjectSync;

    #region Properties
    public string Name {
        get { return SteamFriends.GetFriendPersonaName(_steamId); }
    }

    public bool Ready {
        get { return _ready; }
        set { _ready = value; }
    }

    public bool ReceivedObjectSync {
        get { return _receivedObjectSync; }
        set { _receivedObjectSync = value; }
    }

    public Dictionary<short, SteamNetworkMessageDelegate> RegisteredHandlers {
        get { return _registeredHandlers; }
    }

    public CSteamID SteamId {
        get { return _steamId; }
        set { _steamId = value; }
    }
    #endregion

    public SteamPlayer(CSteamID steamId) {
        _steamId = steamId;

        _registeredHandlers = new Dictionary<short, SteamNetworkMessageDelegate>();
    }

    public void ClearHandlers() {
        _registeredHandlers.Clear();
    }

    public virtual void ReceiveData(CSteamID senderId, byte[] bytes, int byteCount, int channelId) {
        var message = new SteamNetworkMessage(bytes);
        var messageType = message.MessageType;

        message.SenderId = senderId;

        if(messageType > -1)
            if(_registeredHandlers.ContainsKey(messageType))
                _registeredHandlers[messageType](message);
    }

    public void RegisterHandler(short messageType, SteamNetworkMessageDelegate method) {
        if(_registeredHandlers.ContainsKey(messageType))
            _registeredHandlers[messageType] = method;
        else
            _registeredHandlers.Add(messageType, method);
    }

    public virtual bool SendData(byte[] bytes, int byteCount, EP2PSend sendType, int channelId, out byte error) {
        if(_steamId == SteamUser.GetSteamID()) {
            ReceiveData(_steamId, bytes, byteCount, channelId);
            error = 0;
            return true;
        }

        if(SteamNetworking.SendP2PPacket(SteamId, bytes, (uint)byteCount, sendType, channelId)) {
            error = 0;
            return true;
        }
        else {
            error = 1;
            return false;
        }
    }

    public bool SendWriter(SteamNetworkWriter writer, EP2PSend sendType, int channelId) {
        byte error;
        var bytes = writer.ToBytes();

        return SendData(bytes, bytes.Length, sendType, channelId, out error);
    }

    public void UnregisterHandler(short messageType) {
        _registeredHandlers.Remove(messageType);
    }
}