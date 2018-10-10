using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

public class AstroShotNetworkManager : MonoBehaviour {
    public const string GAME_ID = "astroshot-networked";
    public const string SteamPchKey = "astroshot-game";

    public static AstroShotNetworkManager Current { get; private set; }

    [SerializeField]
    HostTopology _hostTopology;

    Dictionary<CSteamID, SteamPlayer> _steamPlayers;
    HashSet<CSteamID> _connectedPlayers;
    CSteamID _steamLobbyId;
    bool _inLobby;

    Callback<P2PSessionRequest_t> _steamInviteCallback;
    Callback<LobbyCreated_t> _lobbyCreated;
    Callback<LobbyEnter_t> _lobbyEntered;
    Callback<LobbyDataUpdate_t> _lobbyDataUpdate;
    Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequested;
    Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
    Callback<P2PSessionRequest_t> _P2PSessionRequested;
    CallResult<LobbyMatchList_t> _lobbyMatchList;

    string[] _lobbyList = new string[0];

    #region Properties
    public CSteamID SteamLobbyId {
        get { return _steamLobbyId; }
        set { _steamLobbyId = value; }
    }
    #endregion

    void Awake() {
        if(Current) {
            Destroy(gameObject);
            return;
        }

        _steamPlayers = new Dictionary<CSteamID, SteamPlayer>();
        _connectedPlayers = new HashSet<CSteamID>();

        Current = this;
        DontDestroyOnLoad(this);

        InitializeSteamCallbacks();
    }

    public void InitializeSteamCallbacks() {
        _steamInviteCallback = Callback<P2PSessionRequest_t>.Create(Steam_InviteCallback);
        _lobbyCreated = Callback<LobbyCreated_t>.Create(Steam_OnLobbyCreated);
        _lobbyEntered = Callback<LobbyEnter_t>.Create(Steam_OnLobbyEntered);
        _lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(Steam_OnLobbyDataUpdate);
        _gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(Steam_OnGameLobbyJoinRequested);
        _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(Steam_ChatUpdate);
        _P2PSessionRequested = Callback<P2PSessionRequest_t>.Create(Steam_OnP2PSessionRequested);
        _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(Steam_OnLobbyMatchList);
    }

    void OnGUI() {
        GUILayout.BeginVertical(GUILayout.Width(160));
        {
            if(_steamLobbyId.IsValid()) {
                if(GUILayout.Button("Leave"))
                    LeaveLobby();

                GUILayout.Label("Lobby ID: " + _steamLobbyId.m_SteamID);
            }
            else {
                if(GUILayout.Button("Create Lobby"))
                    CreateLobby();
            }

            GUILayout.Toggle(_inLobby, "In Lobby");

            if(GUILayout.Button("Find Matches"))
                _lobbyMatchList.Set(SteamMatchmaking.RequestLobbyList(), Steam_OnLobbyMatchList);

        }
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        {
            for(int i = 0; i < _lobbyList.Length; i++) {
                GUILayout.Label("Match: " + _lobbyList[i]);
            }
        }
        GUILayout.EndVertical();
    }

    void CreateLobby() {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 8);
    }

    public void LeaveLobby() {
        if(_steamLobbyId.IsValid())
            SteamMatchmaking.LeaveLobby(_steamLobbyId);

        _steamLobbyId.Clear();
        _inLobby = false;
    }

    void Update() {
        if(!SteamManager.Initialized) return;

        uint packetSize;
        int channels = _hostTopology.DefaultConfig.ChannelCount;

        for(int channel = 0; channel < channels; channel++) {
            while(SteamNetworking.IsP2PPacketAvailable(out packetSize, channel)) {
                byte[] data = new byte[packetSize];
                CSteamID senderId;

                if(SteamNetworking.ReadP2PPacket(data, packetSize, out packetSize, out senderId, channel)) {
                    P2PSessionState_t sessionState;

                    if(SteamNetworking.GetP2PSessionState(senderId, out sessionState)) {
                        SteamNetworking.CloseP2PSessionWithUser(senderId);
                        SteamNetworking.SendP2PPacket(senderId, null, 0, EP2PSend.k_EP2PSendReliable);
                    }
                }
            }
        }
    }

    //public void SendMessage(byte[] bytes, int numBytes, int channelId, out byte error) {
    //    error = 0;
    //}

    //public void Host() {

    //    //RegisterServerHandlers();
    //}

    //public void Join() {
    //    //RegisterClientHandlers();
    //}

    void OnPlayerConnected() {

    }

    void OnPlayerDisconnected() {

    }

    //void OnServerStarted() {

    //}

    //void OnServerStopped() {

    //}

    void Steam_ChatUpdate(LobbyChatUpdate_t callback) {
        Debug.Log("Steam_ChatUpdate");

        var userId = new CSteamID(callback.m_ulSteamIDUserChanged);

        switch(callback.m_rgfChatMemberStateChange) {
            case (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered:
                _connectedPlayers.Add(userId);
                _steamPlayers.Add(userId, new SteamPlayer(userId));
                Debug.LogFormat("Player joined Username:{0}", SteamFriends.GetFriendPersonaName(userId));

                break;
            case (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft:
            case (uint)EChatMemberStateChange.k_EChatMemberStateChangeDisconnected:
            case (uint)EChatMemberStateChange.k_EChatMemberStateChangeKicked:
            case (uint)EChatMemberStateChange.k_EChatMemberStateChangeBanned:
                _connectedPlayers.Remove(userId);
                _steamPlayers.Remove(userId);
                Debug.LogFormat("Player left Username:{0}", SteamFriends.GetFriendPersonaName(userId));

                SteamNetworking.CloseP2PSessionWithUser(userId);

                if(userId == SteamUser.GetSteamID()) {
                    _connectedPlayers.Clear();
                    _steamPlayers.Clear();

                    LeaveLobby();
                }

                break;
        }
    }

    void Steam_InviteCallback(P2PSessionRequest_t callback) {
        Debug.Log("Steam_InviteCallback");
    }

    void Steam_OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback) {
        Debug.Log("Steam_OnGameLobbyJoinRequested");
    }

    void Steam_OnLobbyCreated(LobbyCreated_t callback) {
        //SteamMatchmaking.SetLobbyData(_steamLobbyId, SteamPchKey, GAME_ID);
        //SteamMatchmaking.SetLobbyData(_steamLobbyId, "time", DateTime.Now.ToShortDateString());
        Debug.Log("Steam_OnLobbyCreated");
    }

    void Steam_OnLobbyDataUpdate(LobbyDataUpdate_t callback) {
        Debug.Log("Steam_OnLobbyDataUpdate");
    }

    void Steam_OnLobbyEntered(LobbyEnter_t callback) {
        Debug.Log("Steam_OnLobbyEntered");
        _inLobby = true;
        _steamLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        if(SteamUser.GetSteamID() == SteamMatchmaking.GetLobbyOwner((CSteamID)callback.m_ulSteamIDLobby)) {
            SteamMatchmaking.SetLobbyData(_steamLobbyId, SteamPchKey, GAME_ID);

            var dateTime = DateTime.Now;

            SteamMatchmaking.SetLobbyData(_steamLobbyId, "time", dateTime.ToLongDateString() + " - " + dateTime.ToLongTimeString());
        }
    }

    void Steam_OnLobbyMatchList(LobbyMatchList_t callback, bool bIOFailure) {
        Debug.Log("Steam_OnLobbyMatchList IOFailure:" + bIOFailure);
        //SteamMatchmaking.
        _lobbyList = new string[callback.m_nLobbiesMatching];

        for(int i = 0; i < callback.m_nLobbiesMatching; i++) {
            var steamId = SteamMatchmaking.GetLobbyByIndex(i);
            var data = SteamMatchmaking.GetLobbyData(steamId, SteamPchKey);
            var time = SteamMatchmaking.GetLobbyData(steamId, "time");

            data += ":" + time;

            _lobbyList[i] = data;
        }
    }

    void Steam_OnP2PSessionRequested(P2PSessionRequest_t callback) {
        Debug.Log("Steam_OnP2PSessionRequested");
    }

    //void RegisterServerHandlers() {
    //    NetworkServer.RegisterHandler(MsgType.Connect, OnConnected);
    //    NetworkServer.RegisterHandler(MsgType.Disconnect, OnDisconnected);
    //}

    //void RegisterClientHandlers(NetworkClient client) {
    //    client.RegisterHandler(MsgType.Connect, OnConnected);
    //    client.RegisterHandler(MsgType.Disconnect, OnDisconnected);
    //}
}