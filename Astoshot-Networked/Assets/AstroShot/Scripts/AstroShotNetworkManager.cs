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
    EP2PSend[] _channels;

    Dictionary<CSteamID, SteamPlayer> _steamPlayers;
    HashSet<CSteamID> _connectedPlayers;
    CSteamID _steamLobbyId;
    bool _inLobby;

    Callback<P2PSessionRequest_t> _steamInviteCallback;
    Callback<LobbyCreated_t> _lobbyCreated;
    Callback<LobbyEnter_t> _lobbyEntered;
    //Callback<LobbyDataUpdate_t> _lobbyDataUpdate;
    Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequested;
    Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
    Callback<P2PSessionRequest_t> _P2PSessionRequested;
    CallResult<LobbyMatchList_t> _lobbyMatchList;

    LobbyData[] _lobbyList = new LobbyData[0];

    #region Properties
    public EP2PSend[] Channels {
        get { return _channels; }
        set { _channels = value; }
    }

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

    SteamPlayer AddPlayer(CSteamID steamId) {
        if(_connectedPlayers.Add(steamId))
            _steamPlayers.Add(steamId, new SteamPlayer(steamId));

        return _steamPlayers[steamId];
    }

    public void InitializeSteamCallbacks() {
        _steamInviteCallback = Callback<P2PSessionRequest_t>.Create(Steam_InviteCallback);
        _lobbyCreated = Callback<LobbyCreated_t>.Create(Steam_OnLobbyCreated);
        _lobbyEntered = Callback<LobbyEnter_t>.Create(Steam_OnLobbyEntered);
        //_lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(Steam_OnLobbyDataUpdate);
        _gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(Steam_OnGameLobbyJoinRequested);
        _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(Steam_ChatUpdate);
        _P2PSessionRequested = Callback<P2PSessionRequest_t>.Create(Steam_OnP2PSessionRequested);
        _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(Steam_OnLobbyMatchList);
    }

    void InitializePlayerCallbacks(SteamPlayer player) {
        player.RegisterHandler(NetMessageType.SendMessageTest, ReceiveMessageTest);
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
                var lobbyData = _lobbyList[i];

                GUILayout.BeginVertical();
                {
                    GUILayout.Label(string.Format("Match ({1}/{2}): {0}", lobbyData.label, SteamMatchmaking.GetNumLobbyMembers(lobbyData.lobbyId), SteamMatchmaking.GetLobbyMemberLimit(lobbyData.lobbyId)));

                    if(!_steamLobbyId.IsValid())
                        if(GUILayout.Button("Join"))
                            JoinLobby(lobbyData.lobbyId);
                }
                GUILayout.EndVertical();
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

    void JoinLobby(CSteamID lobbyId) {
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    void Update() {
        if(!SteamManager.Initialized) return;

        uint packetSize;
        int channelCount = _channels.Length;

        for(int channel = 0; channel < channelCount; channel++) {
            while(SteamNetworking.IsP2PPacketAvailable(out packetSize, channel)) {
                byte[] data = new byte[packetSize];
                CSteamID senderId;

                if(SteamNetworking.ReadP2PPacket(data, packetSize, out packetSize, out senderId, channel)) {
                    P2PSessionState_t sessionState;

                    var player = _steamPlayers[senderId];

                    if(player == null) {
                        Debug.Log("Update AddPlayer");
                        player = AddPlayer(senderId);

                        if(SteamNetworking.GetP2PSessionState(senderId, out sessionState)) {
                            SteamNetworking.CloseP2PSessionWithUser(senderId);
                            SteamNetworking.SendP2PPacket(senderId, null, 0, EP2PSend.k_EP2PSendReliable);
                        }
                    }

                    player.ReceiveData(data, Convert.ToInt32(packetSize), channel);
                }
            }
        }

        if(_steamLobbyId.IsValid()) {
            if(Input.GetKeyDown(KeyCode.Space)) {
                var writer = SteamNetworkWriter.Create(NetMessageType.SendMessageTest);
                writer.Write(SteamUser.GetSteamID());
                writer.EndWrite();

                SendWriterToAll(writer, 1, true);
            }
        }
    }

    void ReceiveMessageTest(SteamNetworkMessage message) {
        Debug.Log("ReceiveMessageTest from");
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
                AddPlayer(userId);
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
        Debug.Log("Steam_OnLobbyCreated");
    }

    //void Steam_OnLobbyDataUpdate(LobbyDataUpdate_t callback) {
    //    Debug.Log("Steam_OnLobbyDataUpdate");
    //}

    void Steam_OnLobbyEntered(LobbyEnter_t callback) {
        Debug.Log("Steam_OnLobbyEntered");
        var mySteamId = SteamUser.GetSteamID();

        _inLobby = true;
        _steamLobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        var steamPlayer = AddPlayer(mySteamId);

        InitializePlayerCallbacks(steamPlayer);

        if(mySteamId == SteamMatchmaking.GetLobbyOwner((CSteamID)callback.m_ulSteamIDLobby)) {
            SteamMatchmaking.SetLobbyData(_steamLobbyId, SteamPchKey, GAME_ID);

            var dateTime = DateTime.Now;

            SteamMatchmaking.SetLobbyData(_steamLobbyId, "time", dateTime.ToLongDateString() + " - " + dateTime.ToLongTimeString());
        }
    }

    void Steam_OnLobbyMatchList(LobbyMatchList_t callback, bool bIOFailure) {
        Debug.Log("Steam_OnLobbyMatchList");

        List<LobbyData> list = new List<LobbyData>();

        for(int i = 0; i < callback.m_nLobbiesMatching; i++) {
            var steamId = SteamMatchmaking.GetLobbyByIndex(i);
            var data = SteamMatchmaking.GetLobbyData(steamId, SteamPchKey);

            if(data == GAME_ID) {
                var time = SteamMatchmaking.GetLobbyData(steamId, "time");
                var ownerId = SteamMatchmaking.GetLobbyOwner(steamId);

                string lobbyData = string.Format("{2}\n{0}:{1}", data, time, SteamFriends.GetFriendPersonaName(ownerId));

                list.Add(new LobbyData() {
                    label = lobbyData,
                    lobbyId = steamId
                });
            }
        }

        _lobbyList = list.ToArray();
    }

    void Steam_OnP2PSessionRequested(P2PSessionRequest_t callback) {
        Debug.Log("Steam_OnP2PSessionRequested");
    }

    public void SendData(byte[] bytes, int channelId, CSteamID steamId) {
        SendData(bytes, _channels[channelId], channelId, steamId);
    }

    public void SendDataToAll(byte[] bytes, int channelId, bool ignoreSelf = false) {
        foreach(var player in _connectedPlayers) {
            if(ignoreSelf && player == SteamUser.GetSteamID()) continue;

            SendData(bytes, _channels[channelId], channelId, player);
        }
    }

    public void SendWriter(CSteamID steamId, SteamNetworkWriter writer, byte[] bytes, int channelId) {
        SendData(writer.ToBytes(), channelId, steamId);
    }

    public void SendWriterToAll(SteamNetworkWriter writer, int channelId, bool ignoreSelf = false) {
        SendDataToAll(writer.ToBytes(), channelId, ignoreSelf);
    }


    public static void SendData(byte[] bytes, EP2PSend sendType, int channelId, CSteamID steamId) {
        SteamNetworking.SendP2PPacket(steamId, bytes, (uint)bytes.Length, sendType, channelId);
    }
    public static void SendData(byte[] bytes, EP2PSend sendType, int channelId, params CSteamID[] steamIds) {
        for(int i = 0; i < steamIds.Length; i++)
            SendData(bytes, sendType, channelId, steamIds[i]);
    }

    public static void SendWriter(SteamNetworkWriter writer, EP2PSend sendType, int channelId, CSteamID steamId) {
        SendData(writer.ToBytes(), sendType, channelId, steamId);
    }
    public static void SendWriter(SteamNetworkWriter writer, EP2PSend sendType, int channelId, params CSteamID[] steamIds) {
        var bytes = writer.ToBytes();

        for(int i = 0; i < steamIds.Length; i++)
            SendData(bytes, sendType, channelId, steamIds[i]);
    }

    struct LobbyData {
        public string label;
        public CSteamID lobbyId;
    }
}