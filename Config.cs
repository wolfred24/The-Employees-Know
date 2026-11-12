using MelonLoader;

namespace SmartStorage;

internal static class Config
{
    internal static MelonPreferences_Category Category;

    internal static MelonPreferences_Entry<bool> Enabled;
    internal static MelonPreferences_Entry<bool> DefaultStorageAccess;
    internal static MelonPreferences_Entry<bool> EnableBotanist;
    internal static MelonPreferences_Entry<bool> EnableChemist;
    internal static MelonPreferences_Entry<bool> RecipeAwareChemistry;
    internal static MelonPreferences_Entry<bool> AutoFilterChemistrySlots;
    internal static MelonPreferences_Entry<bool> ChemistryRequirePositiveFilter;
    internal static MelonPreferences_Entry<float> ChemistryFilterRefreshSeconds;
    internal static MelonPreferences_Entry<bool> EnablePackager;
    internal static MelonPreferences_Entry<string> PackagingProductAutoFillMode;
    internal static MelonPreferences_Entry<string> BrickPressProductAutoFillMode;
    internal static MelonPreferences_Entry<bool> PreferVanillaSupply;
    internal static MelonPreferences_Entry<bool> ChainAwareMixers;
    internal static MelonPreferences_Entry<bool> AutoFillLabOvens;
    internal static MelonPreferences_Entry<bool> LabOvenCookableOnly;
    internal static MelonPreferences_Entry<bool> AllowDeliveryVehicleAccess;
    internal static MelonPreferences_Entry<bool> PreferDeliveryVehicles;
    internal static MelonPreferences_Entry<bool> EnableChainMover;
    internal static MelonPreferences_Entry<float> ChainMoveDelaySeconds;
    internal static MelonPreferences_Entry<float> SearchIntervalSeconds;
    internal static MelonPreferences_Entry<string> Language;
    internal static MelonPreferences_Entry<bool> DebugLogging;

    internal static void Init()
    {
        Category = MelonPreferences.CreateCategory("SmartStorage", "The Employees Know");

        Enabled = Category.CreateEntry(
            "Enabled", true,
            "Enabled",
            "Turn the whole mod on or off. Employee behaviour is unchanged when this is off.");

        DefaultStorageAccess = Category.CreateEntry(
            "DefaultStorageAccess", true,
            "New storages are accessible",
            "When on, every storage starts available to employees. Turn a storage off with the button in its storage menu.");

        EnableBotanist = Category.CreateEntry(
            "EnableBotanist", true,
            "Botanists use enabled storages",
            "Botanists may take soil, seeds, additives and spawn from enabled storages on their property.");

        EnableChemist = Category.CreateEntry(
            "EnableChemist", true,
            "Chemists fetch from enabled storages",
            "Chemists fetch ingredients for their assigned stations from enabled storages on their property.");

        RecipeAwareChemistry = Category.CreateEntry(
            "RecipeAwareChemistry", true,
            "Recipe-aware chemistry stations",
            "Chemists only fetch the ingredients required by the recipe selected on each chemistry station.");

        AutoFilterChemistrySlots = Category.CreateEntry(
            "AutoFilterChemistrySlots", true,
            "Apply recipe filters to ingredient slots",
            "When a recipe is selected, The Employees Know sets the three ingredient slot filters from that recipe. Removing a filter by hand leaves it removed until you change the recipe.");

        ChemistryRequirePositiveFilter = Category.CreateEntry(
            "ChemistryRequirePositiveFilter", true,
            "Only fill filtered ingredient slots",
            "A chemist only auto-fills an ingredient slot that has a positive whitelist. Slots without a filter are left alone.");

        ChemistryFilterRefreshSeconds = Category.CreateEntry(
            "ChemistryFilterRefreshSeconds", 1.0f,
            "Chemistry recipe scan interval (seconds)",
            "How often The Employees Know looks for chemistry stations whose recipe changed. Applied recipes are remembered per save.");

        EnablePackager = Category.CreateEntry(
            "EnablePackager", true,
            "Handlers fetch from enabled storages",
            "Packagers fetch inputs for their assigned packaging stations and brick presses from enabled storages.");

        PackagingProductAutoFillMode = Category.CreateEntry(
            "PackagingProductAutoFillMode", "FilteredOnly",
            "Packaging product auto-fill",
            "Off, FilteredOnly or Any. FilteredOnly lets handlers fill a packaging station's product slot only when that slot has a whitelist filter. Without a filter there is no automatic fill.");

        BrickPressProductAutoFillMode = Category.CreateEntry(
            "BrickPressProductAutoFillMode", "FilteredOnly",
            "Brick press product auto-fill",
            "Off, FilteredOnly or Any. FilteredOnly lets handlers fill brick press product slots only when they have a whitelist filter.");

        PreferVanillaSupply = Category.CreateEntry(
            "PreferVanillaSupply", true,
            "Prefer the vanilla supply storage",
            "If a botanist already has a supply storage selected and it contains the needed item, keep using it before searching other storages.");

        ChainAwareMixers = Category.CreateEntry(
            "ChainAwareMixers", true,
            "Chain-aware mixing stations",
            "On: only the first mixer of a chain (one that is not another mixer's destination) may receive product from storage. Later mixers wait for the previous mixer's output. Off: old behaviour, every mixer input may be filled.");

        AutoFillLabOvens = Category.CreateEntry(
            "AutoFillLabOvens", true,
            "Chemists fill lab ovens",
            "Allow chemists to fetch ingredients for assigned lab ovens.");

        LabOvenCookableOnly = Category.CreateEntry(
            "LabOvenCookableOnly", true,
            "Lab ovens only accept cookable items",
            "The chemist only brings items that have a CookableModule, so it does not waste trips on things the oven cannot process.");

        AllowDeliveryVehicleAccess = Category.CreateEntry(
            "AllowDeliveryVehicleAccess", true,
            "Employees can use delivery vehicles",
            "While a delivery van is parked at the property, employees may take items from it like a temporary storage.");

        PreferDeliveryVehicles = Category.CreateEntry(
            "PreferDeliveryVehicles", true,
            "Prefer delivery vehicles as a source",
            "When an item is both in a delivery van and in a normal storage, employees take it from the van first.");

        EnableChainMover = Category.CreateEntry(
            "EnableChainMover", true,
            "Fallback chain output mover",
            "If the vanilla chemist does not move a mixer's output to the next mixer, The Employees Know moves it after the delay below. Vanilla movement always wins.");

        ChainMoveDelaySeconds = Category.CreateEntry(
            "ChainMoveDelaySeconds", 3.0f,
            "Chain mover fallback delay (seconds)",
            "How long an output must sit in a mixer before The Employees Know moves it to the next mixer. Increase this if vanilla already moves the output.");

        SearchIntervalSeconds = Category.CreateEntry(
            "SearchIntervalSeconds", 0.75f,
            "Search interval (seconds)",
            "How often an idle employee looks for a new source. Lower values react faster but scan more often.");

        Language = Category.CreateEntry(
            "Language", "auto",
            "Language",
            "auto, en, es, fr, de, pt, ru, zh or ja. Auto follows the operating system language.");

        DebugLogging = Category.CreateEntry(
            "DebugLogging", false,
            "Debug logging",
            "Write extra lines about storage scans, chosen sources and skipped items.");
    }
}
