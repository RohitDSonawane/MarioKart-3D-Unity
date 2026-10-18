using HarborRacer.Kart;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>Adds the runtime behaviour component for a ZoneMarker kind (Phase 4). Shared by the scene builders.</summary>
    public static class ZoneBinder
    {
        public static void Bind(GameObject go, ZoneMarker marker)
        {
            switch (marker.kind)
            {
                case ZoneKind.JumpPanel:
                {
                    var z = go.AddComponent<JumpPanelZone>();
                    z.upForce = marker.Get("JumpPanelScript.upforce", z.upForce);
                    z.downForce = marker.Get("JumpPanelScript.downforce", z.downForce);
                    z.launchSpeed = marker.Get("JumpPanelScript.launch", z.launchSpeed);
                    break;
                }
                case ZoneKind.BoostPad: go.AddComponent<BoostPadZone>(); break;
                case ZoneKind.TrickZone: go.AddComponent<TrickZone>(); break;
                case ZoneKind.DownForce:
                {
                    var z = go.AddComponent<DownForceZone>();
                    z.force = marker.Get("colliderInAir.force", z.force);
                    break;
                }
                case ZoneKind.CancelDownForce: go.AddComponent<CancelDownForceZone>(); break;
                case ZoneKind.OutOfBounds: go.AddComponent<OutOfBoundsZone>().reason = RespawnReason.OutOfBounds; break;
                case ZoneKind.Water: go.AddComponent<OutOfBoundsZone>().reason = RespawnReason.Water; break;
                case ZoneKind.GliderRamp: go.AddComponent<GliderRampZone>(); break;
                case ZoneKind.GliderLaunch:
                {
                    var z = go.AddComponent<GliderLaunchZone>();
                    z.glideAngleZ = marker.Get("GetGlideAngle.glideAngle", 0f);
                    z.glideAngleX = marker.Get("GetGlideAngle.glideAngleX", 0f);
                    break;
                }
                case ZoneKind.AntiGravityEnter:
                {
                    var z = go.AddComponent<AntiGravityEnterZone>();
                    z.rotateCamera = marker.Get("CameraRotateAntigravity.rotateCam", 0f) > 0.5f;
                    z.cameraRotX = marker.Get("CameraRotateAntigravity.rotAmountX", 0f);
                    z.cameraRotZ = marker.Get("CameraRotateAntigravity.rotAmountZ", 0f);
                    z.canUseBullet = marker.Get("CameraRotateAntigravity.canUseBullet", 1f) > 0.5f;
                    break;
                }
                case ZoneKind.AntiGravityExit:
                {
                    var z = go.AddComponent<AntiGravityExitZone>();
                    z.levelOut = marker.Get("AntiGravityExitRotate.rotateX", 0f) > 0.5f
                                 || marker.Get("AntiGravityExitRotate.rotateY", 0f) > 0.5f
                                 || marker.Get("AntiGravityExitRotate.rotateZ", 0f) > 0.5f;
                    break;
                }
                // Checkpoint / LapLine: Phase 6. ItemBox / Crate: Phases 7-8.
            }
        }
    }
}
