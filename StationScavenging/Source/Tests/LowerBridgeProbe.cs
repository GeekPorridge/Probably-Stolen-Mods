public sealed class LowerBridgeProbe : AfterhourLocation
{
    public int Arrivals, Leaves, Ticks, Panels;
    public override string Id { get { return "dumping_ground"; } }
    public override string Source { get { return "bridge_probe"; } }
    public override string ImageKey { get { return "native_image_probe"; } }
    public override string BackgroundKey { get { return "native_background_probe"; } }
    public override string AmbienceId { get { return "native_audio_probe"; } }
    public override bool AllowsHazardousWaste { get { return true; } }
    public override bool CountsDumpedWaste { get { return true; } }
    public override int MaxConcurrentEvents { get { return 7; } }
    public override string GetTitle() { return "Original"; }
    public override bool IsDiscovered() { return true; }
    public override bool IsAvailable() { return false; }
    public override string GetLeaveWarning() { return "native_leave_warning"; }
    public override void Arrive() { Arrivals++; ScavHelper.ResetScavenging(); }
    public override void Leave() { Leaves++; }
    public override void Tick() { Ticks++; }
    public override void BuildPanel(CustomUIBuilder builder) { Panels++; }
}
