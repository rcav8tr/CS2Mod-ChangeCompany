import { trigger, useValue              } from "cs2/api";
import { SelectedInfoSectionBase        } from "cs2/bindings";
import { useLocalization                } from "cs2/l10n";
import { FormattedParagraphsProps       } from "cs2/ui";

import { bindingWorkplacesOverrideValid, bindingWorkplacesOverrideValue } from "bindings";
import   buttonStyles                     from "changeCompanyButton.module.scss";
import   styles                           from "companyWorkplacesComponent.module.scss";
import { ModuleResolver                 } from "moduleResolver";
import   mod                              from "../mod.json";
import { WorkplacesOverrideNumberInput  } from "workplacesOverrideNumberInput";

// The component for the company workplaces section.
export const CompanyWorkplacesComponent = (componentList: any): any =>
{
    // Define props for company workplaces section.
    // Adapted from bindings.d.ts for the game's sections.
    interface CompanyWorkplacesSection extends SelectedInfoSectionBase
    {
        workplacesOverridden:    boolean,
        workplacesOverrideValue: number,
    }

    // Add CompanyWorkplacesSection to the component list.
    // Make sure section name is unique by including the mod id.
    componentList[mod.id + ".CompanyWorkplacesSection"] = (props: CompanyWorkplacesSection) =>
    {
        // Get workplaces override data.
        const workplacesOverrideValid: boolean = useValue(bindingWorkplacesOverrideValid);
        const workplacesOverrideValue: number  = useValue(bindingWorkplacesOverrideValue);

        // Get the mod's translated text.
        const { translate } = useLocalization();
        const sectionHeading:       string = translate(mod.id + ".CompanyWorkplaces" ) || "Company Workplaces";
        const labelDefault:         string = translate(mod.id + ".Default"           ) || "Default";
        const labelOverridden:      string = translate(mod.id + ".Overwritten"       ) || "Overwritten";
        const labelNewOverride:     string = translate(mod.id + ".NewTotalWorkplaces") || "New Total Workplaces";
        const labelApply:           string = translate(mod.id + ".Apply"             ) || "Apply";
        const labelReset:           string = translate(mod.id + ".Reset"             ) || "Reset";
        const thousandsSeparator:   string = translate("Common.THOUSANDS_SEPARATOR", ",") + "";

        // Get the mod's translated formatted tooltip text.
        const tooltipText: string = "" + translate(mod.id + ".SectionTooltipCompanyWorkplaces");
        const formattedTooltipProps: FormattedParagraphsProps = { children: tooltipText };
        const formattedTooltip: JSX.Element = ModuleResolver.instance.FormattedParagraphs(formattedTooltipProps);

        // Function to format a value.
        function FormatValue(value: number): string
        {
            // Logic adapted from the game's index.js for localized numbers.
            const regexReplacement = /\B(?=(\d{3})+(?!\d))/g;
            return value.toFixed(0).replace(regexReplacement, thousandsSeparator);
        }

        // Construct current formatted override, if any.
        const formattedCurrentOverride: string = props.workplacesOverridden ?
            labelOverridden + ": " + FormatValue(props.workplacesOverrideValue) :
            labelDefault;

        // Handle click on Apply button.
        function onApplyClicked()
        {
            // To apply, workplaces override value must valid.
            if (workplacesOverrideValid)
            {
                trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
                trigger(mod.id, "WorkplacesApplyClicked");
            }
        }

        // Handle click on Reset button.
        function onResetClicked()
        {
            // To reset, must be overridden.
            if (props.workplacesOverridden)
            {
                trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
                trigger(mod.id, "WorkplacesResetClicked");
            }
        }

        // Construct the company workplaces section.
        // Row 1:  Section heading and current override status/amount.
        // Row 2:  Row heading, new override amount, Apply button, and Reset button.
        return (
            <ModuleResolver.instance.InfoSection tooltip={formattedTooltip}>
                <ModuleResolver.instance.InfoRow
                    left={sectionHeading}
                    right={formattedCurrentOverride}
                    uppercase={true}
                    disableFocus={true}
                />
                <ModuleResolver.instance.InfoRow
                    className={styles.companyWorkplacesDataRow}
                    left={labelNewOverride}
                    right=
                    {
                        <>
                            <WorkplacesOverrideNumberInput value={workplacesOverrideValue} />
                            { workplacesOverrideValid    && <button className={`${buttonStyles.changeCompanyButtonEnabled }`} onClick={() => onApplyClicked()}>{labelApply}</button> }
                            { workplacesOverrideValid    || <button className={`${buttonStyles.changeCompanyButtonDisabled}`}                                 >{labelApply}</button> }
                            { props.workplacesOverridden && <button className={`${buttonStyles.changeCompanyButtonEnabled }`} onClick={() => onResetClicked()}>{labelReset}</button> }
                            { props.workplacesOverridden || <button className={`${buttonStyles.changeCompanyButtonDisabled}`}                                 >{labelReset}</button> }
                        </>
                    }
                    disableFocus={true}
                    subRow={true}
                />
            </ModuleResolver.instance.InfoSection>
        );
    }

    // Return the updated component list.
    return componentList as any;
}