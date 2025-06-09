import { trigger                    } from "cs2/api";
import { SelectedInfoSectionBase    } from "cs2/bindings";
import { useLocalization            } from "cs2/l10n";
import { FormattedParagraphsProps   } from "cs2/ui";

import   styles                       from "changeCompanyComponent.module.scss";
import { CompanySelector            } from "companySelector";
import { ModuleResolver             } from "moduleResolver";
import   mod                          from "../mod.json";

// Resource data for a company.
export type CompanyResourceData =
    {
        resourceOutput: string,
        resourceInput1: string,
        resourceInput2: string
    }

// The component for the change company section.
export const ChangeCompanyComponent = (componentList: any): any =>
{
    // Define property types.
    // Matches PropertyType from C#.
    enum PropertyType
    {
        None,
        Commercial,
        Industrial,
        Office,
        Storage
    }

    // Define props for change company section.
    // Adapted from bindings.d.ts for the game's sections.
    interface ChangeCompanySection extends SelectedInfoSectionBase
        {
            propertyType:         PropertyType,
            companyResourceDatas: CompanyResourceData[]
        }

    // Add ChangeCompanySection to the component list.
    // Make sure section name is unique by including the mod id.
    componentList[mod.id + ".ChangeCompanySection"] = (props: ChangeCompanySection) =>
    {
        // Get the mod's translated text for the section heading and button.
        const { translate } = useLocalization();
        const sectionHeading: string = translate(mod.id + ".ChangeCompany") || "Change Company";
        const changeNowLabel: string = translate(mod.id + ".ChangeNow"    ) || "Change Now";

        // Get the game's translated text for left and right headings based on property type.
        let headingLeftSuffix:  string | null = null;
        let headingRightSuffix: string | null = null;
        switch (props.propertyType)
        {
            case PropertyType.Commercial: headingLeftSuffix = "SELLS";    headingRightSuffix = "REQUIRES"; break;
            case PropertyType.Industrial:
            case PropertyType.Office:     headingLeftSuffix = "PRODUCES"; headingRightSuffix = "REQUIRES"; break;
            case PropertyType.Storage:    headingLeftSuffix = "STORES";                                    break;
        }
        const headingLeft:  string = headingLeftSuffix  ? translate("SelectedInfoPanel.COMPANY_" + headingLeftSuffix ) || headingLeftSuffix  : "";
        const headingRight: string = headingRightSuffix ? translate("SelectedInfoPanel.COMPANY_" + headingRightSuffix) || headingRightSuffix : "";

        // Get the mod's translated formatted tooltip text based on property type.
        const tooltipText: string = translate(mod.id + ".SectionTooltip" + PropertyType[props.propertyType]) || "Select a company from the dropdown.";
        const formattedParagraphsProps: FormattedParagraphsProps = { children: tooltipText };
        const formattedTooltip: JSX.Element = ModuleResolver.instance.FormattedParagraphs(formattedParagraphsProps);

        // Handle click on Change Now button
        function onChangeNowClicked()
        {
            trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
            trigger(mod.id, "ChangeNowClicked");
        }

        // Function to join classes.
        function joinClasses(...classes: any) { return classes.join(" "); }

        // Construct the change company section.
        // Info row 1 has section heading and Change Now button.
        // Info row 2 has left and right headings.
        // Info row 3 has dropdown list of companies to choose from.
        return (
            <ModuleResolver.instance.InfoSection tooltip={formattedTooltip} >
                <ModuleResolver.instance.InfoRow
                    className={ModuleResolver.instance.InfoRowClasses.disableFocusHighlight}
                    left={sectionHeading}
                    uppercase={true}
                    right={<button className={styles.changeNowButton} onClick={() => onChangeNowClicked()}>{changeNowLabel}</button>}
                />
                <ModuleResolver.instance.InfoRow
                    className={joinClasses(ModuleResolver.instance.InfoRowClasses.disableFocusHighlight,
                                           ModuleResolver.instance.InfoRowClasses.subRow,
                                           styles.headingRow)}
                    left={headingLeft}
                    right={headingRight}
                />
                <ModuleResolver.instance.InfoRow
                    className={joinClasses(ModuleResolver.instance.InfoRowClasses.disableFocusHighlight,
                                           ModuleResolver.instance.InfoRowClasses.subRow,
                                           styles.dropdownRow)}
                    left={<CompanySelector companyResourceDatas={props.companyResourceDatas} />}
                />
            </ModuleResolver.instance.InfoSection>
        );
    }

    // Return the updated component list.
    return componentList as any;
}