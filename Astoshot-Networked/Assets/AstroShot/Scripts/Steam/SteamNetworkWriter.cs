using Steamworks;
using System.IO;
using UnityEngine;

public class SteamNetworkWriter {

    MemoryStream _stream;
    BinaryWriter _writer;

    bool _streamEnded = false;

    public BinaryWriter Writer {
        get { return _writer; }
    }

    public SteamNetworkWriter(short messageType) {
        _stream = new MemoryStream();
        _writer = new BinaryWriter(_stream);
        _writer.Write(messageType);
    }

    ~SteamNetworkWriter() {
        if(!_streamEnded)
            EndWrite();
    }

    public void EndWrite() {
        _streamEnded = true;
        _writer.Close();
        _stream.Close();
    }

    public void Write(Color color) {
        _writer.Write(color.r);
        _writer.Write(color.g);
        _writer.Write(color.b);
        _writer.Write(color.a);
    }
    public void Write(Color32 color) {
        _writer.Write(color.r);
        _writer.Write(color.g);
        _writer.Write(color.b);
        _writer.Write(color.a);
    }
    public void Write(Quaternion quaternion) {
        _writer.Write(quaternion.x);
        _writer.Write(quaternion.y);
        _writer.Write(quaternion.z);
        _writer.Write(quaternion.w);
    }
    public void Write(CSteamID steamId) {
        _writer.Write(steamId.m_SteamID);
    }
    public void Write(Vector2 vector) {
        _writer.Write(vector.x);
        _writer.Write(vector.y);
    }
    public void Write(Vector3 vector) {
        _writer.Write(vector.x);
        _writer.Write(vector.y);
        _writer.Write(vector.z);
    }
    public void Write(Vector4 vector) {
        _writer.Write(vector.x);
        _writer.Write(vector.y);
        _writer.Write(vector.z);
        _writer.Write(vector.w);
    }
    public void Write(NetworkID id) {
        Write(id.creatorId);
        _writer.Write(id.netId);
    }

    public bool SendTo(SteamPlayer player, EP2PSend sendType, int channelId) {
        if(!_streamEnded)
            EndWrite();

        var buffer = _stream.GetBuffer();
        byte error;

        return player.SendData(buffer, buffer.Length, sendType, channelId, out error);
    }

    public byte[] ToBytes() {
        return _stream.ToArray();
    }


    public static SteamNetworkWriter Create(short messageType) {
        return new SteamNetworkWriter(messageType);
    }
}