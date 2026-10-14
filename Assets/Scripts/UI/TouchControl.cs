using System;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class TouchControl : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler
{
    public bool Stick;
    public Action<Vector2> OnStick,OnDragValue;
    public Action<bool> OnHeld;
    public Action OnPress;
    int? pointer;
    public void OnPointerDown(PointerEventData e)
    {
        if(pointer.HasValue)return;pointer=e.pointerId;OnHeld?.Invoke(true);OnPress?.Invoke();if(Stick)SetStick(e);
    }
    public void OnDrag(PointerEventData e)
    {
        if(pointer!=e.pointerId)return;if(Stick)SetStick(e);else OnDragValue?.Invoke(e.delta);
    }
    void SetStick(PointerEventData e)
    {
        var r=(RectTransform)transform;
        if(RectTransformUtility.ScreenPointToLocalPointInRectangle(r,e.position,e.pressEventCamera,out var p))OnStick?.Invoke(Vector2.ClampMagnitude(p/(r.rect.width*.4f),1));
    }
    public void OnPointerUp(PointerEventData e){if(pointer==e.pointerId)Release();}
    public void Release(){pointer=null;OnHeld?.Invoke(false);if(Stick)OnStick?.Invoke(Vector2.zero);}
    void OnDisable()=>Release();
}
