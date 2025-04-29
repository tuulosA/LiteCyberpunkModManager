namespace LiteCyberpunkModManager.Helpers
{
    public static class CategoryHelper
    {
        public static string GetCategoryName(int categoryId)
        {
            return categoryId switch
            {
                2 => "Miscellaneous",
                3 => "Armour and Clothing",
                4 => "Audio",
                5 => "Characters",
                6 => "Crafting",
                7 => "Gameplay",
                8 => "User Interface",
                9 => "Utilities",
                10 => "Visuals and Graphics",
                11 => "Weapons",
                12 => "Modders Resources",
                13 => "Appearance",
                14 => "Vehicles",
                15 => "Animations",
                16 => "Locations",
                17 => "Scripts",
                _ => "Unknown"
            };
        }
    }
}