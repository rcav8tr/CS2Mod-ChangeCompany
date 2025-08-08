import { trigger                    } from "cs2/api";
import { SelectedInfoSectionBase    } from "cs2/bindings";
import { useLocalization            } from "cs2/l10n";
import { FormattedParagraphsProps   } from "cs2/ui";

import   styles                       from "changeCompanyComponent.module.scss";
import { CompanySelector            } from "companySelector";
import { ModuleResolver             } from "moduleResolver";
import   mod                          from "../mod.json";

// Define special company types.
// Matches SpecialCompanyType from C#.
export enum SpecialCompanyType
{
    None,
    Random,
    Remove
}

// Company info.
export type CompanyInfo =
{
    specialType: SpecialCompanyType,
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
            propertyType:   PropertyType,
            hasCompany:     boolean,
            companyInfos:   CompanyInfo[]
        }

    // Add ChangeCompanySection to the component list.
    // Make sure section name is unique by including the mod id.
    componentList[mod.id + ".ChangeCompanySection"] = (props: ChangeCompanySection) =>
    {
        // Get the mod's translated text.
        const { translate } = useLocalization();
        const sectionHeading:  string = translate(mod.id + ".ChangeCompany") || "Change Company";
        const labelChangeThis: string = translate(mod.id + ".ChangeThis"   ) || "Change This";
        const labelChangeAll:  string = translate(mod.id + ".ChangeAll"    ) || "Change All";

        // Get the game's translated text for left and right headings based on property type.
        let headingSuffixLeft:  string | null = null;
        let headingSuffixRight: string | null = null;
        switch (props.propertyType)
        {
            case PropertyType.Commercial: headingSuffixLeft = "SELLS";    headingSuffixRight = "REQUIRES"; break;
            case PropertyType.Industrial: headingSuffixLeft = "PRODUCES"; headingSuffixRight = "REQUIRES"; break;
            case PropertyType.Office:     headingSuffixLeft = "PRODUCES"; headingSuffixRight = "REQUIRES"; break;
            case PropertyType.Storage:    headingSuffixLeft = "STORES";                                    break;
        }
        const headingLeft:  string = headingSuffixLeft  ? translate("SelectedInfoPanel.COMPANY_" + headingSuffixLeft ) || headingSuffixLeft  : "";
        const headingRight: string = headingSuffixRight ? translate("SelectedInfoPanel.COMPANY_" + headingSuffixRight) || headingSuffixRight : "";

        // Get the mod's translated formatted tooltip text based on property type.
        const tooltipText: string = "" + 
            translate(mod.id + ".SectionTooltip" + PropertyType[props.propertyType]) +
            (props.hasCompany ? "\n" + translate(mod.id + ".SectionTooltipChangeAll") : "");
        const formattedParagraphsProps: FormattedParagraphsProps = { children: tooltipText };
        const formattedTooltip: JSX.Element = ModuleResolver.instance.FormattedParagraphs(formattedParagraphsProps);

        // Handle click on Change This button
        function onChangeThisClicked()
        {
            trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
            trigger(mod.id, "ChangeThisClicked");
        }

        // Handle click on Change All button
        function onChangeAllClicked()
        {
            trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
            trigger(mod.id, "ChangeAllClicked");
        }

        // Construct the change company section.
        // Row 1:  Section heading and Change This button.
        // Row 2:  Change All button.  Displayed only if the property has a company.
        // Row 3:  Headings for the dropdown list.
        // Row 4:  Dropdown list of companies to choose from.
        return (
            <ModuleResolver.instance.InfoSection tooltip={formattedTooltip}>
                <ModuleResolver.instance.InfoRow
                    left={sectionHeading}
                    uppercase={true}
                    right={<button className={styles.changeCompanyChangeThisButton} onClick={() => onChangeThisClicked()}>{labelChangeThis}</button>}
                    disableFocus={true}
                />
                {
                    props.hasCompany &&
                    <ModuleResolver.instance.InfoRow
                        className={styles.changeCompanyHeadingRow}
                        right={<button className={styles.changeCompanyChangeThisButton} onClick={() => onChangeAllClicked()}>{labelChangeAll}</button>}
                        disableFocus={true}
                        subRow={true}
                    />
                }
                <ModuleResolver.instance.InfoRow
                    className={styles.changeCompanyHeadingRow}
                    left={headingLeft}
                    right={headingRight}
                    disableFocus={true}
                    subRow={true}
                />
                <ModuleResolver.instance.InfoRow
                    className={styles.changeCompanyDropdownRow}
                    left={<CompanySelector companyInfos={props.companyInfos} />}
                    disableFocus={true}
                    subRow={true}
                />
            </ModuleResolver.instance.InfoSection>
        );
    }

    // Return the updated component list.
    return componentList as any;
}