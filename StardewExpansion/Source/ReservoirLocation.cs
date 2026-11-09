using System.Collections.Generic;

namespace PelicanCultivation
{
    public sealed class ReservoirLocation:AfterhourLocation
    {
        public override string Id=>Fishing.LocationId;
        public override string Source=>ModEntry.ModId;
        public override string ImageKey=>ModEntry.ModId+":reservoir_card";
        public override string BackgroundKey=>ModEntry.ModId+":reservoir_scene";
        public override string AmbienceId=>"dumping_ground";
        // Keep native State, ActiveEvents, GetModifier and closure handling so
        // a one-night reservoir event uses the game's normal saved lifecycle.
        public override int MaxConcurrentEvents=>1;
        public override string GetTitle()=>Expansion.L("reservoir_title");
        public override bool IsDiscovered()=>FarmStock.Aquaculture;
        public override bool IsAvailable()=>IsDiscovered()&&ReservoirAccess.HasBaitForDeparture()&&base.IsAvailable();
        public override void Arrive()=>Fishing.Arrive();
        public override void Leave()=>Fishing.Leave();
        public override void BuildPanel(CustomUIBuilder builder)=>Fishing.Build(builder);
        public static void Register()
        {
            ModHelper.AddAfterhourLocation(new ReservoirLocation());
            PlaceAfterDoctor(AfterhourLocation.GetAll());
        }
        public static void PlaceAfterDoctor(List<AfterhourLocation> locations)
        {
            var reservoir=locations.Find(location=>location?.Id==Fishing.LocationId);if(reservoir==null)return;
            locations.Remove(reservoir);
            int doctor=locations.FindIndex(location=>location?.Id==AfterhourLocation.DR_JACKSON);
            locations.Insert(doctor>=0?doctor+1:locations.Count,reservoir);
        }
    }
}
