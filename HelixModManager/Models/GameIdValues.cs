using System;

namespace HelixModManager.Models
{
    public static class GameIdValues
    {
        public static GameId[] All => (GameId[])Enum.GetValues(typeof(GameId));
    }
}


