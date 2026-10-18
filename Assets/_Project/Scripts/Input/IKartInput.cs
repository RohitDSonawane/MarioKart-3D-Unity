namespace HarborRacer.Inputs
{
    /// <summary>
    /// Everything a kart needs from a driver, whether human (PlayerKartInput) or AI (AIKartInput, Phase 9).
    /// "Pressed" values are latched until EndFixedStep, so a tap between two physics steps is never missed.
    /// </summary>
    public interface IKartInput
    {
        float Throttle { get; }      // 0..1
        float Brake { get; }         // 0..1
        float Steer { get; }         // -1..1
        float Pitch { get; }         // -1..1 (glider: +1 = nose down)
        bool DriftHeld { get; }
        bool DriftPressed { get; }
        bool TrickPressed { get; }
        bool ItemPressed { get; }
        bool ItemBackPressed { get; }
        void EndFixedStep();         // clears latched "pressed" flags
    }
}
