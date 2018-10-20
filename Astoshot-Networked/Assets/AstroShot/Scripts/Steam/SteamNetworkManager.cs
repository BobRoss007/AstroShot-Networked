using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

public class SteamNetworkManager : MonoBehaviour {
    public const string GAME_ID = "astroshot-networked";
    public const string SteamPchKey = "astroshot-game";

    public static SteamNetworkManager Current { get; private set; }

    static CSteamID _steamLobbyId;
    static Dictionary<CSteamID, SteamPlayer> _steamPlayers;
    static HashSet<CSteamID> _connectedPlayers;
    static Dictionary<string, GameObject> _registeredPrefabs;
    static Dictionary<uint, GameObject> _spawnedObjects;
    static uint _highestNetId;
    static EP2PSend[] _channels = null;

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
    public static EP2PSend[] Channels {
        get {
            if(_channels == null)
                _channels = new EP2PSend[]{
                    EP2PSend.k_EP2PSendReliable,
                    EP2PSend.k_EP2PSendUnreliable
                };

            return _channels;
        }
        private set { _channels = value; }
    }

    public SteamPlayer MyPlayer {
        get {
            if(!SteamManager.Initialized) return null;

            var steamId = SteamUser.GetSteamID();
            SteamPlayer player;

            _steamPlayers.TryGetValue(steamId, out player);

            return player;
        }
    }

    public static Dictionary<string, GameObject> RegisteredPrefabs {
        get { return _registeredPrefabs; }
    }

    public static int RegisteredObjectCount {
        get { return _registeredPrefabs == null ? 0 : _registeredPrefabs.Count; }
    }

    public static int SpawnObjectCount {
        get { return _spawnedObjects == null ? 0 : _spawnedObjects.Count; }
    }

    public static Dictionary<uint, GameObject> SpawnedObjects {
        get { return _spawnedObjects; }
    }

    public static CSteamID SteamLobbyId {
        get { return _steamLobbyId; }
        set { _steamLobbyId = value; }
    }
    #endregion

    void Awake() {
        if(Current) {
            Destroy(gameObject);
            return;
        }

        if(_registeredPrefabs == null)
            _registeredPrefabs = new Dictionary<string, GameObject>();

        if(_spawnedObjects == null)
            _spawnedObjects = new Dictionary<uint, GameObject>();

        _steamPlayers = new Dictionary<CSteamID, SteamPlayer>();
        _connectedPlayers = new HashSet<CSteamID>();

        Current = this;
        DontDestroyOnLoad(this);

        InitializeSteamCallbacks();
    }

    SteamPlayer AddPlayer(CSteamID steamId) {
        if(!steamId.IsValid()) return null;

        if(_connectedPlayers.Add(steamId)) {
            Debug.LogFormat("Player added Username:{0}", SteamFriends.GetFriendPersonaName(steamId));

            _steamPlayers.Add(steamId, new SteamPlayer(steamId));
        }

        return _steamPlayers[steamId];
    }

    void CreateLobby() {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 8);
    }

    public void CreateP2PConnectionWithPeer(CSteamID peerId) {
        SteamNetworking.SendP2PPacket(peerId, null, 0, EP2PSend.k_EP2PSendReliable);

        OnPlayerConnected(peerId);
    }

    public SteamPlayer GetPlayer(CSteamID steamId) {
        SteamPlayer player;

        _steamPlayers.TryGetValue(steamId, out player);

        return player;
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
        Debug.Log("InitializePlayerCallbacks");

        //player.RegisterHandler(NetMessageType.SendMessageTest, ReceiveMessageTest);
    }

    public bool IsMemberInSteamLobby(CSteamID steamUser) {
        if(SteamManager.Initialized) {
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(SteamLobbyId);

            for(int i = 0; i < numMembers; i++) {
                var member = SteamMatchmaking.GetLobbyMemberByIndex(SteamLobbyId, i);

                if(member.m_SteamID == steamUser.m_SteamID)
                    return true;
            }
        }

        return false;
    }

    void JoinLobby(CSteamID lobbyId) {
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    public void LeaveLobby() {
        if(_steamLobbyId.IsValid())
            SteamMatchmaking.LeaveLobby(_steamLobbyId);

        _steamLobbyId.Clear();
        _inLobby = false;
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

    void ReceiveMessageTest(SteamNetworkMessage message) {
        Debug.Log("ReceiveMessageTest");
    }

    void OnPlayerConnected(CSteamID steamId) {
        AddPlayer(steamId);
    }

    void OnPlayerDisconnected() {

    }

    //public bool RegisterSpawnedObject(NetworkObjectId id, GameObject gameObject) {
    //    if(!_networkedObjects.ContainsKey(id)) {
    //        _networkedObjects.Add(id, gameObject);
    //        return true;
    //    }

    //    return false;
    //}

    //public bool UnregisterSpawnedObject(NetworkObjectId id) {
    //    return _networkedObjects.Remove(id);
    //}

    void SyncObjectsToNewPlayer(CSteamID playerSteamId) {
        foreach(var gameObject in _spawnedObjects.Values) {
            var networkObject = gameObject.GetComponent<SteamNetworkObject>();

            if(networkObject.HasAuthority) {
                var writer = SteamNetworkWriter.Create(NetMessageType.Spawn);

                writer.Writer.Write(false); // false = Spawn : true = Unspawn
                writer.Writer.Write(networkObject.NetId);
                writer.Writer.Write(networkObject.PrefabId);
                writer.Write(networkObject.OwnerId);
                writer.Write(networkObject.transform.position);
                writer.Write(networkObject.transform.rotation);
                writer.EndWrite();

                SendWriter(playerSteamId, writer, 0);
            }
        }
    }

    void Update() {
        if(!SteamManager.Initialized) return;

        uint packetSize;
        int channelCount = Channels.Length;

        for(int channel = 0; channel < channelCount; channel++) {
            while(SteamNetworking.IsP2PPacketAvailable(out packetSize, channel)) {
                byte[] data = new byte[packetSize];
                CSteamID senderId;

                if(SteamNetworking.ReadP2PPacket(data, packetSize, out packetSize, out senderId, channel)) {
                    P2PSessionState_t sessionState;
                    SteamPlayer player;

                    if(!_steamPlayers.TryGetValue(senderId, out player)) {
                        OnPlayerConnected(senderId);
                        player = GetPlayer(senderId);

                        if(SteamNetworking.GetP2PSessionState(senderId, out sessionState)) {
                            SteamNetworking.CloseP2PSessionWithUser(senderId);
                            SteamNetworking.SendP2PPacket(senderId, null, 0, EP2PSend.k_EP2PSendReliable);
                        }
                    }

                    SteamPlayer myPlayer;

                    if(_steamPlayers.TryGetValue(SteamUser.GetSteamID(), out myPlayer))
                        myPlayer.ReceiveData(data, Convert.ToInt32(packetSize), channel);
                }
            }
        }
    }

    #region Static
    public static void AssignObjectOwner(SteamNetworkObject networkObject, CSteamID ownerId) {
        if(networkObject != null)
            networkObject.OwnerId = ownerId;
        else
            Debug.LogError("AssignObjectOwner - SteamNetworkObject component required", networkObject);
    }

    public static void ClearRegisteredPrefabs() {
        _registeredPrefabs.Clear();
    }

    public static uint GenerateId() {
        return ++_highestNetId;
    }

    public static GameObject GetSpawnedObject(uint netId) {
        GameObject gameObject;

        _spawnedObjects.TryGetValue(netId, out gameObject);

        return gameObject;
    }

    public static bool RegisterPrefab(string prefabId, GameObject gameObject) {
        var networkObject = gameObject.GetComponent<SteamNetworkObject>();

        if(networkObject == null) {
            Debug.LogErrorFormat("RegisterPrefab Failed - GameObject doesn't have a SteamNetworkObject component");
            return false;
        }

        networkObject.PrefabId = prefabId;

        if(_registeredPrefabs == null) _registeredPrefabs = new Dictionary<string, GameObject>();

        if(!_registeredPrefabs.ContainsKey(prefabId)) {
            _registeredPrefabs.Add(prefabId, gameObject);
            return true;
        }

        return false;
    }

    public static void SendData(byte[] bytes, EP2PSend sendType, int channelId, CSteamID steamId) {
        Debug.Log("Sending Data To: " + SteamFriends.GetFriendPersonaName(steamId));

        SteamNetworking.SendP2PPacket(steamId, bytes, bytes == null ? 0 : (uint)bytes.Length, sendType, channelId);
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


    public static void SendData(byte[] bytes, int channelId, CSteamID steamId) {
        SendData(bytes, _channels[channelId], channelId, steamId);
    }

    public static void SendDataToAll(byte[] bytes, int channelId, bool ignoreSelf = false) {
        foreach(var player in _connectedPlayers) {
            if(ignoreSelf && player == SteamUser.GetSteamID()) continue;

            SendData(bytes, _channels[channelId], channelId, player);
        }
    }

    public static void SendWriter(CSteamID steamId, SteamNetworkWriter writer, int channelId) {
        SendData(writer.ToBytes(), channelId, steamId);
    }

    public static void SendWriterToAll(SteamNetworkWriter writer, int channelId, bool ignoreSelf = false) {
        SendDataToAll(writer.ToBytes(), channelId, ignoreSelf);
    }

    public static GameObject Spawn(GameObject gameObject, Vector3 position, Quaternion rotation) {
        return Spawn(gameObject, SteamUser.GetSteamID(), position, rotation);
    }
    public static GameObject Spawn(SteamNetworkObject networkObject, Vector3 position, Quaternion rotation) {
        return Spawn(networkObject, SteamUser.GetSteamID(), position, rotation);
    }
    public static GameObject Spawn(string prefabId, Vector3 position, Quaternion rotation) {
        return Spawn(prefabId, SteamUser.GetSteamID(), position, rotation);
    }
    public static GameObject Spawn(GameObject gameObject, CSteamID ownerId, Vector3 position, Quaternion rotation) {
        return Spawn(gameObject.GetComponent<SteamNetworkObject>(), ownerId, position, rotation);
    }
    public static GameObject Spawn(SteamNetworkObject networkObject, CSteamID ownerId, Vector3 position, Quaternion rotation) {
        if(networkObject == null) return null;

        return Spawn(networkObject.PrefabId, ownerId, position, rotation);
    }
    public static GameObject Spawn(string prefabId, CSteamID ownerId, Vector3 position, Quaternion rotation) {
        if(!ownerId.IsValid()) {
            Debug.LogError("Spawn - Invalid Steam ID for owner");
            return null;
        }

        if(_registeredPrefabs.ContainsKey(prefabId)) {
            var netId = GenerateId();
            var gameObject = SpawnInternal(prefabId, netId, ownerId, position, rotation);

            if(_connectedPlayers.Count > 1) {
                var writer = SteamNetworkWriter.Create(NetMessageType.Spawn);

                writer.Writer.Write(false); // false = Spawn : true = Unspawn
                writer.Writer.Write(netId);
                writer.Writer.Write(prefabId);
                writer.Write(ownerId);
                writer.Write(position);
                writer.Write(rotation);
                writer.EndWrite();

                SendWriterToAll(writer, 0, true);
            }
        }
        else {
            Debug.LogErrorFormat("Spawn - Prefab ID not registered: {0}", prefabId);
        }

        return null;
    }

    static GameObject SpawnInternal(string prefabId, uint netId, CSteamID ownerId, Vector3 position, Quaternion rotation) {
        var gameObject = Instantiate(_registeredPrefabs[prefabId], position, rotation);
        var networkObject = gameObject.GetComponent<SteamNetworkObject>();

        networkObject.PrefabId = prefabId;
        networkObject.NetId = netId;

        if(_spawnedObjects == null) _spawnedObjects = new Dictionary<uint, GameObject>();

        _spawnedObjects.Add(netId, gameObject);

        AssignObjectOwner(networkObject, ownerId);

        if(networkObject.OnSpawn != null) networkObject.OnSpawn.Invoke();

        return gameObject;
    }

    static void SpawnMessageInternal(SteamNetworkMessage message) {
        var spawnType = message.NetworkReader.Reader.ReadBoolean();

        var netId = message.NetworkReader.Reader.ReadUInt32();

        if(spawnType) {
            //UnspawnInternal(
        }
        else {
            var prefabId = message.NetworkReader.Reader.ReadString();
            var ownerId = message.NetworkReader.ReadSteamID();
            var position = message.NetworkReader.ReadVector3();
            var rotation = message.NetworkReader.ReadQuaternion();

            SpawnInternal(prefabId, netId, ownerId, position, rotation);
        }
    }

    public static GameObject SpawnInLobby(GameObject gameObject, Vector3 position, Quaternion rotation) {
        return Spawn(gameObject, SteamLobbyId, position, rotation);
    }
    public static GameObject SpawnInLobby(SteamNetworkObject networkObject, Vector3 position, Quaternion rotation) {
        return Spawn(networkObject, SteamLobbyId, position, rotation);
    }
    public static GameObject SpawnInLobby(string prefabId, Vector3 position, Quaternion rotation) {
        return Spawn(prefabId, SteamLobbyId, position, rotation);
    }

    public static bool UnregisterPrefab(string prefabId) {
        return _registeredPrefabs.Remove(prefabId);
    }

    public static void Unspawn(uint netId) {
        if(UnspawnInternal(netId))
            if(_connectedPlayers.Count > 1) {
                var writer = SteamNetworkWriter.Create(NetMessageType.Spawn);

                writer.Writer.Write(true); // false = Spawn : true = Unspawn
                writer.Writer.Write(netId);
                writer.EndWrite();

                SendWriterToAll(writer, 0, true);
            }
    }
    static bool UnspawnInternal(uint netId) {
        GameObject gameObject;

        if(_spawnedObjects.TryGetValue(netId, out gameObject)) {
            var unityEvent = gameObject.GetComponent<SteamNetworkObject>().OnUnspawn;

            if(unityEvent != null) unityEvent.Invoke();

            Destroy(gameObject); // TODO Add special unspawn/spawn methods that you can register. Just like Unet's spawning system

            _spawnedObjects.Remove(netId);

            return true;
        }

        return false;
    }
    #endregion

    #region Steam Callbacks
    void Steam_ChatUpdate(LobbyChatUpdate_t callback) {
        Debug.Log("Steam_ChatUpdate");

        var userId = new CSteamID(callback.m_ulSteamIDUserChanged);

        switch(callback.m_rgfChatMemberStateChange) {
            case (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered:
                OnPlayerConnected(userId);

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

        OnPlayerConnected(mySteamId);
        var steamPlayer = GetPlayer(mySteamId);

        InitializePlayerCallbacks(steamPlayer);

        if(mySteamId == SteamMatchmaking.GetLobbyOwner((CSteamID)callback.m_ulSteamIDLobby)) {
            SteamMatchmaking.SetLobbyData(_steamLobbyId, SteamPchKey, GAME_ID);

            var dateTime = DateTime.Now;

            SteamMatchmaking.SetLobbyData(_steamLobbyId, "time", dateTime.ToLongDateString() + " - " + dateTime.ToLongTimeString());
        }
        else {
            var memberCount = SteamMatchmaking.GetNumLobbyMembers(_steamLobbyId);

            for(int i = 0; i < memberCount; i++) {
                var memberId = SteamMatchmaking.GetLobbyMemberByIndex(_steamLobbyId, i);

                if(memberId != mySteamId)
                    OnPlayerConnected(memberId);
            }
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
        var memberId = callback.m_steamIDRemote;

        if(IsMemberInSteamLobby(memberId)) {
            SteamNetworking.AcceptP2PSessionWithUser(memberId);
            CreateP2PConnectionWithPeer(memberId);
        }
    }
    #endregion

    struct LobbyData {
        public string label;
        public CSteamID lobbyId;
    }
}