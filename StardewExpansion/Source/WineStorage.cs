namespace PelicanCultivation
{
    public static class WineStorage
    {
        public const string Shape="00000000"+"00000000"+"00000011";
        public const int Width=8,Capacity=22;
        public static GameGridInventory IngredientGrid(GameItem bottle)
            => MachineHelper.GetBodyStorage(bottle,0) as GameGridInventory;
        public static void Expand(GameItem bottle)
        {
            if(bottle==null || !bottle.IsTag(WineHelper.WINE_CONTAINER_TAG) || bottle.GetTagReadonly(WineHelper.WINE_CONTAINER_STATE).GetInt()!=0) return;
            var row=bottle.contentWindow?.childElement as MachineryPanelRow;
            var grid=IngredientGrid(bottle);
            // Extend the actual native grid, retaining the start button, its
            // captured reference, ownership filters and saved ingredient slots.
            if(grid!=null && grid.inventoryShape.width==6 && grid.inventoryShape.height==2)
            {grid.SetShape(Shape,Width);row?.Validate();}
        }
    }
}
