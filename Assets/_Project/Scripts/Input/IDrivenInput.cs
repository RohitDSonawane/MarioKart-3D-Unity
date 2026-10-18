namespace HarborRacer.Inputs
{
    /// <summary>
    /// Optional extension for inputs that compute their values each physics step (AI, autopilot).
    /// KartController calls Tick right before it reads the input, so the values always match the current kart state.
    /// </summary>
    public interface IDrivenInput
    {
        void Tick(float dt);
    }
}
