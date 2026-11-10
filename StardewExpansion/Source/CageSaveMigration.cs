using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PelicanCultivation
{
    // Old builds appended an extra output panel at every load/morning because
    // they failed to unwrap MachineryIntake. Native saves number graph nodes
    // breadth first, so those siblings shift nested dispenser/butcher slots.
    public static class CageSaveMigration
    {
        public const string Format="PELICAN_CAGE_STORAGE_FORMAT";
        private sealed class Scope
        {
            public Scope Previous;
            public readonly Dictionary<long,SaveItemNode> Items=new Dictionary<long,SaveItemNode>();
            public readonly Dictionary<int,SaveItemNode> Cages=new Dictionary<int,SaveItemNode>();
        }
        [ThreadStatic] private static Scope current;
        public static object Enter(List<SaveItemNode> items)
        {
            var scope=new Scope{Previous=current};
            if(items!=null) foreach(var item in items)
            {
                scope.Items[item.uuid]=item;
                if(item.identifier=="animal_cage")scope.Cages[item.uniqueId]=item;
            }
            current=scope;return scope;
        }
        public static void Exit(object state)
        {if(ReferenceEquals(current,state))current=current.Previous;}
        public static Dictionary<GraphNodeStorage,int> InventoryNumbers(GameItem cage)
        {
            var result=new Dictionary<GraphNodeStorage,int>();var queue=new Queue<GraphNodeStorage>(cage.children);
            while(queue.Count>0)
            {
                var node=queue.Dequeue();if(node is GameItem || result.ContainsKey(node))continue;
                result.Add(node,result.Count);foreach(var child in node.children)queue.Enqueue(child);
            }
            return result;
        }
        public static void Normalize(GameItem cage)
        {
            if(!ModEntry.Enabled || current==null || cage==null || cage.identifier!="animal_cage" || !current.Cages.TryGetValue(cage.uniqueId,out var saved))return;
            var numbers=InventoryNumbers(cage);var animals=AnimalCage.GetCageInventory(cage);
            var dispenser=AnimalCage.GetDispenserSlot(cage);var butcher=AnimalCage.GetButcherSlot(cage);var products=FarmHusbandry.Products(cage);
            int changed=0;
            for(int i=0;i<saved.childItems.Count && i<saved.childItemInventoryNode.Count;i++)
            {
                if(!current.Items.TryGetValue(saved.childItems[i],out var item))continue;
                GraphNodeStorage target=null;
                if(item.identifier=="livestock_feed_dispenser")target=dispenser;
                else if(item.identifier=="auto_butcher")target=butcher;
                else if(IsProduct(item.identifier) && animals!=null && numbers.TryGetValue(animals,out int animalNumber) && saved.childItemInventoryNode[i]!=animalNumber)target=products;
                if(target!=null && numbers.TryGetValue(target,out int number) && saved.childItemInventoryNode[i]!=number)
                {saved.childItemInventoryNode[i]=number;changed++;}
            }
            Crops.SetInt(cage,Format,1);
            if(changed>0)ModEntry.Note("Restored cage storage paths: cage="+cage.uniqueId+"; items="+changed);
        }
        public static bool IsProduct(string id)
            => id=="pelican_milk"||id=="pelican_truffle"||id=="pelican_truffle_small"||id=="pelican_egg"||id=="pelican_goat_milk";
    }
    [HarmonyPatch(typeof(SaveManager),nameof(SaveManager.DecodeNodes))]
    public static class CageSaveScopePatch
    {
        public static void Prefix(List<SaveItemNode> savedItems,out object __state)
        {__state=CageSaveMigration.Enter(savedItems);}
        public static Exception Finalizer(Exception __exception,object __state)
        {CageSaveMigration.Exit(__state);return __exception;}
    }
    [HarmonyPatch(typeof(StorageExpansionHelper),nameof(StorageExpansionHelper.RestoreTabs))]
    public static class CageSavePathsPatch
    {
        public static void Postfix(GameItem __0){CageSaveMigration.Normalize(__0);}
    }
}
