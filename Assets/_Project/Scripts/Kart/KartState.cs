namespace HarborRacer.Kart
{
    /// <summary>Plain state bag owned by KartController. Other kart components read it; each field has one writer.</summary>
    public class KartState
    {
        public bool Grounded;          // ground probe hit or recent contact
        public bool OnDirt;            // standing on an OffRoad-layer surface
        public bool Reversing;
        // set by later phases (glider, jump panel, hits); read by drift/boost
        public bool Gliding;
        public bool OnJumpPanel;
        public bool Stunned;
        public bool AntiGravity;
        public bool Bullet;
        public bool Star;              // invincible (Phase 8)
        public bool CancelDownForce;   // inside a CancelDownForceZone
        public bool TrickAvailable;    // inside a TrickZone
        public bool TrickPending;      // trick done, boost pays out on landing // moving backwards under brake
        public float GroundDistance;   // probe distance (valid when GroundHit)
        public bool GroundHit;
        public UnityEngine.Vector3 GroundNormal = UnityEngine.Vector3.up;
    }
}
