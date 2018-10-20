using UnityEngine;

public class TestSpawner : MonoBehaviour {

    [SerializeField]
    SteamNetworkObject _networkObjectPrefab;

    [SerializeField]
    float _range = 1;

    void Start() {
        SteamNetworkManager.RegisterPrefab("Test Spawner Object", _networkObjectPrefab.gameObject);
    }

    //[ContextMenu("Spawn", true)]
    //bool SpawnValidate() {
    //    return SteamNetworkManager.SteamLobbyId.IsValid();
    //}

    [ContextMenu("Spawn")]
    public void Spawn() {
        if(SteamNetworkManager.SteamLobbyId.IsValid()) {
            var randomDirection = Random.insideUnitCircle * _range;
            Vector3 position = new Vector3(randomDirection.x, 0, randomDirection.y);

            SteamNetworkManager.Spawn(_networkObjectPrefab, position, Quaternion.identity);
        }
    }

    [ContextMenu("Debug Registered Prefabs")]
    public void DebugRegisteredPrefabs() {
        foreach(var item in SteamNetworkManager.RegisteredPrefabs) {
            Debug.LogFormat("Registered Prefabe ID: {0}", item.Key);
        }
    }
}