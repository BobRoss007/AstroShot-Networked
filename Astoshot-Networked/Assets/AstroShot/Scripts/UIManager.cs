using System;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour {

    [SerializeField]
    Image _backgroundImage;

    void Awake() {
        SteamNetworkManager.OnConnect += SteamNetworkManager_OnConnect;
        SteamNetworkManager.OnDisconnect += SteamNetworkManager_OnDisonnect;
    }

    void SteamNetworkManager_OnConnect() {
        _backgroundImage.gameObject.SetActive(false);
    }

    void SteamNetworkManager_OnDisonnect() {
        _backgroundImage.gameObject.SetActive(true);
    }
}