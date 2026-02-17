import { trigger                    } from "cs2/api";
import { SelectedInfoSectionBase    } from "cs2/bindings";
import { useLocalization            } from "cs2/l10n";
import { FormattedParagraphsProps   } from "cs2/ui";

import   buttonStyles                 from "changeCompanyButton.module.scss";
import   mod                          from "../mod.json";
import { ModuleResolver             } from "moduleResolver";

// The component for the lock company section.
export const LockCompanyComponent = (componentList: any): any =>
{
    // Define props for company workplaces section.
    // Adapted from bindings.d.ts for the game's sections.
    interface LockCompanySection extends SelectedInfoSectionBase
    {
        companyLocked: boolean,
    }

    // Add LockCompanySection to the component list.
    // Make sure section name is unique by including the mod id.
    componentList[mod.id + ".LockCompanySection"] = (props: LockCompanySection) =>
    {
        // Get the mod's translated text.
        const { translate } = useLocalization();
        const sectionHeading: string = translate(mod.id + ".LockCompany" ) || "Lock Company";
        const labelLockAll:   string = translate(mod.id + ".LockAll"     ) || "Lock All";
        const labelUnlockAll: string = translate(mod.id + ".UnlockAll"   ) || "Unlock All";

        // Get the mod's translated formatted tooltip text.
        const tooltipText: string = translate(mod.id + ".SectionTooltipLockCompany") || "Lock or unlock the company on this property.";
        const formattedParagraphsProps: FormattedParagraphsProps = { children: tooltipText };
        const formattedTooltip: JSX.Element = ModuleResolver.instance.FormattedParagraphs(formattedParagraphsProps);

        // Get lock icon.
        // The two image files were copied from the game's Lock.svg and OpenLock.svg files but with the color changed.
        const lockIcon: string = "coui://" + mod.id.toLowerCase() + (props.companyLocked ? "/LockClosed.svg" : "/LockOpen.svg");

        // Handle click on button for toggle company locked.
        function onToggleCompanyLockedClicked()
        {
            // ToolButton by default makes a click, so no need to make a sound here.
            trigger(mod.id, "ToggleCompanyLockedClicked");
        }

        // Handle click on button for lock or unlock for all companies like this.
        function onAllCompaniesLikeCurrentClicked(lockAll: boolean)
        {
            trigger("audio", "playSound", ModuleResolver.instance.UISound.selectItem, 1);
            trigger(mod.id, "AllCompaniesLikeCurrentClicked", lockAll);
        }

        // Construct the lock company section which contains 1 row:
        //      section heading
        //      button to toggle the locked status on this company
        //      buttons to lock or unlock all existing companies like this company
        return (
            <ModuleResolver.instance.InfoSection tooltip={formattedTooltip}>
                <ModuleResolver.instance.InfoRow
                    left={sectionHeading}
                    uppercase={true}
                    right=
                    {
                        <>
                            <ModuleResolver.instance.ToolButton
                                className={ModuleResolver.instance.ToolButtonClasses.button}
                                src={lockIcon}
                                onSelect={onToggleCompanyLockedClicked}
                                selected={props.companyLocked}
                                multiSelect={false}
                                disabled={false}
                                focusKey={ModuleResolver.instance.FOCUS_DISABLED}
                            />
                            <button className={buttonStyles.changeCompanyButtonEnabled} onClick={() => onAllCompaniesLikeCurrentClicked(true )}>{labelLockAll  }</button>
                            <button className={buttonStyles.changeCompanyButtonEnabled} onClick={() => onAllCompaniesLikeCurrentClicked(false)}>{labelUnlockAll}</button>
                        </>
                    }
                    disableFocus={true}
                />
            </ModuleResolver.instance.InfoSection>
        );
    }

    // Return the updated component list.
    return componentList as any;
}