using UnityEngine;

[CreateAssetMenu(fileName = "Network Object Database", menuName = "Steam/Network Object Database")]
public class NetworkObjectDatabase : ScriptableObject {

    [SerializeField]
    GameObject[] _networkedObjects;

    public int Count {
        get { return _networkedObjects.Length; }
    }

    public GameObject GetObject(int index) {
        return _networkedObjects[index];
    }

    public GameObject Instantiate(int index) {
        return Instantiate(index, Vector3.zero, Quaternion.identity, null);
    }
    public GameObject Instantiate(int index, Vector3 position, Quaternion rotation) {
        return Instantiate(index, position, rotation, null);
    }
    public GameObject Instantiate(int index, Vector3 position, Quaternion rotation, Transform parent) {
        return Instantiate(GetObject(index), position, rotation, parent);
    }
}