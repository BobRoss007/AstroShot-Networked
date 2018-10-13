using Steamworks;
using System.IO;
using UnityEngine;

public class SteamNetworkReader {

    short _messageType;

    MemoryStream _stream;
    BinaryReader _reader;

    bool _streamEnded = false;

    #region Properties
    public short MessageType {
        get { return _messageType; }
    }

    public BinaryReader Reader {
        get { return _reader; }
    }
    #endregion

    public SteamNetworkReader(byte[] bytes) {
        _stream = new MemoryStream(bytes);
        _reader = new BinaryReader(_stream);
        Debug.Log("Data Length = " + _stream.Length);
        _messageType = _reader.ReadInt16();
    }

    ~SteamNetworkReader() {
        _reader.Close();
        _stream.Close();
    }

    public Color ReadColor() {
        var r = _reader.ReadSingle();
        var g = _reader.ReadSingle();
        var b = _reader.ReadSingle();
        var a = _reader.ReadSingle();

        return new Color(r, g, b, a);
    }

    public Color32 ReadColor32() {
        var r = _reader.ReadByte();
        var g = _reader.ReadByte();
        var b = _reader.ReadByte();
        var a = _reader.ReadByte();

        return new Color32(r, g, b, a);
    }

    public Quaternion ReadQuaternion() {
        var x = _reader.ReadSingle();
        var y = _reader.ReadSingle();
        var z = _reader.ReadSingle();
        var w = _reader.ReadSingle();

        return new Quaternion(x, y, z, w);
    }

    public CSteamID ReadSteamID() {
        return (CSteamID)_reader.ReadUInt64();
    }

    public Vector2 ReadVector2() {
        var x = _reader.ReadSingle();
        var y = _reader.ReadSingle();

        return new Vector2(x, y);
    }

    public Vector3 ReadVector3() {
        var x = _reader.ReadSingle();
        var y = _reader.ReadSingle();
        var z = _reader.ReadSingle();

        return new Vector3(x, y, z);
    }

    public Vector4 ReadVector4() {
        var x = _reader.ReadSingle();
        var y = _reader.ReadSingle();
        var z = _reader.ReadSingle();
        var w = _reader.ReadSingle();

        return new Vector4(x, y, z, w);
    }
}