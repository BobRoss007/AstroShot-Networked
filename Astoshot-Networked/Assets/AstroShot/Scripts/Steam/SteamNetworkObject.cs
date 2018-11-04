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
    CSteamID _ownerId;
    NetworkID _id;

    #region Properties
    public CSteamID CreatorId {
        get { return ID.creatorId; }
    }

    public bool HasAuthority {
        get { return SteamIdHasAuthority(SteamUser.GetSteamID()); }
    }

    public NetworkID ID {
        get { return _id; }
        set { _id = value; }
    }

    public uint NetId {
        get { return ID.netId; }
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

    void OnEnable() {
        if(!SteamNetworkManager.SteamLobbyId.IsValid())
            gameObject.SetActive(false);
    }

    public bool SteamIdHasAuthority(CSteamID steamId) {
        if(!_ownerId.IsValid()) return false;
        else {
            if(OwnedByLobby)
                return SteamMatchmaking.GetLobbyOwner(SteamNetworkManager.SteamLobbyId) == steamId;
            else
                return _ownerId == steamId;
        }
    }
}