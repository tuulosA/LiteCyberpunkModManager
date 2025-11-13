using System;

namespace LiteCyberpunkModManager.Models
{
    public static class GameIdValues
    {
        public static GameId[] All => (GameId[])Enum.GetValues(typeof(GameId));
    }
}

