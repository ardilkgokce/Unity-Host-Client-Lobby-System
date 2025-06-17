using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System.Collections.Generic;

public class LobbyUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    
    [Header("Team UI")]
    public Transform teamAListParent;
    public Transform teamBListParent;
    public Transform inspectorListParent; // NEW: Inspector list
    public GameObject playerListItemPrefab;
    public Button joinTeamAButton;
    public Button joinTeamBButton;
    public TextMeshProUGUI teamAHeaderText;
    public TextMeshProUGUI teamBHeaderText;
    public TextMeshProUGUI inspectorHeaderText; // NEW: Inspector header
    
    [Header("Lobby Controls")]
    public Button readyButton;
    public Button startGameButton;
    public TextMeshProUGUI readyButtonText;
    public TextMeshProUGUI statusMessageText;
    
    private VRLobbyManager lobbyManager;
    private Dictionary<ulong, PlayerListItem> playerItems = new Dictionary<ulong, PlayerListItem>();
    private bool isLocalPlayerReady = false;
    private int localPlayerTeam = -1;
    
    private void Start()
    {
        lobbyManager = FindFirstObjectByType<VRLobbyManager>();
        
        if (lobbyManager != null)
        {
            lobbyManager.OnPlayerListUpdated += UpdatePlayerList;
            lobbyManager.OnConnectionStatusChanged += OnConnectionChanged;
        }
        
        // Setup button listeners
        if (readyButton != null)
            readyButton.onClick.AddListener(ToggleReady);
            
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(StartGame);
            startGameButton.gameObject.SetActive(false);
        }
        
        if (joinTeamAButton != null)
            joinTeamAButton.onClick.AddListener(() => RequestTeamChange(0));
            
        if (joinTeamBButton != null)
            joinTeamBButton.onClick.AddListener(() => RequestTeamChange(1));
        
        ShowMainMenu();
    }
    
    private void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        ClearPlayerLists();
    }
    
    private void ShowLobby()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        
        // Show start button only for host
        if (startGameButton != null)
            startGameButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);
    }
    
    private void OnConnectionChanged(bool connected)
    {
        if (connected)
        {
            ShowLobby();
        }
        else
        {
            ShowMainMenu();
            isLocalPlayerReady = false;
            localPlayerTeam = -1;
        }
    }
    
    private void UpdatePlayerList(List<LobbyPlayerData> players)
    {
        // Clear existing items
        ClearPlayerLists();
        
        int teamACount = 0;
        int teamBCount = 0;
        int inspectorCount = 0;
        
        foreach (var playerData in players)
        {
            CreatePlayerListItem(playerData);
            
            if (playerData.isInspector)
            {
                inspectorCount++;
            }
            else if (playerData.teamId == 0)
            {
                teamACount++;
            }
            else if (playerData.teamId == 1)
            {
                teamBCount++;
            }
            
            // Track local player state
            if (playerData.clientId == NetworkManager.Singleton.LocalClientId)
            {
                isLocalPlayerReady = playerData.isReady;
                localPlayerTeam = playerData.teamId;
                UpdateReadyButton();
                UpdateTeamButtons(playerData.isInspector);
            }
        }
        
        // Update team headers
        if (teamAHeaderText != null)
            teamAHeaderText.text = $"Team A ({teamACount})";
            
        if (teamBHeaderText != null)
            teamBHeaderText.text = $"Team B ({teamBCount})";
            
        if (inspectorHeaderText != null)
            inspectorHeaderText.text = $"Inspectors ({inspectorCount})";
    }
    
    private void CreatePlayerListItem(LobbyPlayerData playerData)
    {
        if (playerListItemPrefab == null) return;
        
        Transform parent;
        if (playerData.isInspector)
        {
            parent = inspectorListParent;
        }
        else
        {
            parent = playerData.teamId == 0 ? teamAListParent : teamBListParent;
        }
        
        if (parent == null) return;
        
        GameObject itemObj = Instantiate(playerListItemPrefab, parent);
        PlayerListItem item = itemObj.GetComponent<PlayerListItem>();
        
        if (item != null)
        {
            bool isLocalPlayer = playerData.clientId == NetworkManager.Singleton.LocalClientId;
            bool isHost = playerData.playerName.ToString().Contains("(Host)");
            
            item.SetPlayerInfo(
                playerData.clientId,
                playerData.playerName.ToString(),
                playerData.isReady,
                isHost,
                isLocalPlayer
            );
            
            playerItems[playerData.clientId] = item;
        }
    }
    
    private void ClearPlayerLists()
    {
        foreach (var kvp in playerItems)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
                Destroy(kvp.Value.gameObject);
        }
        playerItems.Clear();
    }
    
    private void RequestTeamChange(int newTeam)
    {
        if (lobbyManager == null || !NetworkManager.Singleton.IsClient)
            return;
            
        if (newTeam == localPlayerTeam)
        {
            ShowStatusMessage("Zaten bu takımdasınız!");
            return;
        }
        
        lobbyManager.ChangeTeamServerRpc(newTeam);
    }
    
    private void ToggleReady()
    {
        if (lobbyManager == null || !NetworkManager.Singleton.IsClient)
            return;
            
        isLocalPlayerReady = !isLocalPlayerReady;
        lobbyManager.SetReadyStatusServerRpc(isLocalPlayerReady);
        UpdateReadyButton();
    }
    
    private void UpdateReadyButton()
    {
        if (readyButtonText != null)
        {
            readyButtonText.text = isLocalPlayerReady ? "Hazır Değil" : "Hazır";
        }
        
        if (readyButton != null)
        {
            var colors = readyButton.colors;
            colors.normalColor = isLocalPlayerReady ? Color.green : Color.white;
            readyButton.colors = colors;
        }
    }
    
    private void UpdateTeamButtons(bool isInspector = false)
    {
        // Hide team buttons for inspectors
        if (joinTeamAButton != null)
            joinTeamAButton.gameObject.SetActive(!isInspector);
            
        if (joinTeamBButton != null)
            joinTeamBButton.gameObject.SetActive(!isInspector);
            
        if (isInspector)
            return;
            
        // Highlight current team button
        if (joinTeamAButton != null)
        {
            var colorsA = joinTeamAButton.colors;
            colorsA.normalColor = (localPlayerTeam == 0) ? new Color(0.3f, 0.7f, 1f) : Color.white;
            joinTeamAButton.colors = colorsA;
        }
        
        if (joinTeamBButton != null)
        {
            var colorsB = joinTeamBButton.colors;
            colorsB.normalColor = (localPlayerTeam == 1) ? new Color(1f, 0.7f, 0.3f) : Color.white;
            joinTeamBButton.colors = colorsB;
        }
    }
    
    public void SetGameCanStart(bool canStart)
    {
        if (startGameButton != null && NetworkManager.Singleton.IsHost)
        {
            startGameButton.interactable = canStart;
            
            var colors = startGameButton.colors;
            colors.normalColor = canStart ? Color.green : Color.gray;
            startGameButton.colors = colors;
        }
        
        ShowStatusMessage(canStart ? "Tüm oyuncular hazır!" : "Oyuncular bekleniyor...");
    }
    
    private void StartGame()
    {
        if (NetworkManager.Singleton.IsHost)
        {
            ShowStatusMessage("Oyun başlatılıyor...");
            // Add your game start logic here
            // NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }
    }
    
    private void ShowStatusMessage(string message)
    {
        if (statusMessageText != null)
        {
            statusMessageText.text = message;
        }
    }
    
    private void OnDestroy()
    {
        if (lobbyManager != null)
        {
            lobbyManager.OnPlayerListUpdated -= UpdatePlayerList;
            lobbyManager.OnConnectionStatusChanged -= OnConnectionChanged;
        }
    }
}