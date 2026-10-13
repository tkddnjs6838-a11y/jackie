using UnityEngine;
using UnityEngine.UI;

// Stable integration contract. Visuals belong to the prefab; gameplay belongs to GameFlowManager.
public sealed class StartScreenView : MonoBehaviour
{
    public Button startButton;
    public GameObject testModeNotice;
    public void ShowTestMode(bool enabled)
    {
        if(testModeNotice!=null)testModeNotice.SetActive(enabled);
    }
}
