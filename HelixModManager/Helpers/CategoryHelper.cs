namespace LiteCyberpunkModManager.Helpers
{
    public static class CategoryHelper
    {
        public static string GetCategoryName(string gameDomain, int categoryId)
        {
            return gameDomain switch
            {
                "baldursgate3" => GetBg3CategoryName(categoryId),
                "cyberpunk2077" => GetCp2077CategoryName(categoryId),
                _ => GetCp2077CategoryName(categoryId) // default/backwards-compatible
            };
        }

        private static string GetCp2077CategoryName(int categoryId)
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

        private static string GetBg3CategoryName(int categoryId)
        {
            return categoryId switch
            {
                1 => "Baldur's Gate 3",
                2 => "Miscellaneous",
                3 => "Character Customisation",
                4 => "Visuals",
                5 => "Gameplay",
                6 => "User Interface",
                7 => "Utilities",
                9 => "Audio",
                10 => "Equipment",
                12 => "Classes",
                13 => "Spells",
                15 => "Races",
                16 => "Dice",
                17 => "Armor",
                18 => "Animations",
                19 => "Quests",
                20 => "Accessories",
                21 => "Companions",
                22 => "Weapons",
                23 => "Clothing",
                24 => "Resources",
                25 => "Maps",
                26 => "Photo Mode",
                _ => "Unknown"
            };
        }
    }
}
