using System;
using HarmonyLib;
using UnityEngine;

namespace PelicanCultivation
{
    public static class ReservoirStage
    {
        private static SpriteRenderer fitted;
        private static Vector3 position,scale;
        public static void Restore(){if(fitted!=null){fitted.transform.position=position;fitted.transform.localScale=scale;}fitted=null;}
        public static float CoverScale(float spriteWidth,float spriteHeight,float viewWidth,float viewHeight)
            => Math.Max(viewWidth/Math.Max(0.001f,spriteWidth),viewHeight/Math.Max(0.001f,spriteHeight));
        public static void Fit(MapUIManager map,AfterhourLocation location)
        {
            if(!ModEntry.Enabled||location?.Id!=Fishing.LocationId)return;
            var stage=AccessTools.Field(typeof(MapUIManager),"stage").GetValue(map) as SpriteRenderer;
            var camera=Camera.main;if(stage==null||stage.sprite==null||camera==null||!camera.orthographic)return;
            fitted=stage;position=stage.transform.position;scale=stage.transform.localScale;
            var sprite=stage.sprite;float factor=CoverScale(sprite.rect.width/sprite.pixelsPerUnit,sprite.rect.height/sprite.pixelsPerUnit,camera.orthographicSize*2*camera.aspect,camera.orthographicSize*2);
            stage.transform.localScale=new Vector3(factor,factor,1);stage.transform.position=new Vector3(camera.transform.position.x,camera.transform.position.y,0);
        }
    }
    [HarmonyPatch(typeof(MapUIManager),"ShowStage")]
    public static class ReservoirStagePatch
    {public static void Prefix(){ReservoirStage.Restore();}public static void Postfix(MapUIManager __instance,AfterhourLocation location){ReservoirStage.Fit(__instance,location);}}
    [HarmonyPatch(typeof(MapUIManager),"OpenLocationPanel")]
    public static class ReservoirPanelPatch
    {public static void Postfix(AfterhourLocation location){if(ModEntry.Enabled&&location?.Id==Fishing.LocationId)Fishing.FinalizePanel();}}
}
