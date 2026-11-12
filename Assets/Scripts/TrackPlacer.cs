using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Procura uma superfície horizontal no centro da tela e mostra um retículo onde a esteira será colocada.
public class TrackPlacer : MonoBehaviour
{
    public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;
    public Camera arCamera;
    public GameObject reticlePrefab;

    public bool HasPose { get; private set; }
    public Pose CurrentPose { get; private set; }

    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    GameObject reticle;
    bool active;

    public void Begin()
    {
        active = true;
        HasPose = false;
        if (reticle == null && reticlePrefab != null) reticle = Instantiate(reticlePrefab);
        if (reticle != null) reticle.SetActive(false);
    }

    public void End()
    {
        active = false;
        HasPose = false;
        if (reticle != null)
        {
            Destroy(reticle);
            reticle = null;
        }
    }

    void Update()
    {
        if (!active || raycastManager == null) return;

        HasPose = false;
        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (raycastManager.Raycast(center, hits, TrackableType.PlaneWithinPolygon))
        {
            foreach (var h in hits)
            {
                if (planeManager != null)
                {
                    var plane = planeManager.GetPlane(h.trackableId);
                    if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp) continue;
                }
                CurrentPose = h.pose;
                HasPose = true;
                break;
            }
        }

        if (reticle != null)
        {
            reticle.SetActive(HasPose);
            if (HasPose) reticle.transform.SetPositionAndRotation(CurrentPose.position, CurrentPose.rotation);
        }
    }
}
