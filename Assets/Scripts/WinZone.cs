using UnityEngine;

public class WinZone : MonoBehaviour
{
    public static event System.Action OnPlayerWon;

    private bool triggered;

    // Enter alone is not enough: the goal zone sits on the player's spawn point, so the
    // player can already be standing inside it when the boss dies. Stay covers that case.
    void OnTriggerEnter(Collider other)
    {
        TryWin(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryWin(other);
    }

    void TryWin(Collider other)
    {
        if (triggered || !other.CompareTag("Player")) return;
        if (!Enemy.BossDefeated) return;

        triggered = true;
        Debug.Log("===== YOU WIN — Boss defeated and reached the goal zone. Clear time " + Time.timeSinceLevelLoad.ToString("F1") + "s. =====");
        OnPlayerWon?.Invoke();
        Time.timeScale = 0f;
    }
}
