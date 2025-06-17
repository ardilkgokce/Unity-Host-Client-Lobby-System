using Unity.Netcode;
using Unity.Collections;
using System;

[System.Serializable]
public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
{
    public ulong clientId;
    public FixedString64Bytes playerName;
    public bool isReady;
    public int teamId; // 0 = Team A, 1 = Team B, -1 = Inspector
    public bool isInspector; // New field for inspector players
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref playerName);
        serializer.SerializeValue(ref isReady);
        serializer.SerializeValue(ref teamId);
        serializer.SerializeValue(ref isInspector);
    }
    
    public bool Equals(LobbyPlayerData other)
    {
        return clientId == other.clientId && 
               playerName.Equals(other.playerName) && 
               isReady == other.isReady &&
               teamId == other.teamId &&
               isInspector == other.isInspector;
    }
    
    public override bool Equals(object obj)
    {
        return obj is LobbyPlayerData other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(clientId, playerName, isReady, teamId, isInspector);
    }
}