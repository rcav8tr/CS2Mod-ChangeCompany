import { HTMLAttributes, PropsWithChildren, ReactElement, ReactNode } from "react";

import { Theme, UniqueFocusKey                                                      } from "cs2/bindings";
import { ControlPath                                                                } from "cs2/input";
import { getModule                                                                  } from "cs2/modding";
import { ClassProps, FocusKey, FormattedParagraphsProps, InfoRowProps, TooltipProps } from "cs2/ui";


export interface DescriptionTooltipProps extends Omit<TooltipProps, 'tooltip'>
{
    title:          string | null;
    description:    string | null;
    content?:       ReactNode | string | null;
}

// When attempting to use the game's InfoSectionProps from ui.d.ts directly, there is a compile error:
// Property 'children' does not exist on type ... InfoSectionProps.
// So copy the game's InfoSectionProps here but include children.
export interface InfoSectionProps extends ClassProps
{
    focusKey?:      FocusKey;
    tooltip?:       ReactNode;
    disableFocus?:  boolean;
    children:       any;
}

export interface LocalizedInputPathProps
{
    group:          string;
    binding:        ControlPath;
    modifiers:      ControlPath[];
    short:          any;
    gamepadType:    any;
    keyboardLayout: any;
    layoutMap:      any;
}

type PropsToolButton = {
    focusKey?:      UniqueFocusKey | null
    src:            string
    selected?:      boolean
    multiSelect?:   boolean
    disabled?:      boolean
    tooltip?:       string | ReactElement | null
    selectSound?:   any
    uiTag?:         string
    className?:     string
    children?:      string | ReactElement | ReactElement[]
    onSelect?:      (x: any) => any,
} & HTMLAttributes<any>

// Provide access to modules from index.js.
export class ModuleResolver
{
    // Define instance.
    private static _instance: ModuleResolver = new ModuleResolver();
    public static get instance(): ModuleResolver { return this._instance }

    // Define modules.
    // For unknown reasons, using the game's InfoRow and InfoSection directly from ui.d.ts causes a run time error.
    private _descriptionTooltip:            any;
    private _focusDisabled:                 any;
    private _formattedParagraphs:           any;
    private _infoRow:                       any;
    private _infoSection:                   any;
    private _localizedInputPath:            any;
    private _toolButton:                    any;
    private _uiSound:                       any;

    // Define SCSS modules.
    private _companySectionClasses:         any;
    private _dropdownClasses:               any;
    private _iconButtonClasses:             any;
    private _infoRowClasses:                any;
    private _panelClasses:                  any;
    private _panelThemeClasses:             any;
    private _roundHighlightButtonClasses:   any;
    private _tintedIconClasses:             any;
    private _toolButtonClasses:             any;

    // Provide access to modules.
    public get DescriptionTooltip():    (props: PropsWithChildren<DescriptionTooltipProps>)
                                                                          => ReactElement   { return this._descriptionTooltip   ?? (this._descriptionTooltip    = getModule("game-ui/common/tooltip/description-tooltip/description-tooltip.tsx",                           "DescriptionTooltip"    )); }
    public get FOCUS_DISABLED():        UniqueFocusKey                                      { return this._focusDisabled        ?? (this._focusDisabled         = getModule("game-ui/common/focus/focus-key.ts",                                                            "FOCUS_DISABLED"        )); }
    public get FormattedParagraphs():   (props: FormattedParagraphsProps) => ReactElement   { return this._formattedParagraphs  ?? (this._formattedParagraphs   = getModule("game-ui/common/text/formatted-paragraphs.tsx",                                                 "FormattedParagraphs"   )); }
    public get InfoRow():               (props: InfoRowProps            ) => ReactElement   { return this._infoRow              ?? (this._infoRow               = getModule("game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",          "InfoRow"               )); }
    public get InfoSection():           (props: InfoSectionProps        ) => ReactElement   { return this._infoSection          ?? (this._infoSection           = getModule("game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",  "InfoSection"           )); }
    public get LocalizedInputPath():    (props: LocalizedInputPathProps ) => ReactElement   { return this._localizedInputPath   ?? (this._localizedInputPath    = getModule("game-ui/common/localization/localized-input-path.tsx",                                         "LocalizedInputPath"    )); }
    public get ToolButton():            (props: PropsToolButton         ) => ReactElement   { return this._toolButton           ?? (this._toolButton            = getModule("game-ui/game/components/tool-options/tool-button/tool-button.tsx",                             "ToolButton"            )); }
    public get UISound()                                                                    { return this._uiSound              ?? (this._uiSound               = getModule("game-ui/common/data-binding/audio-bindings.ts",                                                "UISound"               )); }

    // Provide access to SCSS modules.
    public get CompanySectionClasses():         Theme | any { return this._companySectionClasses        ?? (this._companySectionClasses         = getModule("game-ui/game/components/selected-info-panel/selected-info-sections/building-sections/company-section/company-section.module.scss", "classes")); }
    public get DropdownClasses():               Theme | any { return this._dropdownClasses              ?? (this._dropdownClasses               = getModule("game-ui/menu/themes/dropdown.module.scss",                                                                                         "classes")); }
    public get IconButtonClasses():             Theme | any { return this._iconButtonClasses            ?? (this._iconButtonClasses             = getModule("game-ui/common/input/button/icon-button.module.scss",                                                                              "classes")); }
    public get InfoRowClasses():                Theme | any { return this._infoRowClasses               ?? (this._infoRowClasses                = getModule("game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",                                      "classes")); }
    public get PanelClasses():                  Theme | any { return this._panelClasses                 ?? (this._panelClasses                  = getModule("game-ui/common/panel/panel.module.scss",                                                                                           "classes")); }
    public get PanelThemeClasses():             Theme | any { return this._panelThemeClasses            ?? (this._panelThemeClasses             = getModule("game-ui/common/panel/themes/default.module.scss",                                                                                  "classes")); }
    public get RoundHighlightButtonClasses():   Theme | any { return this._roundHighlightButtonClasses  ?? (this._roundHighlightButtonClasses   = getModule("game-ui/common/input/button/themes/round-highlight-button.module.scss",                                                            "classes")); }
    public get TintedIconClasses():             Theme | any { return this._tintedIconClasses            ?? (this._tintedIconClasses             = getModule("game-ui/common/image/tinted-icon.module.scss",                                                                                     "classes")); }
    public get ToolButtonClasses():             Theme | any { return this._toolButtonClasses            ?? (this._toolButtonClasses             = getModule("game-ui/game/components/tool-options/tool-button/tool-button.module.scss",                                                         "classes")); }
}