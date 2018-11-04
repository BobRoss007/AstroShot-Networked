using Steamworks;
using System;

[Serializable]
public struct NetworkID {
    public CSteamID creatorId;
    public uint netId;

    public NetworkID(CSteamID creatorId, uint netId) {
        this.creatorId = creatorId;
        this.netId = netId;
    }

    public override bool Equals(object obj) {
        return base.Equals(obj);
    }

    public override int GetHashCode() {
        return base.GetHashCode();
    }

    public static bool operator ==(CSteamID a, NetworkID b) {
        return a == b.creatorId;
    }
    public static bool operator !=(CSteamID a, NetworkID b) {
        return a != b.creatorId;
    }

    public static bool operator ==(uint a, NetworkID b) {
        return a == b.netId;
    }
    public static bool operator !=(uint a, NetworkID b) {
        return a != b.netId;
    }

    public static bool operator ==(NetworkID a, NetworkID b) {
        return a.creatorId == b.creatorId && a.netId == b.netId;
    }
    public static bool operator !=(NetworkID a, NetworkID b) {
        return !(a == b);
    }
}