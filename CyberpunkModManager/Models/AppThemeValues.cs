using System;
using System.Collections.Generic;

namespace CyberpunkModManager.Models
{
    public static class AppThemeValues
    {
        public static AppTheme[] All => (AppTheme[])Enum.GetValues(typeof(AppTheme));

    }
}
