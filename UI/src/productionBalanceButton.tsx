import { useValue, trigger  } from "cs2/api";
import { useLocalization    } from "cs2/l10n";
import { Button             } from "cs2/ui";

import { ProductionBalanceUISettings, bindingProductionBalanceUISettings    } from "bindings";
import { DescriptionTooltipWithKeyBind                                      } from "descriptionTooltipWithKeyBind";
import   mod                                                                  from "../mod.json";
import   productionBalanceIcon                                                from "images/productionBalance.svg";

// Button to display or hide the production balance panel.
export const ProductionBalanceButton = () =>
{
    // Get production balance UI settings.
    const productionBalanceUISettings: ProductionBalanceUISettings = useValue(bindingProductionBalanceUISettings);

    // Get translations.
    const { translate } = useLocalization();
    const productionBalanceTitle       = translate(mod.id + ".ProductionBalanceStatistics");
    const productionBalanceDescription = translate(mod.id + ".ProductionBalanceDescription");

    return (
        <>
        {
            productionBalanceUISettings.hideActivationButton ||
            (
                <DescriptionTooltipWithKeyBind
                    title={productionBalanceTitle}
                    description={productionBalanceDescription}
                    keyBind={productionBalanceUISettings.activationKey}
                >
                    <Button
                        src={productionBalanceIcon}
                        variant="floating"
                        selected={productionBalanceUISettings.panelVisible}
                        onSelect={() => trigger(mod.id, "ProductionBalanceButtonClicked")}
                    />
                </DescriptionTooltipWithKeyBind>
            )
        }
        </>
    );
}