using System.ComponentModel;
using System.Reflection;

namespace Retheme
{
    public interface IThemeService
    {

        Task SetSidebar(SidebarTheme? theme);
        event Action<SidebarTheme?>? OnSidebarChanged;

        Task SetApplication(ApplicationTheme? theme);
        event Action<ApplicationTheme?>? OnApplicationChanged;

        Task SetBackground(AnimationMode? theme);
        event Action<AnimationMode?>? OnBackgroundChanged;

    }


    public class ThemeService : IThemeService
    {

        public event Action<SidebarTheme?>? OnSidebarChanged;
        public async Task SetSidebar(SidebarTheme? theme)
        {
            OnSidebarChanged?.Invoke(theme);
        }

        public event Action<ApplicationTheme?>? OnApplicationChanged;

        public async Task SetApplication(ApplicationTheme? theme)
        {
            OnApplicationChanged?.Invoke(theme);
        }

        public event Action<AnimationMode?>? OnBackgroundChanged;

        public async Task SetBackground(AnimationMode? theme)
        {
            OnBackgroundChanged?.Invoke(theme);
        }
    }


    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum ApplicationTheme
    {
        [Description("Atrium Ivory (vs Default Purple)")]
        Ivory = 0,

        [Description("Dark Slate (vs Shale Life)")]
        DarkSlate = 13,

        [Description("Midnight Void (vs Daybreak)")]
        MidnightVoid = 14,

        [Description("Arctic Thaw (vs Desert Sunset)")]
        ArcticThaw = 15,

        [Description("Abyssal Trench (vs Ocean Ripple)")]
        AbyssalTrench = 16,

        [Description("Oxidized Iron (vs Silver Chrome)")]
        OxidizedIron = 17,

        [Description("Deep Sea Kelp (vs Coral Reef)")]
        DeepSeaKelp = 18,

        [Description("Solar Flare (vs Dusk)")]
        SolarFlare = 19,

        [Description("Monochrome Matrix (vs Rainbow)")]
        MonochromeMatrix = 20,

        [Description("Lead Fortress (vs Golden Gate)")]
        LeadFortress = 21,

        [Description("Ashen Wastes (vs Emerald Forest)")]
        AshenWastes = 22,

        [Description("Inverted Neon (vs Night Vision)")]
        InvertedNeon = 23,

        [Description("Sour Lime (vs Berry Blast)")]
        SourLime = 24,

        [Description("Obsidian Ledger (vs Parchment)")]
        ObsidianLedger = 25,

        [Description("Daybreak (Classic Sunrise)")]
        Daybreak = 1,

        [Description("Desert Sunset (Banded Gold)")]
        DesertSunset = 2,

        [Description("Ocean Ripple (Deep Reflective)")]
        OceanRipple = 3,

        [Description("Silver Chrome (Metallic)")]
        SilverChrome = 4,

        [Description("Coral Reef (Tropical Cyan)")]
        CoralReef = 5,

        [Description("Dusk (Atmospheric Night)")]
        Dusk = 6,

        [Description("Rainbow Peak (Full Spectrum)")]
        RainbowPeak = 7,

        [Description("Golden Gate (Polished Gold)")]
        GoldenGate = 8,

        [Description("Emerald Forest (Vibrant Green)")]
        EmeraldForest = 9,

        [Description("Night Vision (Matrix Green)")]
        NightVision = 10,

        [Description("Berry Blast (Candy Red/Blue)")]
        BerryBlast = 11,

        [Description("Parchment (Academic Beige)")]
        Parchment = 12
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum SidebarTheme
    {

        [Description("Default (Blazor Purple)")]
        Default = 0,

        [Description("Midnight Void (vs Daybreak)")]
        MidnightVoid = 14,

        [Description("Arctic Thaw (vs Desert Sunset)")]
        ArcticThaw = 15,

        [Description("Abyssal Trench (vs Ocean Ripple)")]
        AbyssalTrench = 16,

        [Description("Oxidized Iron (vs Silver Chrome)")]
        OxidizedIron = 17,

        [Description("Deep Sea Kelp (vs Coral Reef)")]
        DeepSeaKelp = 18,

        [Description("Solar Flare (vs Dusk)")]
        SolarFlare = 19,

        [Description("Monochrome Matrix (vs Rainbow)")]
        MonochromeMatrix = 20,

        [Description("Lead Fortress (vs Golden Gate)")]
        LeadFortress = 21,

        [Description("Ashen Wastes (vs Emerald Forest)")]
        AshenWastes = 22,

        [Description("Inverted Neon (vs Night Vision)")]
        InvertedNeon = 23,

        [Description("Sour Lime (vs Berry Blast)")]
        SourLime = 24,

        [Description("Obsidian Ledger (vs Parchment)")]
        ObsidianLedger = 25,

        [Description("Shale Life (Default Dark Mode)")]
        ShaleLife = 13,

        [Description("Daybreak (Classic Sunrise)")]
        Daybreak = 1,

        [Description("Desert Sunset (Banded Gold)")]
        DesertSunset = 2,

        [Description("Ocean Ripple (Deep Reflective)")]
        OceanRipple = 3,

        [Description("Silver Chrome (Metallic)")]
        SilverChrome = 4,

        [Description("Coral Reef (Tropical Cyan)")]
        CoralReef = 5,

        [Description("Dusk (Atmospheric Night)")]
        Dusk = 6,

        [Description("Rainbow Peak (Full Spectrum)")]
        RainbowPeak = 7,

        [Description("Golden Gate (Polished Gold)")]
        GoldenGate = 8,

        [Description("Emerald Forest (Vibrant Green)")]
        EmeraldForest = 9,

        [Description("Night Vision (Matrix Green)")]
        NightVision = 10,

        [Description("Berry Blast (Candy Red/Blue)")]
        BerryBlast = 11,

        [Description("Parchment (Academic Beige)")]
        Parchment = 12
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    public enum AnimationMode
    {
        [Description("None (Turn Off)")]
        none = 0,

        [Description("Gradient")]
        gradient = 1,

        [Description("Lichtenberg Cube")]
        lichtenberg = 2,

        [Description("Lichtenberg Light")]
        lichtenlight = 3,

        [Description("Lichten Birds")]
        lichtenbird = 4,

        [Description("Lichten Atrium")]
        lichtenatrium = 5
    }
}
