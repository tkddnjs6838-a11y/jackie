using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Only the retry control reacts to hover/focus. Works while the result pauses time.
public sealed class ResultButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Image surface;
    bool hover,selected;
    float amount;
    void Update()
    {
        var b=GetComponent<Button>();
        float target=b!=null && b.interactable && (hover||selected)?1:0;
        amount=Mathf.MoveTowards(amount,target,Time.unscaledDeltaTime*5);
        if(surface!=null)surface.color=Color.Lerp(new Color(.05f,.12f,.21f),new Color(.08f,.42f,.65f),Mathf.SmoothStep(0,1,amount));
    }
    public void OnPointerEnter(PointerEventData e){hover=true;}
    public void OnPointerExit(PointerEventData e){hover=false;if(EventSystem.current!=null && EventSystem.current.currentSelectedGameObject==gameObject)EventSystem.current.SetSelectedGameObject(null);}
    public void OnSelect(BaseEventData e){selected=true;}
    public void OnDeselect(BaseEventData e){selected=false;}
    void OnDisable(){hover=selected=false;amount=0;if(surface!=null)surface.color=new Color(.05f,.12f,.21f);}
}
