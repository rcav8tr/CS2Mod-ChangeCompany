import { useValue                       } from "cs2/api";
import { useLocalization                } from "cs2/l10n";
import { PanelSection, PanelSectionRow  } from "cs2/ui";

import { ProductionBalanceInfo, bindingProductionBalanceInfoIndustrial, bindingProductionBalanceInfoOffice  } from "bindings";
import { SpecialCompanyType                                                                                 } from "changeCompanyComponent";
import   mod                                                                                                  from "../mod.json";
import   styles                                                                                               from "productionBalancePanel.module.scss";
import { ResourceIconLabel                                                                                  } from "resourceIconLabel";

// Props for ProductionBalancePanelInfo.
export interface ProductionBalancePanelInfoProps
{
    isIndustrial: boolean;
}

// Display the production balance info for industrial or office.
export const ProductionBalancePanelInfo = ({ isIndustrial }: ProductionBalancePanelInfoProps) =>
{
    // Translation.
    const { translate } = useLocalization();

    // Get section heading for industrial vs office.
    const sectionHeading: string | null =
        translate(mod.id + (isIndustrial ? ".ProductionBalanceIndustrial" : ".ProductionBalanceOffice"),
        (isIndustrial ? "Industrial" : "Office"));

    // Get production balance info for industrial vs office.
    const productionBalanceInfo: ProductionBalanceInfo = isIndustrial ?
        useValue(bindingProductionBalanceInfoIndustrial) :
        useValue(bindingProductionBalanceInfoOffice);

    // The panel section consists of a header row and multiple data rows.
    // Most data values are strings and already formatted in C# before being passed to UI.
    // The last change resources are displayed only if valid.
    const noResource: string = "NoResource";
    return (
        <PanelSection>
            <PanelSectionRow
                className={styles.productionBalancePanelSectionRow}
                left={<div className={styles.productionBalancePanelSectionHeader}>{sectionHeading}</div>}
            />
            <PanelSectionRow
                className={styles.productionBalancePanelSectionRow}
                left={translate(mod.id + ".ProductionBalanceInfoCompanies")}
                right={productionBalanceInfo.companyCount}
                tooltip={translate(mod.id + ".ProductionBalanceInfoCompaniesTooltip")}
            />
            <PanelSectionRow
                className={styles.productionBalancePanelSectionRow}
                left={translate(mod.id + ".ProductionBalanceInfoMinimumCompanies")}
                right={productionBalanceInfo.minimumCompanies}
                tooltip={translate(mod.id + ".ProductionBalanceInfoMinimumCompaniesTooltip")}
            />
            <PanelSectionRow
                className={styles.productionBalancePanelSectionRow}
                left={translate(mod.id + ".ProductionBalanceInfoStandardDeviation")}
                right={productionBalanceInfo.standardDeviationPercent}
                tooltip={translate(mod.id + ".ProductionBalanceInfoStandardDeviationTooltip")}
            />
            <PanelSectionRow
                className={styles.productionBalancePanelSectionRow}
                left={translate(mod.id + ".ProductionBalanceInfoMinimumStandardDeviation")}
                right={productionBalanceInfo.minimumStandardDeviation}
                tooltip={translate(mod.id + ".ProductionBalanceInfoMinimumStandardDeviationTooltip")}
            />
            <PanelSectionRow
                className={styles.productionBalancePanelSectionRow}
                left={translate(mod.id + ".ProductionBalanceInfoLastChangeDateTime")}
                right={productionBalanceInfo.lastChangeDateTime}
                tooltip={translate(mod.id + ".ProductionBalanceInfoLastChangeDateTimeTooltip")}
            />
            <PanelSectionRow
                className={styles.productionBalancePanelSectionRow}
                left={translate(mod.id + ".ProductionBalanceInfoLastChangeFromResource")}
                right={(productionBalanceInfo.lastChangeFromResource != noResource &&
                    <ResourceIconLabel
                        specialCompanyType={SpecialCompanyType.None}
                        resource={productionBalanceInfo.lastChangeFromResource}
                        trailingConcatenator={false}
                    />)}
                tooltip={translate(mod.id + ".ProductionBalanceInfoLastChangeFromResourceTooltip")}
            />
            <PanelSectionRow
                className={styles.productionBalancePanelSectionRow}
                left={translate(mod.id + ".ProductionBalanceInfoLastChangeToResource")}
                right={(productionBalanceInfo.lastChangeToResource != noResource &&
                    <ResourceIconLabel
                        specialCompanyType={SpecialCompanyType.None}
                        resource={productionBalanceInfo.lastChangeToResource}
                        trailingConcatenator={false}
                    />)}
                tooltip={translate(mod.id + ".ProductionBalanceInfoLastChangeToResourceTooltip")}
            />
            <PanelSectionRow
                className={styles.productionBalancePanelSectionRow}
                left={translate(mod.id + ".ProductionBalanceInfoNextCheckDateTime")}
                right={productionBalanceInfo.nextCheckDateTime}
                tooltip={translate(mod.id + ".ProductionBalanceInfoNextCheckDateTimeTooltip")}
            />
        </PanelSection>
    );
}
