using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// Visual feedback only; TouchControl and Button still own all input actions.
public sealed class UiPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    Vector3 rest;bool pressed;Outline glow;Color idle;
    void Awake(){rest=transform.localScale;glow=GetComponent<Outline>();if(glow!=null)idle=glow.effectColor;}
    void Update()
    {
        transform.localScale=Vector3.Lerp(transform.localScale,rest*(pressed?.94f:1f),1-Mathf.Exp(-24*Time.unscaledDeltaTime));
        
    }
    public void OnPointerDown(PointerEventData e){pressed=true;if(glow!=null){idle=glow.effectColor;glow.effectColor=new Color(1,1,1,.8f);}}
    public void OnPointerUp(PointerEventData e){pressed=false;if(glow!=null)glow.effectColor=idle;}
    public void OnPointerExit(PointerEventData e){pressed=false;if(glow!=null)glow.effectColor=idle;}
    void OnDisable(){pressed=false;transform.localScale=rest;}
}
