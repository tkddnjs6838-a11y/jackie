using UnityEngine;

public sealed class UiSafePanel : MonoBehaviour
{
    RectTransform rect;Canvas canvas;Vector2 origin;Rect lastSafe;int width,height;
    void Start(){rect=(RectTransform)transform;canvas=GetComponentInParent<Canvas>();origin=rect.anchoredPosition;Apply();}
    void LateUpdate(){if(Screen.safeArea!=lastSafe || Screen.width!=width || Screen.height!=height)Apply();}
    void Apply()
    {
        if(rect==null || canvas==null)return;
        width=Screen.width;height=Screen.height;lastSafe=Screen.safeArea;
        float scale=Mathf.Max(.01f,canvas.scaleFactor);
        var a=rect.anchorMin;var offset=Vector2.zero;
        if(a.x==0)offset.x=lastSafe.xMin/scale;
        else if(a.x==1)offset.x=-(width-lastSafe.xMax)/scale;
        if(a.y==0)offset.y=lastSafe.yMin/scale;
        else if(a.y==1)offset.y=-(height-lastSafe.yMax)/scale;
        rect.anchoredPosition=origin+offset;
    }
}
