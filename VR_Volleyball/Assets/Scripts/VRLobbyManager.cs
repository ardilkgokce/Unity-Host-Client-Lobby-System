using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Collections;

public class VRLobbyManager : NetworkBehaviour
{
    [Header("UI References")]
    public GameObject hostButton;
    public GameObject joinButton;
    public GameObject joinAsInspectorButton; // NEW: Inspector button
    public GameObject disconnectButton;
    public TMP_InputField ipInputField;
    public TMP_InputField playerNameInputField;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI playerCountText;
    
    [Header("Lobby Settings")]
    public int maxPlayers = 4;
    public int maxInspectors = 2; // NEW: Max inspector limit
    public string defaultIP = "127.0.0.1";
    public ushort port = 7777;
    public string defaultPlayerName = "Player";
    
    // Network variables for synchronization
    private NetworkVariable<int> connectedPlayersCount = new NetworkVariable<int>(0);
    private NetworkList<LobbyPlayerData> lobbyPlayers;
    
    // Events
    public System.Action<List<LobbyPlayerData>> OnPlayerListUpdated;
    public System.Action<bool> OnConnectionStatusChanged;
    
    // Local cache for player name and type
    private string localPlayerName;
    private bool isLocalInspector = false; // NEW: Track if local player is inspector
    
    private void Awake()
    {
        lobbyPlayers = new NetworkList<LobbyPlayerData>();
    }
    
    private void Start()
    {
        InitializeUI();
        SetupNetworkCallbacks();
        
        // Load saved player name
        if (PlayerPrefs.HasKey("PlayerName"))
        {
            playerNameInputField.text = PlayerPrefs.GetString("PlayerName");
        }
    }
    
    private void InitializeUI()
    {
        hostButton.SetActive(true);
        joinButton.SetActive(true);
        if (joinAsInspectorButton != null)
            joinAsInspectorButton.SetActive(true);
        disconnectButton.SetActive(false);
        
        if (ipInputField != null)
            ipInputField.text = defaultIP;
            
        if (playerNameInputField != null && string.IsNullOrEmpty(playerNameInputField.text))
            playerNameInputField.text = defaultPlayerName;
            
        UpdateStatusText("Bekleniyor...");
        UpdatePlayerCount(0);
        
        hostButton.GetComponent<Button>().onClick.AddListener(StartHost);
        joinButton.GetComponent<Button>().onClick.AddListener(StartClient);
        if (joinAsInspectorButton != null)
            joinAsInspectorButton.GetComponent<Button>().onClick.AddListener(StartClientAsInspector);
        disconnectButton.GetComponent<Button>().onClick.AddListener(Disconnect);
    }
    
    private void SetupNetworkCallbacks()
    {
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        
        lobbyPlayers.OnListChanged += OnLobbyPlayersChanged;
    }
    
    public void StartHost()
    {
        if (!ValidateAndSavePlayerName())
        {
            UpdateStatusText("Geçersiz isim! (3-20 karakter)");
            return;
        }
        
        isLocalInspector = false; // Host is never an inspector
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(defaultIP, port);
        
        NetworkManager.Singleton.StartHost();
        UpdateStatusText("Host başlatılıyor...");
    }
    
    public void StartClient()
    {
        if (!ValidateAndSavePlayerName())
        {
            UpdateStatusText("Geçersiz isim! (3-20 karakter)");
            return;
        }
        
        isLocalInspector = false; // Regular player
        string targetIP = string.IsNullOrEmpty(ipInputField.text) ? defaultIP : ipInputField.text;
        
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(targetIP, port);
        
        NetworkManager.Singleton.StartClient();
        UpdateStatusText($"Bağlanılıyor: {targetIP}:{port}");
    }
    
    public void StartClientAsInspector()
    {
        if (!ValidateAndSavePlayerName())
        {
            UpdateStatusText("Geçersiz isim! (3-20 karakter)");
            return;
        }
        
        isLocalInspector = true; // Inspector player
        string targetIP = string.IsNullOrEmpty(ipInputField.text) ? defaultIP : ipInputField.text;
        
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(targetIP, port);
        
        NetworkManager.Singleton.StartClient();
        UpdateStatusText($"Inspector olarak bağlanılıyor: {targetIP}:{port}");
    }
    
    private bool ValidateAndSavePlayerName()
    {
        string playerName = playerNameInputField.text.Trim();
        
        if (string.IsNullOrWhiteSpace(playerName) || playerName.Length < 3 || playerName.Length > 20)
        {
            return false;
        }
        
        localPlayerName = playerName;
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.Save();
        return true;
    }
    
    public void Disconnect()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        UpdateStatusText("Bağlantı kesildi");
        SetUIForDisconnected();
    }
    
    private void OnServerStarted()
    {
        if (IsServer)
        {
            Debug.Log("Server started successfully");
            // Host automatically joins as first player
            AddPlayerToLobby(NetworkManager.Singleton.LocalClientId, localPlayerName + " (Host)", 0, false);
            SetUIForConnected();
            UpdateStatusText("Lobby oluşturuldu - Oyuncular bekleniyor...");
        }
    }
    
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected: {clientId}");
        
        if (IsServer)
        {
            // Handle new client connection on server
            if (clientId != NetworkManager.Singleton.LocalClientId)
            {
                // Wait for client to send their name
                UpdateStatusText($"Yeni oyuncu bağlandı: {clientId}");
            }
        }
        
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Local client connected
            SetUIForConnected();
            
            if (!IsServer)
            {
                // Client sends their name and type to server
                SendPlayerInfoToServerRpc(localPlayerName, isLocalInspector);
                UpdateStatusText(isLocalInspector ? "Inspector olarak lobbye katıldınız!" : "Lobbye katıldınız!");
            }
            
            OnConnectionStatusChanged?.Invoke(true);
        }
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client disconnected: {clientId}");
        
        if (IsServer)
        {
            RemovePlayerFromLobby(clientId);
            UpdateStatusText($"Oyuncu ayrıldı: {clientId}");
        }
        
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            SetUIForDisconnected();
            OnConnectionStatusChanged?.Invoke(false);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void SendPlayerInfoToServerRpc(string playerName, bool isInspector, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Validate name on server
        if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 20)
        {
            playerName = $"Player_{clientId}";
        }
        
        // Check inspector limit
        if (isInspector)
        {
            int currentInspectorCount = 0;
            foreach (var player in lobbyPlayers)
            {
                if (player.isInspector) currentInspectorCount++;
            }
            
            if (currentInspectorCount >= maxInspectors)
            {
                // Too many inspectors, join as regular player instead
                isInspector = false;
                NotifyPlayerTypeClientRpc(clientId, false, "Inspector limiti dolu! Normal oyuncu olarak katıldınız.");
            }
        }
        
        // Add player with appropriate settings
        if (isInspector)
        {
            AddPlayerToLobby(clientId, playerName + " (Inspector)", -1, true);
        }
        else
        {
            int assignedTeam = GetBalancedTeamAssignment();
            AddPlayerToLobby(clientId, playerName, assignedTeam, false);
        }
        
        Debug.Log($"Player {playerName} (ID: {clientId}) joined as {(isInspector ? "Inspector" : $"Team {(isInspector ? "N/A" : "Player")}")}");
    }
    
    [ClientRpc]
    private void NotifyPlayerTypeClientRpc(ulong targetClientId, bool confirmedAsInspector, string message)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            UpdateStatusText(message);
            isLocalInspector = confirmedAsInspector;
        }
    }
    
    private void AddPlayerToLobby(ulong clientId, string playerName, int teamId, bool isInspector)
    {
        if (!IsServer) return;
        
        // Check if player already exists
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].clientId == clientId)
            {
                Debug.LogWarning($"Player {clientId} already in lobby");
                return;
            }
        }
        
        var playerData = new LobbyPlayerData
        {
            clientId = clientId,
            playerName = playerName,
            isReady = false,
            teamId = teamId,
            isInspector = isInspector
        };
        
        lobbyPlayers.Add(playerData);
        connectedPlayersCount.Value = lobbyPlayers.Count;
        
        Debug.Log($"Added player to lobby: {playerName} (Team {teamId}, Inspector: {isInspector})");
    }
    
    private void RemovePlayerFromLobby(ulong clientId)
    {
        if (!IsServer) return;
        
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            if (lobbyPlayers[i].clientId == clientId)
            {
                lobbyPlayers.RemoveAt(i);
                connectedPlayersCount.Value = lobbyPlayers.Count;
                Debug.Log($"Removed player {clientId} from lobby");
                break;
            }
        }
    }
    
    private int GetBalancedTeamAssignment()
    {
        // For easier testing, just alternate teams or let players choose
        // Inspectors are not counted in team balance
        int teamACount = 0;
        int teamBCount = 0;
        
        foreach (var player in lobbyPlayers)
        {
            if (!player.isInspector) // Only count non-inspector players
            {
                if (player.teamId == 0) teamACount++;
                else if (player.teamId == 1) teamBCount++;
            }
        }
        
        // Still try to balance initially, but no restrictions on switching
        return teamACount <= teamBCount ? 0 : 1;
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ChangeTeamServerRpc(int newTeamId, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        // Update player's team
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            var player = lobbyPlayers[i];
            if (player.clientId == clientId)
            {
                // Inspectors cannot change teams
                if (player.isInspector)
                {
                    NotifyTeamChangeResultClientRpc(clientId, false, "Inspectorlar takım değiştiremez!");
                    return;
                }
                
                // Check if already on this team
                if (player.teamId == newTeamId)
                {
                    NotifyTeamChangeResultClientRpc(clientId, false, $"Zaten Team {newTeamId} içindesiniz!");
                    return;
                }
                
                player.teamId = newTeamId;
                lobbyPlayers[i] = player;
                
                NotifyTeamChangeResultClientRpc(clientId, true, $"Team {newTeamId} olarak değiştirildi!");
                Debug.Log($"Player {clientId} changed to team {newTeamId}");
                break;
            }
        }
    }
    
    [ClientRpc]
    private void NotifyTeamChangeResultClientRpc(ulong targetClientId, bool success, string message)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            UpdateStatusText(message);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void SetReadyStatusServerRpc(bool isReady, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        
        for (int i = 0; i < lobbyPlayers.Count; i++)
        {
            var player = lobbyPlayers[i];
            if (player.clientId == clientId)
            {
                player.isReady = isReady;
                lobbyPlayers[i] = player;
                Debug.Log($"Player {clientId} ready status: {isReady}");
                break;
            }
        }
        
        CheckIfGameCanStart();
    }
    
    private void CheckIfGameCanStart()
    {
        if (!IsServer || lobbyPlayers.Count < 2) return;
        
        int totalReady = 0;
        
        foreach (var player in lobbyPlayers)
        {
            if (player.isReady) totalReady++;
        }
        
        // Simple check: all players must be ready (no team balance requirement)
        bool canStart = totalReady == lobbyPlayers.Count && lobbyPlayers.Count >= 2;
        
        NotifyGameReadyStatusClientRpc(canStart);
    }
    
    [ClientRpc]
    private void NotifyGameReadyStatusClientRpc(bool canStart)
    {
        var uiManager = FindFirstObjectByType<LobbyUIManager>();
        if (uiManager != null)
        {
            uiManager.SetGameCanStart(canStart);
        }
    }
    
    private void OnLobbyPlayersChanged(NetworkListEvent<LobbyPlayerData> changeEvent)
    {
        // Update UI with current player list
        var playerList = new List<LobbyPlayerData>();
        foreach (var player in lobbyPlayers)
        {
            playerList.Add(player);
        }
        
        OnPlayerListUpdated?.Invoke(playerList);
        UpdatePlayerCount(lobbyPlayers.Count);
        
        Debug.Log($"Player list updated: {lobbyPlayers.Count} players");
    }
    
    private void UpdateStatusText(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
    
    private void UpdatePlayerCount(int count)
    {
        if (playerCountText != null)
            playerCountText.text = $"Oyuncular: {count}/{maxPlayers}";
    }
    
    private void SetUIForConnected()
    {
        hostButton.SetActive(false);
        joinButton.SetActive(false);
        disconnectButton.SetActive(true);
    }
    
    private void SetUIForDisconnected()
    {
        hostButton.SetActive(true);
        joinButton.SetActive(true);
        if (joinAsInspectorButton != null)
            joinAsInspectorButton.SetActive(true);
        disconnectButton.SetActive(false);
        isLocalInspector = false; // Reset inspector status
    }
    
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        
        lobbyPlayers?.Dispose();
        connectedPlayersCount?.Dispose();
    }
    
    // Public method to check if local player is inspector
    public bool IsLocalPlayerInspector()
    {
        return isLocalInspector;
    }
    
    // Public method to get player type for spawning
    public LobbyPlayerData? GetPlayerData(ulong clientId)
    {
        foreach (var player in lobbyPlayers)
        {
            if (player.clientId == clientId)
                return player;
        }
        return null;
    }
}