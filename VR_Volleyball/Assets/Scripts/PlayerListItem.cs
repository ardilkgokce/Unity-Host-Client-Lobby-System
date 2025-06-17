using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerListItem : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI readyStatusText;
    public Image backgroundImage;
    public Image hostIcon;
    
    private ulong playerId;
    private bool isLocalPlayer;
    private bool currentReadyStatus;
    
    public void SetPlayerInfo(ulong id, string playerName, bool isReady, bool isHost, bool isLocal)
    {
        playerId = id;
        isLocalPlayer = isLocal;
        currentReadyStatus = isReady;
        
        if (playerNameText != null)
            playerNameText.text = playerName;
            
        UpdateReadyStatus(isReady);
            
        if (hostIcon != null)
            hostIcon.gameObject.SetActive(isHost);
        
        Debug.Log($"PlayerListItem created: {playerName} (ID: {id}, Local: {isLocal}, Ready: {isReady})");
    }
    
    public void UpdateReadyStatus(bool isReady)
    {
        currentReadyStatus = isReady;
        
        if (readyStatusText != null)
            readyStatusText.text = isReady ? "Hazır" : "Hazır Değil";
            
        // Renk güncelleme
        if (backgroundImage != null)
        {
            Color backgroundColor;
            if (isLocalPlayer)
            {
                // Local player için mavi ton
                backgroundColor = isReady ? new Color(0.2f, 0.6f, 0.8f, 0.6f) : new Color(0.4f, 0.4f, 0.6f, 0.4f);
            }
            else
            {
                // Diğer oyuncular için yeşil/kırmızı
                backgroundColor = isReady ? new Color(0.2f, 0.8f, 0.2f, 0.4f) : new Color(0.8f, 0.2f, 0.2f, 0.4f);
            }
            backgroundImage.color = backgroundColor;
        }
        
        Debug.Log($"PlayerListItem {playerId} ready status updated to: {isReady}");
    }
    
    public ulong GetPlayerId()
    {
        return playerId;
    }
    
    public bool IsReady()
    {
        return currentReadyStatus;
    }
}
