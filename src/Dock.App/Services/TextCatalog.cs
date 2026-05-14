using System;
using System.Collections.Generic;
using Dock.App.Models;

namespace Dock.App.Services;

public static class TextCatalog
{
    public const string English = "en-US";
    public const string PortugueseBrazil = "pt-BR";

    public static IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new(English, "English"),
        new(PortugueseBrazil, "Português (Brasil)")
    ];

    public static string NormalizeLanguage(string? language)
    {
        return string.Equals(language, PortugueseBrazil, StringComparison.OrdinalIgnoreCase)
            ? PortugueseBrazil
            : English;
    }

    public static LocalizedText Get(string? language)
    {
        return NormalizeLanguage(language) == PortugueseBrazil
            ? LocalizedText.PortugueseBrazilian
            : LocalizedText.English;
    }
}

public sealed record LanguageOption(string Value, string Label)
{
    public override string ToString() => Label;
}

public sealed class LocalizedText
{
    public static LocalizedText English => new(TextCatalog.English, EnglishValues);

    public static LocalizedText PortugueseBrazilian => new(
        TextCatalog.PortugueseBrazil,
        Merge(EnglishValues, PortugueseBrazilianValues));

    private readonly IReadOnlyDictionary<string, string> _values;

    private LocalizedText(string languageCode, IReadOnlyDictionary<string, string> values)
    {
        LanguageCode = languageCode;
        _values = values;
    }

    public string LanguageCode { get; }

    public string this[string key] => _values.TryGetValue(key, out var value) ? value : key;

    public string LabelFor<T>(T value)
    {
        return value switch
        {
            DockEdge edge => this[$"EnumDockEdge{edge}"],
            DockLayering layering => this[$"EnumDockLayering{layering}"],
            IconQuality quality => this[$"EnumIconQuality{quality}"],
            HoverEffect effect => this[$"EnumHoverEffect{effect}"],
            DockImportMode mode => this[$"EnumDockImportMode{mode}"],
            DockMoveModifierKey moveModifierKey => this[$"EnumDockMoveModifierKey{moveModifierKey}"],
            _ => value?.ToString() ?? ""
        };
    }

    private static IReadOnlyDictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> baseValues,
        IReadOnlyDictionary<string, string> overrides)
    {
        var values = new Dictionary<string, string>(baseValues, StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        return values;
    }

    private static readonly IReadOnlyDictionary<string, string> EnglishValues = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SettingsWindowTitle"] = "Rock ET Dock - Settings",
        ["SettingsSearchPlaceholder"] = "Find a setting",
        ["SettingsGeneralTab"] = "General",
        ["SettingsIconsTab"] = "Icons",
        ["SettingsPositionTab"] = "Position",
        ["SettingsStyleTab"] = "Style",
        ["SettingsBehaviorTab"] = "Behavior",
        ["SettingsCurrentDockTitle"] = "Current dock",
        ["SettingsCreateDockTitle"] = "Create a new dock",
        ["SettingsCreateDockSubtitle"] = "Choose a screen edge and Rock ET Dock will create another bar with the same system-level behavior.",
        ["SettingsDragSectionTitle"] = "Drag and auto-hide",
        ["SettingsBarName"] = "Dock name",
        ["SettingsLanguage"] = "Language",
        ["SettingsRunAtStartup"] = "Start with Windows",
        ["SettingsLockItems"] = "Lock dock items",
        ["SettingsAutoHide"] = "Auto-hide",
        ["SettingsWindowsButton"] = "Add Windows button to the dock",
        ["SettingsRecycleBin"] = "Add Recycle Bin to the dock",
        ["SettingsHideNativeTaskbar"] = "Hide the native Windows taskbar while Rock ET Dock is running",
        ["SettingsMoveModifierKey"] = "Hold this key to move items instead of adding or removing shortcuts",
        ["SettingsGifModifierKey"] = "Hold this key to add dropped GIF files as looping dock items",
        ["SettingsAutoHideDelay"] = "Auto-hide delay (ms)",
        ["SettingsAutoHideDuration"] = "Auto-hide duration (ms)",
        ["SettingsBarFolder"] = "Dock folder",
        ["SettingsIconSize"] = "Size",
        ["SettingsIconOpacity"] = "Opacity",
        ["SettingsHideLabels"] = "Hide icon labels",
        ["SettingsIconSpacing"] = "Spacing between icons",
        ["SettingsIconBottomMargin"] = "Icon bottom margin",
        ["SettingsIconQuality"] = "Quality",
        ["SettingsZoomEnabled"] = "Magnify on hover",
        ["SettingsZoomOpaque"] = "Make icon opaque while magnified",
        ["SettingsZoomSize"] = "Magnified size",
        ["SettingsZoomRange"] = "Magnification range",
        ["SettingsZoomDuration"] = "Magnification time (ms)",
        ["SettingsHoverEffect"] = "Hover effect",
        ["SettingsAddAnimatedGif"] = "Add animated GIF...",
        ["SettingsMonitor"] = "Monitor",
        ["SettingsEdge"] = "Screen edge",
        ["SettingsLayering"] = "Layering",
        ["SettingsBarWidth"] = "Dock width (0 = automatic)",
        ["SettingsBarHeight"] = "Dock height (0 = automatic)",
        ["SettingsOffset"] = "Distance from edge",
        ["SettingsCenterOffset"] = "Center offset",
        ["SettingsCenterHint"] = "0 = absolute center",
        ["SettingsTheme"] = "Theme",
        ["SettingsBackgroundOpacity"] = "Background opacity",
        ["SettingsShellCornerRadius"] = "Dock corner radius",
        ["SettingsTileCornerRadius"] = "Icon tile corner radius",
        ["SettingsFontFamily"] = "Font",
        ["SettingsFontSize"] = "Font size",
        ["SettingsLabelColor"] = "Label color",
        ["SettingsMinimizeWindows"] = "Minimize windows to the dock",
        ["SettingsMinimizeWindowsTooltip"] = "Adds minimized windows as temporary dock items.",
        ["SettingsDisableMinimizeAnimations"] = "Disable minimize animations (coming soon)",
        ["SettingsDisableMinimizeAnimationsTooltip"] = "Depends on minimize-to-dock implementation.",
        ["SettingsShowRunningIndicators"] = "Show running app indicators",
        ["SettingsShowRunningIndicatorsTooltip"] = "Shows a marker on .exe items or .lnk shortcuts when the matching process is open.",
        ["SettingsOpenRunningInstances"] = "Open existing instance when possible",
        ["SettingsOpenRunningInstancesTooltip"] = "Tries to bring an existing window forward before starting a new instance.",
        ["SettingsPopupOnMouseover"] = "Show dock on mouseover",
        ["SettingsPopupDelay"] = "Popup delay (ms)",
        ["SettingsClose"] = "Close",
        ["SettingsBarPrefix"] = "Dock",

        ["MenuDockSettings"] = "Dock settings",
        ["MenuAddItem"] = "Add item",
        ["MenuFile"] = "File...",
        ["MenuFolder"] = "Folder...",
        ["MenuSeparator"] = "Separator",
        ["MenuExit"] = "Exit",
        ["MenuOpenDockFolder"] = "Open dock folder",
        ["MenuCreateNewDock"] = "Create new dock",
        ["MenuMoveDock"] = "Move this dock",
        ["MenuRemoveDock"] = "Remove this dock",

        ["DialogAddFilesTitle"] = "Add files",
        ["DialogAddFilesFilter"] = "All files (*.*)|*.*",
        ["DialogAddFolderDescription"] = "Select the folder to add to the dock",
        ["DialogAddGifTitle"] = "Add animated GIF",
        ["DialogAddGifFilter"] = "Animated GIF (*.gif)|*.gif",

        ["BarLeft"] = "Left Dock",
        ["BarRight"] = "Right Dock",
        ["BarTop"] = "Top Dock",
        ["BarBottom"] = "Bottom Dock",
        ["BarGeneric"] = "Dock",

        ["ItemWindows"] = "Windows",
        ["ItemWindowsSettings"] = "Windows Settings",
        ["ItemFileExplorer"] = "File Explorer",
        ["ItemMicrosoftEdge"] = "Microsoft Edge",
        ["ItemRecycleBin"] = "Recycle Bin",
        ["ItemSeparator"] = "Separator",
        ["EnumDockEdgeBottom"] = "Bottom",
        ["EnumDockEdgeTop"] = "Top",
        ["EnumDockEdgeLeft"] = "Left",
        ["EnumDockEdgeRight"] = "Right",
        ["EnumDockLayeringTopMost"] = "Always on top",
        ["EnumDockLayeringNormal"] = "Normal",
        ["EnumDockLayeringBottom"] = "Always behind",
        ["EnumIconQualityLow"] = "Low",
        ["EnumIconQualityMedium"] = "Medium",
        ["EnumIconQualityHigh"] = "High",
        ["EnumHoverEffectNone"] = "None",
        ["EnumHoverEffectBubble"] = "Bubble",
        ["EnumHoverEffectPlateau"] = "Plateau",
        ["EnumDockImportModeMoveToBarFolder"] = "Move into the dock folder",
        ["EnumDockImportModeCreateShortcutInBarFolder"] = "Create a shortcut in the dock folder",
        ["EnumDockMoveModifierKeyShift"] = "Shift",
        ["EnumDockMoveModifierKeyControl"] = "Control",
        ["EnumDockMoveModifierKeyAlt"] = "Alt"
    };

    private static readonly IReadOnlyDictionary<string, string> PortugueseBrazilianValues = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SettingsWindowTitle"] = "Rock ET Dock - Configurações",
        ["SettingsSearchPlaceholder"] = "Localizar uma configuração",
        ["SettingsGeneralTab"] = "Geral",
        ["SettingsIconsTab"] = "Ícones",
        ["SettingsPositionTab"] = "Posição",
        ["SettingsStyleTab"] = "Estilo",
        ["SettingsBehaviorTab"] = "Comportamento",
        ["SettingsCurrentDockTitle"] = "Barra atual",
        ["SettingsCreateDockTitle"] = "Criar uma nova barra",
        ["SettingsCreateDockSubtitle"] = "Escolha um canto da tela e o Rock ET Dock cria outra barra com o mesmo comportamento integrado ao sistema.",
        ["SettingsDragSectionTitle"] = "Arrastar e auto-ocultar",
        ["SettingsBarName"] = "Nome da barra",
        ["SettingsLanguage"] = "Idioma",
        ["SettingsRunAtStartup"] = "Iniciar com o Windows",
        ["SettingsLockItems"] = "Travar itens da barra",
        ["SettingsAutoHide"] = "Auto-ocultar",
        ["SettingsWindowsButton"] = "Adicionar botão Windows na barra",
        ["SettingsRecycleBin"] = "Adicionar Lixeira na barra",
        ["SettingsHideNativeTaskbar"] = "Esconder a barra nativa do Windows enquanto o Rock ET Dock estiver aberto",
        ["SettingsMoveModifierKey"] = "Segure esta tecla para mover itens em vez de adicionar ou remover atalhos",
        ["SettingsGifModifierKey"] = "Segure esta tecla para adicionar GIFs arrastados como itens em loop",
        ["SettingsAutoHideDelay"] = "Atraso do auto-ocultar (ms)",
        ["SettingsAutoHideDuration"] = "Duração do auto-ocultar (ms)",
        ["SettingsBarFolder"] = "Pasta da barra",
        ["SettingsIconSize"] = "Tamanho",
        ["SettingsIconOpacity"] = "Opacidade",
        ["SettingsHideLabels"] = "Ocultar texto dos ícones",
        ["SettingsIconSpacing"] = "Espaçamento entre ícones",
        ["SettingsIconBottomMargin"] = "Margem inferior dos ícones",
        ["SettingsIconQuality"] = "Qualidade",
        ["SettingsZoomEnabled"] = "Ampliar ao passar o mouse",
        ["SettingsZoomOpaque"] = "Ícone opaco durante ampliação",
        ["SettingsZoomSize"] = "Tamanho da ampliação",
        ["SettingsZoomRange"] = "Alcance da ampliação",
        ["SettingsZoomDuration"] = "Tempo da ampliação (ms)",
        ["SettingsHoverEffect"] = "Efeito hover",
        ["SettingsAddAnimatedGif"] = "Adicionar GIF animado...",
        ["SettingsMonitor"] = "Monitor",
        ["SettingsEdge"] = "Lado da tela",
        ["SettingsLayering"] = "Sobreposição",
        ["SettingsBarWidth"] = "Largura da barra (0 = automática)",
        ["SettingsBarHeight"] = "Altura da barra (0 = automática)",
        ["SettingsOffset"] = "Distância da borda",
        ["SettingsCenterOffset"] = "Centralização",
        ["SettingsCenterHint"] = "0 = centro absoluto",
        ["SettingsTheme"] = "Tema",
        ["SettingsBackgroundOpacity"] = "Opacidade do fundo",
        ["SettingsShellCornerRadius"] = "Arredondamento da barra",
        ["SettingsTileCornerRadius"] = "Arredondamento do fundo dos ícones",
        ["SettingsFontFamily"] = "Fonte",
        ["SettingsFontSize"] = "Tamanho da fonte",
        ["SettingsLabelColor"] = "Cor da legenda",
        ["SettingsMinimizeWindows"] = "Minimizar janelas para a barra",
        ["SettingsMinimizeWindowsTooltip"] = "Adiciona janelas minimizadas como itens temporários na barra.",
        ["SettingsDisableMinimizeAnimations"] = "Desativar animações de minimizar (em breve)",
        ["SettingsDisableMinimizeAnimationsTooltip"] = "Depende da implementação de minimizar janelas para a barra.",
        ["SettingsShowRunningIndicators"] = "Mostrar indicadores de apps abertos",
        ["SettingsShowRunningIndicatorsTooltip"] = "Mostra um marcador em itens .exe ou atalhos .lnk quando o processo correspondente estiver aberto.",
        ["SettingsOpenRunningInstances"] = "Abrir instância existente quando possível",
        ["SettingsOpenRunningInstancesTooltip"] = "Tenta trazer a janela existente para frente antes de abrir uma nova instância.",
        ["SettingsPopupOnMouseover"] = "Mostrar barra ao passar o mouse",
        ["SettingsPopupDelay"] = "Atraso do popup (ms)",
        ["SettingsClose"] = "Fechar",
        ["SettingsBarPrefix"] = "Barra",

        ["MenuDockSettings"] = "Configurações da barra",
        ["MenuAddItem"] = "Adicionar item",
        ["MenuFile"] = "Arquivo...",
        ["MenuFolder"] = "Pasta...",
        ["MenuSeparator"] = "Separador",
        ["MenuExit"] = "Sair",
        ["MenuOpenDockFolder"] = "Abrir pasta da barra",
        ["MenuCreateNewDock"] = "Criar nova barra",
        ["MenuMoveDock"] = "Mover esta barra",
        ["MenuRemoveDock"] = "Remover esta barra",

        ["DialogAddFilesTitle"] = "Adicionar arquivos",
        ["DialogAddFilesFilter"] = "Todos os arquivos (*.*)|*.*",
        ["DialogAddFolderDescription"] = "Selecione a pasta para adicionar ao dock",
        ["DialogAddGifTitle"] = "Adicionar GIF animado",
        ["DialogAddGifFilter"] = "GIF animado (*.gif)|*.gif",

        ["BarLeft"] = "Barra Esquerda",
        ["BarRight"] = "Barra Direita",
        ["BarTop"] = "Barra Superior",
        ["BarBottom"] = "Barra Inferior",
        ["BarGeneric"] = "Barra",

        ["ItemWindows"] = "Windows",
        ["ItemWindowsSettings"] = "Configurações do Windows",
        ["ItemFileExplorer"] = "Windows Explorer",
        ["ItemMicrosoftEdge"] = "Microsoft Edge",
        ["ItemRecycleBin"] = "Lixeira",
        ["ItemSeparator"] = "Separador",
        ["EnumDockEdgeBottom"] = "Inferior",
        ["EnumDockEdgeTop"] = "Superior",
        ["EnumDockEdgeLeft"] = "Esquerda",
        ["EnumDockEdgeRight"] = "Direita",
        ["EnumDockLayeringTopMost"] = "Sempre acima",
        ["EnumDockLayeringNormal"] = "Normal",
        ["EnumDockLayeringBottom"] = "Sempre abaixo",
        ["EnumIconQualityLow"] = "Baixa",
        ["EnumIconQualityMedium"] = "Média",
        ["EnumIconQualityHigh"] = "Alta",
        ["EnumHoverEffectNone"] = "Nenhum",
        ["EnumHoverEffectBubble"] = "Bolha",
        ["EnumHoverEffectPlateau"] = "Platô",
        ["EnumDockImportModeMoveToBarFolder"] = "Mover para a pasta da barra",
        ["EnumDockImportModeCreateShortcutInBarFolder"] = "Criar atalho na pasta da barra",
        ["EnumDockMoveModifierKeyShift"] = "Shift",
        ["EnumDockMoveModifierKeyControl"] = "Control",
        ["EnumDockMoveModifierKeyAlt"] = "Alt"
    };
}
