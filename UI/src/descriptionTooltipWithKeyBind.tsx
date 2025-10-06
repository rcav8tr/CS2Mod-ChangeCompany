import { PropsWithChildren } from "react";

import { useLocalization    } from "cs2/l10n";
import { BalloonDirection   } from "cs2/ui";

import   mod                  from "../mod.json";
import { ModuleResolver     } from "moduleResolver";

interface DescriptionTooltipWithKeyBindProps
{
    title:          string | null;
    description:    string | null;
    direction?:     BalloonDirection;
    keyBind?:       any;
    className?:     string;
}

// Define a description tooltip with key bind.
// Logic adapted from Traffic mod.
export const DescriptionTooltipWithKeyBind = ({ title, description, direction = "down", keyBind, className, children }:
    PropsWithChildren<DescriptionTooltipWithKeyBindProps>) =>
{
    // Get translation.
    const { translate } = useLocalization();
    const translationShortcut = translate(mod.id + ".ProductionBalanceShortcut") + ": ";

    return (
        <ModuleResolver.instance.DescriptionTooltip
            title={title}
            description={description}
            content={
                (keyBind?.binding &&
                    (
                        <p>
                            {translationShortcut}
                            <strong>
                                <ModuleResolver.instance.LocalizedInputPath
                                    group={keyBind.group}
                                    binding={keyBind.binding}
                                    gamepadType={0}
                                    keyboardLayout={0}
                                    short={""}
                                    modifiers={keyBind.modifiers}
                                    layoutMap={""}
                                />
                            </strong>
                        </p>
                    )
                )}
            direction={direction}
            alignment="end"
        >
            <div className={className}>{children}</div>
        </ModuleResolver.instance.DescriptionTooltip>
    )
}