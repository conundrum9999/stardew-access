using Microsoft.Xna.Framework;
using stardew_access.Features;
using stardew_access.Patches;
using stardew_access.Translation;
using stardew_access.Utils;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
// ReSharper disable UnusedMember.Global

namespace stardew_access;

// TODO: Update doc comments
public class API : IStardewAccessApi
{
    private static TranslationCategory ToTranslationCategory(string translationCategory) =>
        translationCategory switch
        {
            "Menu" => TranslationCategory.Menu,
            "MiniGames" => TranslationCategory.MiniGames,
            "CharacterCreationMenu" => TranslationCategory.CharacterCreationMenu,
            "StaticTiles" => TranslationCategory.StaticTiles,
            "CustomCommands" => TranslationCategory.CustomCommands,
            _ => TranslationCategory.Default
        };

    // Note to future self, don't make these static, it won't give errors in sv access but it will in other mods if they try to use the stardew access api.
    //Setting Pragma to disable warning CA1822 prompting to make fields static.
    public API()
    {
    }
#pragma warning disable CA1822 // Mark members as static

    #region Screen reader related

    public string PrevMenuQueryText
    {
        get => MainClass.ScreenReader.PrevMenuQueryText;
        set => MainClass.ScreenReader.PrevMenuQueryText = value;
    }

    public string MenuPrefixText
    {
        get => MainClass.ScreenReader.MenuPrefixText;
        set => MainClass.ScreenReader.MenuPrefixText = value;
    }

    public string MenuSuffixText
    {
        get => MainClass.ScreenReader.MenuSuffixText;
        set => MainClass.ScreenReader.MenuSuffixText = value;
    }

    public string MenuPrefixNoQueryText
    {
        get => MainClass.ScreenReader.MenuPrefixNoQueryText;
        set => MainClass.ScreenReader.MenuPrefixNoQueryText = value;
    }

    public string MenuSuffixNoQueryText
    {
        get => MainClass.ScreenReader.MenuSuffixNoQueryText;
        set => MainClass.ScreenReader.MenuSuffixNoQueryText = value;
    }

    public KeybindList PrimaryInfoKey => MainClass.Config.PrimaryInfoKey;

    public bool Say(string text, bool interrupt)
        => MainClass.ScreenReader.Say(text, interrupt);

    public bool TranslateAndSay(string translationKey, bool interrupt, object? translationTokens = null, string translationCategory = "Default", bool disableTranslationWarnings = false)
        => MainClass.ScreenReader.TranslateAndSay(translationKey, interrupt, translationTokens,
            ToTranslationCategory(translationCategory), disableTranslationWarnings);

    public bool SayWithChecker(string text, bool interrupt, string? customQuery = null)
        => MainClass.ScreenReader.SayWithChecker(text, interrupt, customQuery);

    public bool TranslateAndSayWithChecker(string translationKey, bool interrupt, object? translationTokens = null,
        string translationCategory = "Default", string? customQuery = null, bool disableTranslationWarnings = false)
        => MainClass.ScreenReader.TranslateAndSayWithChecker(translationKey, interrupt, translationTokens,
            ToTranslationCategory(translationCategory), customQuery, disableTranslationWarnings);

    public bool SayWithMenuChecker(string text, bool interrupt, string? customQuery = null)
        => MainClass.ScreenReader.SayWithMenuChecker(text, interrupt, customQuery);

    public bool TranslateAndSayWithMenuChecker(string translationKey, bool interrupt, object? translationTokens = null,
        string translationCategory = "Menu", string? customQuery = null, bool disableTranslationWarnings = false)
        => MainClass.ScreenReader.TranslateAndSayWithMenuChecker(translationCategory, interrupt, translationTokens,
            ToTranslationCategory(translationCategory), customQuery, disableTranslationWarnings);

    public bool SayWithChatChecker(string text, bool interrupt)
        => MainClass.ScreenReader.SayWithChatChecker(text, interrupt);

    public bool SayWithTileQuery(string text, int x, int y, bool interrupt)
        => MainClass.ScreenReader.SayWithTileQuery(text, x, y, interrupt);

    public string Translate(string translationKey, object? tokens = null,
        string translationCategory = "Default", bool disableWarning = false)
        => Translator.Instance.Translate(translationKey, tokens, ToTranslationCategory(translationCategory), disableWarning);
    
    #endregion

    #region Tiles related

    public Dictionary<Vector2, (string name, string category)> SearchNearbyTiles(Vector2 center, int limit)
        => new Radar().SearchNearbyTiles(center, limit, false);

    public SortedDictionary<string, List<(Vector2 position, string name)>> SearchLocation()
        => Radar.SearchLocation();

    public (string? name, string? category) GetNameWithCategoryNameAtTile(Vector2 tile)
        => TileInfo.GetNameWithCategoryNameAtTile(tile, null);

    public string? GetNameAtTile(Vector2 tile) => TileInfo.GetNameAtTile(tile, null);

    #endregion

    #region Inventory and Item related

    public bool SpeakHoveredInventorySlot(InventoryMenu? inventoryMenu,
        bool? giveExtraDetails = null,
        int hoverPrice = -1,
        int extraItemToShowIndex = -1,
        int extraItemToShowAmount = -1,
        string highlightedItemPrefix = "",
        string highlightedItemSuffix = "",
        int? hoverX = null,
        int? hoverY = null) =>
        InventoryUtils.NarrateHoveredSlot(inventoryMenu,
            giveExtraDetails,
            hoverPrice,
            extraItemToShowIndex == -1 ? null : extraItemToShowIndex.ToString(),
            extraItemToShowAmount,
            highlightedItemPrefix,
            highlightedItemSuffix,
            hoverX,
            hoverY);

    public bool SpeakHoveredInventorySlot(InventoryMenu? inventoryMenu,
        bool? giveExtraDetails = null,
        int hoverPrice = -1,
        string? extraItemToShowIndex = null,
        int extraItemToShowAmount = -1,
        string highlightedItemPrefix = "",
        string highlightedItemSuffix = "",
        int? hoverX = null,
        int? hoverY = null) =>
        InventoryUtils.NarrateHoveredSlot(inventoryMenu,
            giveExtraDetails,
            hoverPrice,
            extraItemToShowIndex,
            extraItemToShowAmount,
            highlightedItemPrefix,
            highlightedItemSuffix,
            hoverX,
            hoverY);

    public string GetDetailsOfItem(Item item,
        bool giveExtraDetails = false,
        int price = -1,
        string? extraItemToShowIndex = null,
        int extraItemToShowAmount = -1)
        => InventoryUtils.GetItemDetails(item,
            giveExtraDetails: giveExtraDetails,
            hoverPrice: price,
            extraItemToShowIndex: extraItemToShowIndex,
            extraItemToShowAmount: extraItemToShowAmount);

    public string GetDetailsOfItem(Item item,
        bool giveExtraDetails = false,
        int price = -1,
        int extraItemToShowIndex = -1,
        int extraItemToShowAmount = -1)
        => InventoryUtils.GetItemDetails(item,
            giveExtraDetails: giveExtraDetails,
            hoverPrice: price,
            extraItemToShowIndex: extraItemToShowIndex == -1 ? null : extraItemToShowIndex.ToString(),
            extraItemToShowAmount: extraItemToShowAmount);

    #endregion

    public bool SpeakHoveredClickableComponentsFromList<T>(List<T> ccList) where T : ClickableComponent
        => ClickableComponentUtils.NarrateHoveredComponentFromList(ccList);

    public void SpeakClickableComponent(ClickableComponent component, string? commonUIButtonType = null)
        => ClickableComponentUtils.NarrateComponent(component, CommonUIButton.FromFieldName(commonUIButtonType));

    public bool SpeakHoveredOptionsElementSlot(List<ClickableComponent> optionSlots, List<OptionsElement> options, int currentItemIndex)
        => OptionsElementUtils.NarrateHoveredElementFromSlots(optionSlots, options, currentItemIndex);

    public bool SpeakHoveredOptionsElementFromList<T>(List<T> options) where T : OptionsElement
        => OptionsElementUtils.NarrateHoveredElementFromList(options);

    public void SpeakOptionsElement(OptionsElement element)
        => OptionsElementUtils.NarrateElement(element);

    public void RegisterCustomMenuAsAccessible(string? fullNameOfClass)
    {
        if (string.IsNullOrWhiteSpace(fullNameOfClass))
        {
            Log.Error("fullNameOfClass cannot be null or empty!");
            return;
        }

        Log.Debug($"Added `{fullNameOfClass}` to the list of maually patched custom menus.");
        IClickableMenuPatch.ManuallyPatchedCustomMenus.Add(fullNameOfClass);
    }

    public void RegisterLanguageHelper(string locale, Type helperType)
    {
#if DEBUG
        Log.Trace($"Registered language helper for locale '{locale}': Type: {helperType.Name}");
#endif
        CustomFluentFunctions.RegisterLanguageHelper(locale, helperType);
    }
#pragma warning restore CA1822 // Mark members as static
}