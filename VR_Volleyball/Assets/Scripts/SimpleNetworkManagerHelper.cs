using UnityEngine;
using Unity.Netcode;

public class NetworkManagerHelper : MonoBehaviour
{
    [Header("Network Settings")]
    public int maxConnections = 4;
    public int tickRate = 30;
    
    [Header("Debug")]
    public bool enableDebugLogs = true;
    
    private void Awake()
    {
        // Ensure NetworkManager exists
        var networkManager = GetComponent<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager component not found!");
            return;
        }
    }
    
    private void Start()
    {
        var networkManager = GetComponent<NetworkManager>();
        if (networkManager != null && networkManager.NetworkConfig != null)
        {
            // Configure network settings
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApprovalCheck;
            networkManager.NetworkConfig.TickRate = (uint)tickRate;
            
            // Set reliable delivery for better synchronization
            networkManager.NetworkConfig.EnableSceneManagement = true;
            networkManager.NetworkConfig.EnableNetworkLogs = enableDebugLogs;
        }
    }
    
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // Check current connections
        int currentConnections = NetworkManager.Singleton.ConnectedClients.Count;
        bool hasSpace = currentConnections < maxConnections;
        
        if (!hasSpace)
        {
            response.Approved = false;
            response.Reason = "Sunucu dolu!";
            Debug.Log($"Connection rejected: Server full ({currentConnections}/{maxConnections})");
            return;
        }
        
        // Approve connection
        response.Approved = true;
        response.CreatePlayerObject = false; // Don't create player object for lobby
        response.Pending = false;
        
        Debug.Log($"Connection approved. Total players will be: {currentConnections + 1}/{maxConnections}");
    }
    
    private void OnDestroy()
    {
        var networkManager = GetComponent<NetworkManager>();
        if (networkManager != null)
        {
            networkManager.ConnectionApprovalCallback = null;
        }
    }
    
    // Helper methods for debugging
    public void LogNetworkState()
    {
        if (!enableDebugLogs) return;
        
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        
        Debug.Log($"[Network State] IsHost: {nm.IsHost}, IsServer: {nm.IsServer}, IsClient: {nm.IsClient}");
        Debug.Log($"[Network State] Connected Clients: {nm.ConnectedClients.Count}");
        
        foreach (var client in nm.ConnectedClients)
        {
            Debug.Log($"  - Client {client.Key}: Connected");
        }
    }
}