using System;
using System.Runtime.CompilerServices;

namespace PelicanCultivation
{
    public static class WineTransferProtection
    {
        private sealed class Hooks
        {
            public Func<GameItem,GameItem,bool> May,Can;
            public Action<GameItem,GameItem> Action;
        }
        private static readonly ConditionalWeakTable<GameItem,Hooks> hooks=new ConditionalWeakTable<GameItem,Hooks>();
        public static bool IsProtectedWine(GameItem item)
        {
            if(item==null || !item.IsTag(Rules.WineTag)) return false;
            int kind=item.GetTagReadonly(Rules.WineTag).GetInt();
            return kind>0 && kind<Rules.WineKeys.Length && WineHelper.IsFinishedWine(item);
        }
        public static bool Blocks(GameItem source,GameItem target)
            => ModEntry.Enabled && target!=null && IsProtectedWine(source) && target.IsTag("LIQUID_CONTAINER_TAG") && !WaterHelper.IsDrain(target);
        public static void Attach(GameItem bottle)
        {
            if(bottle==null) return;
            var guard=hooks.GetValue(bottle,item=>new Hooks());
            if(bottle.mayThisTargetItemFunc!=guard.May)
            {
                var native=bottle.mayThisTargetItemFunc;
                guard.May=(source,target)=>!Blocks(source,target) && (native?.Invoke(source,target)??false);
                bottle.mayThisTargetItemFunc=guard.May;
            }
            if(bottle.canThisTargetItemFunc!=guard.Can)
            {
                var native=bottle.canThisTargetItemFunc;
                guard.Can=(source,target)=>!Blocks(source,target) && (native?.Invoke(source,target)??false);
                bottle.canThisTargetItemFunc=guard.Can;
            }
            if(bottle.onThisTargetItemFunc!=guard.Action)
            {
                var native=bottle.onThisTargetItemFunc;
                // Guard the whole native callback, including its final bottle
                // reset. Blocking PourWineIntoContainer alone would lose wine.
                guard.Action=(source,target)=>{if(!Blocks(source,target)) native?.Invoke(source,target);};
                bottle.onThisTargetItemFunc=guard.Action;
            }
        }
    }
}
