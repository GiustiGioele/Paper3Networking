using System;

public struct RelayHostData
{
    public string _joinCode;
    public string _pv4Address;
    public ushort _port;
    public Guid _allocationID;
    public byte[] _allocationIDBytes;
    public byte[] _connectionData;
    public byte[] _key;
}
