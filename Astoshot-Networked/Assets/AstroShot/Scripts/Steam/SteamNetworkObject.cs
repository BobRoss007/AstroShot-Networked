using System;
using Steamworks;
using UnityEngine;
using UnityEngine.Events;

public class SteamNetworkObject : MonoBehaviour {

    [SerializeField]
    UnityEvent _onSpawn;

    [SerializeField]
    UnityEvent _onUnspawn;

    string _prefabId;
    uint _netId;
    CSteamID _ownerId;

    void OnEnable() {
        if(!SteamNetworkManager.SteamLobbyId.IsValid())
            gameObject.SetActive(false);
    }

    #region Properties
    public bool HasAuthority {
        get {
            if(!_ownerId.IsValid()) return false;
            else {
                if(OwnedByLobby)
                    return SteamMatchmaking.GetLobbyOwner(SteamNetworkManager.SteamLobbyId) == SteamUser.GetSteamID();
                else
                    return _ownerId == SteamUser.GetSteamID();
            }
        }
    }

    public uint NetId {
        get { return _netId; }
        set { _netId = value; }
    }

    public UnityEvent OnSpawn {
        get { return _onSpawn; }
    }

    public UnityEvent OnUnspawn {
        get { return _onUnspawn; }
    }

    public bool OwnedByLobby {
        get { return _ownerId.IsLobby(); }
    }

    public CSteamID OwnerId {
        get { return _ownerId; }
        set { _ownerId = value; }
    }

    public string PrefabId {
        get { return _prefabId; }
        set { _prefabId = value; }
    }
    #endregion
}