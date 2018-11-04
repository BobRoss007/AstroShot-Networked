using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

public class SteamNetworkManager : MonoBehaviour {
    [Obsolete("Set this variable's value to something else", false)]
    public const string GAME_ID = "my_game_title";

    [Obsolete("Set this variable's value to something else", false)]
    public const string SteamPchKey = "my_game_key";

    public static SteamNetworkManager Current { get; private set; }

    public static event Action OnConnect;
    public static event Action OnDisconnect;

    static CSteamID _steamLobbyId;
    static Dictionary<CSteamID, SteamPlayer> _steamPlayers;
    static HashSet<CSteamID> _connectedPlayers;
    static Dictionary<string, GameObject> _registeredPrefabs;
    static Dictionary<NetworkID, GameObject> _spawnedObjects;
    static uint _highestNetId;
    static EP2PSend[] _channels = null;

    static bool _inLobby;

    static Callback<P2PSessionRequest_t> _steamInviteCallback;
    static Callback<LobbyCreated_t> _lobbyCreated;
    static Callback<LobbyEnter_t> _lobbyEntered;
    //Callback<LobbyDataUpdate_t> _lobbyDataUpdate;
    static Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequested;
    static Callback<LobbyChatUpdate_t> _lobbyChatUpdate;
    static Callback<P2PSessionRequest_t> _P2PSessionRequested;
    static CallResult<LobbyMatchList_t> _lobbyMatchList;

    static LobbyData[] _lobbyList = new LobbyData[0];

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

    public static SteamPlayer MyPlayer {
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

    public static Dictionary<NetworkID, GameObject> SpawnedObjects {
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

        if(_registeredPrefabs == null) _registeredPrefabs = new Dictionary<string, GameObject>();
        if(_spawnedObjects == null) _spawnedObjects = new Dictionary<NetworkID, GameObject>();

        _steamPlayers = new Dictionary<CSteamID, SteamPlayer>();
        _connectedPlayers = new HashSet<CSteamID>();

        Current = this;
        DontDestroyOnLoad(this);

        InitializeSteamCallbacks();
    }

    void OnGUI() {
        //GUILayout.BeginHorizontal();
        //{
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(160));
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

        if(_lobbyList.Length > 0) {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300));
            {
                GUILayout.BeginHorizontal();
                {
                    if(GUILayout.Button("Clear"))
                        _lobbyList = new LobbyData[0];

                    GUILayout.Label("Matches -");
                }
                GUILayout.EndHorizontal();

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


        if(_connectedPlayers.Count > 0) {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(200));
            {
                GUILayout.Label("Players (" + _connectedPlayers.Count + ") -");

                foreach(var player in _steamPlayers.Values) {
                    GUILayout.Label(player.Name);
                    GUILayout.Toggle(player.Ready, "Ready");
                    GUILayout.Space(10);
                }
            }
            GUILayout.EndVertical();
        }
        //}
        //GUILayout.EndHorizontal();
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
                        myPlayer.ReceiveData(senderId, data, Convert.ToInt32(packetSize), channel);
                }
            }
        }
    }


    static SteamPlayer AddPlayer(CSteamID steamId) {
        if(!steamId.IsValid()) return null;

        if(_connectedPlayers.Add(steamId)) {
            Debug.LogFormat("Player added Username:{0}", SteamFriends.GetFriendPersonaName(steamId));

            _steamPlayers.Add(steamId, new SteamPlayer(steamId));
        }

        return _steamPlayers[steamId];
    }

    public static void AssignObjectOwner(SteamNetworkObject networkObject, CSteamID ownerId) {
        if(networkObject != null) {
            networkObject.OwnerId = ownerId;

            var writer = SteamNetworkWriter.Create(NetMessageType.Owner);

            writer.Write(networkObject.ID);
            writer.Write(networkObject.OwnerId);
            writer.EndWrite();

            SendWriterToAll(writer, 0, true);
        }
        else
            Debug.LogError("AssignObjectOwner - SteamNetworkObject component required", networkObject);
    }

    public static void ClearRegisteredPrefabs() {
        _registeredPrefabs.Clear();
    }

    static void CreateLobby() {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 8);
    }

    public static void CreateP2PConnectionWithPeer(CSteamID peerId) {
        SteamNetworking.SendP2PPacket(peerId, null, 0, EP2PSend.k_EP2PSendReliable);

        OnPlayerConnected(peerId);
    }

    static void FinishObjectSyncMessageInternal(SteamNetworkMessage message) {
        var senderPlayer = GetPlayer(message.SenderId);

        if(senderPlayer != null) {
            senderPlayer.ReceivedObjectSync = true;

            foreach(var player in _steamPlayers.Values)
                if(!player.ReceivedObjectSync) return;

            SetPlayerReady(MyPlayer.SteamId, true);
        }
    }

    public static NetworkID GenerateId() {
        var netId = ++_highestNetId;

        return new NetworkID(SteamUser.GetSteamID(), netId);
    }

    public static SteamPlayer GetPlayer(CSteamID steamId) {
        SteamPlayer player;

        _steamPlayers.TryGetValue(steamId, out player);

        return player;
    }

    public static GameObject GetSpawnedObject(NetworkID id) {
        GameObject gameObject;

        _spawnedObjects.TryGetValue(id, out gameObject);

        return gameObject;
    }

    public static void InitializeSteamCallbacks() {
        _steamInviteCallback = Callback<P2PSessionRequest_t>.Create(Steam_InviteCallback);
        _lobbyCreated = Callback<LobbyCreated_t>.Create(Steam_OnLobbyCreated);
        _lobbyEntered = Callback<LobbyEnter_t>.Create(Steam_OnLobbyEntered);
        //_lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(Steam_OnLobbyDataUpdate);
        _gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(Steam_OnGameLobbyJoinRequested);
        _lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(Steam_ChatUpdate);
        _P2PSessionRequested = Callback<P2PSessionRequest_t>.Create(Steam_OnP2PSessionRequested);
        _lobbyMatchList = CallResult<LobbyMatchList_t>.Create(Steam_OnLobbyMatchList);
    }

    static void InitializePlayerCallbacks(SteamPlayer player) {
        Debug.Log("InitializePlayerCallbacks");

        player.RegisterHandler(NetMessageType.Spawn, SpawnMessageInternal);
        player.RegisterHandler(NetMessageType.Owner, OwnerMessageInternal);
        player.RegisterHandler(NetMessageType.FinishObjectSync, FinishObjectSyncMessageInternal);
        player.RegisterHandler(NetMessageType.Ready, ReadyMessageInternal);
    }

    public static bool IsMemberInSteamLobby(CSteamID steamUser) {
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

    static void JoinLobby(CSteamID lobbyId) {
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    public static void LeaveLobby() {
        if(_steamLobbyId.IsValid())
            SteamMatchmaking.LeaveLobby(_steamLobbyId);

        OnPlayerDisconnected(SteamUser.GetSteamID());
    }

    static void OnPlayerConnected(CSteamID steamId) {
        var isMe = steamId == SteamUser.GetSteamID();

        AddPlayer(steamId);
        if(!isMe) SendReady(steamId);

        var playerCount = SteamMatchmaking.GetNumLobbyMembers(SteamLobbyId);

        if(playerCount > 1)
            SyncObjectsToNewPlayer(steamId);
        else
            SetPlayerReady(MyPlayer.SteamId, true);

        if(isMe)
            if(OnConnect != null) OnConnect();
    }

    static void OnPlayerDisconnected(CSteamID steamId) {
        if(SteamMatchmaking.GetLobbyOwner(SteamLobbyId) == SteamUser.GetSteamID())
            if(steamId != SteamUser.GetSteamID())
                RemovePlayerObjects(steamId);

        if(steamId == SteamUser.GetSteamID()) {
            if(OnDisconnect != null) OnDisconnect();

            _steamLobbyId.Clear();
            _connectedPlayers.Clear();
            _inLobby = false;
        }
        else {
            _steamPlayers.Remove(steamId);
            _connectedPlayers.Remove(steamId);
        }
    }

    static void OwnerMessageInternal(SteamNetworkMessage message) {
        var id = message.NetworkReader.ReadNetworkID();
        var ownerId = message.NetworkReader.ReadSteamID();

        var gameObject = GetSpawnedObject(id);

        if(gameObject != null) {
            var networkObject = gameObject.GetComponent<SteamNetworkObject>();

            networkObject.ID = id;
            networkObject.OwnerId = ownerId;
        }
        else Debug.LogErrorFormat("No object found to set creatorId({0}) netId({1})", id.creatorId.ToString(), id.netId);
    }

    static void ReadyMessageInternal(SteamNetworkMessage message) {
        var steamId = message.NetworkReader.ReadSteamID();
        var ready = message.NetworkReader.Reader.ReadBoolean();

        SetPlayerReady(steamId, ready);
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

    public static void RemoveAllPlayerObjects() {
        var list = new List<NetworkID>(_spawnedObjects.Keys);

        foreach(var id in list)
            Unspawn(id);

        _spawnedObjects.Clear();
    }

    public static void RemovePlayerObjects(CSteamID steamId) {
        List<NetworkID> removeItems = new List<NetworkID>();

        foreach(var item in _spawnedObjects) {
            var networkObject = item.Value.GetComponent<SteamNetworkObject>();

            if(networkObject.SteamIdHasAuthority(steamId))
                removeItems.Add(new NetworkID(networkObject.CreatorId, networkObject.NetId));
        }

        foreach(var item in removeItems)
            Unspawn(item);
    }

    public static void SendData(byte[] bytes, EP2PSend sendType, int channelId, CSteamID steamId) {
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

    public static void SendEmpty(short messageType, int channelId, CSteamID steamId) {
        var writer = SteamNetworkWriter.Create(messageType);
        writer.EndWrite();

        SendWriter(steamId, writer, channelId);
    }

    public static void SendEmptyToAll(short messageType, int channelId, bool ignoreSelf = false) {
        var writer = SteamNetworkWriter.Create(messageType);
        writer.EndWrite();

        SendWriterToAll(writer, channelId, ignoreSelf);
    }

    static void SendReady(CSteamID sendToSteamId) {
        var writer = SteamNetworkWriter.Create(NetMessageType.Ready);
        writer.Write(SteamUser.GetSteamID());
        writer.Writer.Write(MyPlayer.Ready);
        writer.EndWrite();

        SendWriter(sendToSteamId, writer, 0);
    }

    public static void SendWriter(CSteamID steamId, SteamNetworkWriter writer, int channelId) {
        SendData(writer.ToBytes(), channelId, steamId);
    }

    public static void SendWriterToAll(SteamNetworkWriter writer, int channelId, bool ignoreSelf = false) {
        SendDataToAll(writer.ToBytes(), channelId, ignoreSelf);
    }

    public static void SetPlayerReady(CSteamID steamId, bool ready) {
        if(steamId.IsValid() && !steamId.IsLobby()) {
            if(steamId == SteamUser.GetSteamID()) {
                MyPlayer.Ready = true;

                if(_connectedPlayers.Count > 1) {
                    var writer = SteamNetworkWriter.Create(NetMessageType.Ready);
                    writer.Write(steamId);
                    writer.Writer.Write(ready);
                    writer.EndWrite();

                    SendWriterToAll(writer, 0, true);
                }
            }
            else {
                var player = GetPlayer(steamId);

                if(player != null)
                    player.Ready = ready;
                else
                    Debug.LogErrorFormat("Player ready could not be set Steam Username({0}) Ready({1})", SteamFriends.GetFriendPersonaName(steamId), ready);
            }
        }
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
            var id = GenerateId();
            var gameObject = SpawnInternal(prefabId, id, ownerId, position, rotation);

            if(_connectedPlayers.Count > 1) {
                var writer = SteamNetworkWriter.Create(NetMessageType.Spawn);

                writer.Writer.Write(false); // false = Spawn : true = Unspawn
                writer.Write(id);
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

    static GameObject SpawnInternal(string prefabId, NetworkID id, CSteamID ownerId, Vector3 position, Quaternion rotation) {
        var gameObject = Instantiate(_registeredPrefabs[prefabId], position, rotation);
        var networkObject = gameObject.GetComponent<SteamNetworkObject>();

        networkObject.PrefabId = prefabId;
        networkObject.ID = id;

        if(_spawnedObjects == null) _spawnedObjects = new Dictionary<NetworkID, GameObject>();

        _spawnedObjects.Add(id, gameObject);

        networkObject.OwnerId = ownerId;
        //AssignObjectOwner(networkObject, ownerId);

        if(networkObject.OnSpawn != null) networkObject.OnSpawn.Invoke();

        return gameObject;
    }

    static void SpawnMessageInternal(SteamNetworkMessage message) {
        var spawnType = message.NetworkReader.Reader.ReadBoolean();
        var id = message.NetworkReader.ReadNetworkID();

        if(spawnType) {
            UnspawnInternal(id);
        }
        else {
            var prefabId = message.NetworkReader.Reader.ReadString();
            var ownerId = message.NetworkReader.ReadSteamID();
            var position = message.NetworkReader.ReadVector3();
            var rotation = message.NetworkReader.ReadQuaternion();

            SpawnInternal(prefabId, id, ownerId, position, rotation);
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

    static void SyncObjectsToNewPlayer(CSteamID playerSteamId) {
        foreach(var gameObject in _spawnedObjects.Values) {
            var networkObject = gameObject.GetComponent<SteamNetworkObject>();

            if(networkObject.HasAuthority) {
                var writer = SteamNetworkWriter.Create(NetMessageType.Spawn);

                writer.Writer.Write(false); // false = Spawn : true = Unspawn
                writer.Write(networkObject.ID);
                writer.Writer.Write(networkObject.PrefabId);
                writer.Write(networkObject.OwnerId);
                writer.Write(networkObject.transform.position);
                writer.Write(networkObject.transform.rotation);
                writer.EndWrite();

                SendWriter(playerSteamId, writer, 0);
            }
        }

        SendEmpty(NetMessageType.FinishObjectSync, 0, playerSteamId);
    }

    public static bool UnregisterPrefab(string prefabId) {
        return _registeredPrefabs.Remove(prefabId);
    }

    public static void Unspawn(NetworkID id) {
        if(UnspawnInternal(id))
            if(_steamLobbyId.IsValid() && _connectedPlayers.Count > 1) {
                var writer = SteamNetworkWriter.Create(NetMessageType.Spawn);

                writer.Writer.Write(true); // false = Spawn : true = Unspawn
                writer.Write(id);
                writer.EndWrite();

                SendWriterToAll(writer, 0, true);
            }
    }

    static bool UnspawnInternal(NetworkID id) {
        GameObject gameObject;

        if(_spawnedObjects.TryGetValue(id, out gameObject)) {
            var unityEvent = gameObject.GetComponent<SteamNetworkObject>().OnUnspawn;

            if(unityEvent != null) unityEvent.Invoke();

            Destroy(gameObject); // TODO Add special unspawn/spawn methods that you can register. Just like Unet's spawning system

            _spawnedObjects.Remove(id);

            return true;
        }

        return false;
    }

    #region Steam Callbacks
    static void Steam_ChatUpdate(LobbyChatUpdate_t callback) {
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
                else {
                    OnPlayerDisconnected(userId);
                }

                break;
        }
    }

    static void Steam_InviteCallback(P2PSessionRequest_t callback) {
        Debug.Log("Steam_InviteCallback");
    }

    static void Steam_OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback) {
        Debug.Log("Steam_OnGameLobbyJoinRequested");
    }

    static void Steam_OnLobbyCreated(LobbyCreated_t callback) {
        Debug.Log("Steam_OnLobbyCreated");
    }

    //static void Steam_OnLobbyDataUpdate(LobbyDataUpdate_t callback) {
    //    Debug.Log("Steam_OnLobbyDataUpdate");
    //}

    static void Steam_OnLobbyEntered(LobbyEnter_t callback) {
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

    static void Steam_OnLobbyMatchList(LobbyMatchList_t callback, bool bIOFailure) {
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

    static void Steam_OnP2PSessionRequested(P2PSessionRequest_t callback) {
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