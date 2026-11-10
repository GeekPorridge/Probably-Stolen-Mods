using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using PelicanCultivation;

public static class HeadlessRunner
{
    private static string managedDirectory,modDll,engineFixture;
    private static bool cropOnly;
    public static int Main(string[] args)
    {
        Environment.SetEnvironmentVariable("MONOMOD_DMDType","cecil");
        managedDirectory=Path.Combine(args[0],"Probably Stolen_Data/Managed");modDll=args[1];
        cropOnly=args.Length>2 && args[2]=="crop-only";
        engineFixture=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"EngineFixture");
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,eventArgs)=>{
            string name=new AssemblyName(eventArgs.Name).Name;
            string path=name=="PelicanCultivation"?modDll:Path.Combine(managedDirectory,name+".dll");
            string fixture=Path.Combine(engineFixture,name+".dll");if(File.Exists(fixture))path=fixture;
            return File.Exists(path)?Assembly.LoadFrom(path):null;
        };
        return Run();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run()
    {
        var harmony=new Harmony("pelican_cultivation.headless_verification");
        try
        {
            // Unity's StringDatabase needs the engine. Replace text lookup only;
            // the native fridge feature, callbacks and value math run intact.
            harmony.Patch(typeof(LocHelper).GetMethod("GetLocalizedMechanic"),
                new HarmonyMethod(typeof(PelicanVerification).GetMethod("MechanicTextFixture")));
            harmony.Patch(typeof(ModEntry).GetMethod("L"),new HarmonyMethod(typeof(FarmVerification).GetMethod("TextFixture")));
            Console.WriteLine(FarmVerification.ValidatePatchBindings());
            foreach(var type in typeof(ModEntry).Assembly.GetTypes())
                if(type.IsDefined(typeof(HarmonyPatch),false) && type!=typeof(NightReportScrollPatch)) harmony.CreateClassProcessor(type).Patch();
            Console.WriteLine("Night-report scroll patch: native target/parameters verified; engine rendering is excluded from the CLR harness.");
            if(cropOnly){Console.WriteLine(CropUpdateVerification.Run());return 0;}
            Console.WriteLine(PelicanVerification.RunStorageIntegration());
            Console.WriteLine(PelicanVerification.RunScheduleIntegration());
            Console.WriteLine(FarmVerification.Run());
            Console.WriteLine(FarmVerification.RunPatchedIntegration());
            Console.WriteLine(CompatibilityVerification.Run());
            Console.WriteLine(TownDialogueVerification.Run());
            Console.WriteLine(NightReportVerification.Run(Path.Combine(Path.GetDirectoryName(modDll),"Localization")));
            harmony.Unpatch(typeof(LocHelper).GetMethod("GetLocalizedMechanic"),typeof(PelicanVerification).GetMethod("MechanicTextFixture"));
            Console.WriteLine(CropUpdateVerification.Run());
            Console.WriteLine(V2Verification.Run(Path.Combine(Path.GetDirectoryName(modDll),"Localization"),Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)));
            Console.WriteLine(ManualStockVerification.Run());
            Console.WriteLine(BaitGeometryVerification.Run());
            Console.WriteLine(ReservoirAccessVerification.Run());
            Console.WriteLine(ReservoirEventVerification.Run());
            return 0;
        }
        catch(Exception error) {Console.WriteLine(error);return 1;}
        finally {harmony.UnpatchAll(harmony.Id);}
    }
}
