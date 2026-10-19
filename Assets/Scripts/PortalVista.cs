using UnityEngine;

// Baked from the real destination scene: no second gameplay scene or camera at runtime.
public sealed class PortalVista : ScriptableObject
{
    public Cubemap panorama;
    public Vector3 forward=Vector3.forward;
}
