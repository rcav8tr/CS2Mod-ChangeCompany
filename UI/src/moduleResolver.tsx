import { Theme, UniqueFocusKey                                          } from "cs2/bindings";
import { getModule                                                      } from "cs2/modding";
import { ClassProps, FocusKey, FormattedParagraphsProps, InfoRowProps   } from "cs2/ui";
import { ReactNode                                                      } from 'react';

// When attempting to use the game's InfoSectionProps from ui.d.ts directly, ChangeCompanyComponent has a compile error,
// something about property 'children' does not exist in InfoSectionProps.
// So copy the game's InfoSectionProps here but include children.
export interface InfoSectionProps extends ClassProps
{
    focusKey?:      FocusKey;
    tooltip?:       ReactNode;
    disableFocus?:  boolean;
    children:       any;
}

// Provide access to modules from index.js.
export class ModuleResolver
{
    // Define instance.
    private static _instance: ModuleResolver = new ModuleResolver();
    public static get instance(): ModuleResolver { return this._instance }

    // Define modules.
    // For unknown reasons, using the game's InfoRow and InfoSection directly from ui.d.ts causes a run time error.
    private _focusDisabled:         any;
    private _formattedParagraphs:   any;
    private _infoRow:               any;
    private _infoSection:           any;
    private _uiSound:               any;

    // Define SCSS modules.
    private _companySectionClasses: any;
    private _dropdownClasses:       any;
    private _infoRowClasses:        any;

    // Provide access to modules.
    public get FOCUS_DISABLED():        UniqueFocusKey                                      { return this._focusDisabled            ?? (this._focusDisabled         = getModule("game-ui/common/focus/focus-key.ts",                                                                                                "FOCUS_DISABLED"        )); }
    public get FormattedParagraphs():   (props: FormattedParagraphsProps) => JSX.Element    { return this._formattedParagraphs      ?? (this._formattedParagraphs   = getModule("game-ui/common/text/formatted-paragraphs.tsx",                                                                                     "FormattedParagraphs"   )); }
    public get InfoRow():               (props: InfoRowProps            ) => JSX.Element    { return this._infoRow                  ?? (this._infoRow               = getModule("game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",                                              "InfoRow"               )); }
    public get InfoSection():           (props: InfoSectionProps        ) => JSX.Element    { return this._infoSection              ?? (this._infoSection           = getModule("game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",                                      "InfoSection"           )); }
    public get UISound()                                                                    { return this._uiSound                  ?? (this._uiSound               = getModule("game-ui/common/data-binding/audio-bindings.ts",                                                                                    "UISound"               )); }

    // Provide access to SCSS modules.
    public get CompanySectionClasses(): Theme | any                                         { return this._companySectionClasses    ?? (this._companySectionClasses = getModule("game-ui/game/components/selected-info-panel/selected-info-sections/building-sections/company-section/company-section.module.scss", "classes")); }
    public get DropdownClasses():       Theme | any                                         { return this._dropdownClasses          ?? (this._dropdownClasses       = getModule("game-ui/menu/themes/dropdown.module.scss",                                                                                         "classes")); }
    public get InfoRowClasses():        Theme | any                                         { return this._infoRowClasses           ?? (this._infoRowClasses        = getModule("game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",                                      "classes")); }
}