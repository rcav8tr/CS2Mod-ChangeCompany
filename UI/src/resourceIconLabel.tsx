import { useLocalization    } from "cs2/l10n";

import { SpecialCompanyType } from "changeCompanyComponent";
import   mod                  from "../mod.json";
import { ModuleResolver     } from "moduleResolver";
import   styles               from "resourceIconLabel.module.scss";

// Props for ResourceIconLabel.
interface ResourceIconLabelProps
{
    specialCompanyType:     SpecialCompanyType,
    resource:               string,
    trailingConcatenator:   boolean,
}

// Custom component to combine the icon and label for a resource
// so the game does not split the icon from the label when wrapping in the company selector.
export const ResourceIconLabel = ({ specialCompanyType, resource, trailingConcatenator }: ResourceIconLabelProps) =>
{
    // Get the icon for the resource or special company.
    let resourceIcon: string;
    switch (specialCompanyType)
    {
        case SpecialCompanyType.None:
            resourceIcon = "Media/Game/Resources/" + resource + ".svg";
            break;
        case SpecialCompanyType.Random:
            resourceIcon = "coui://" + mod.id.toLowerCase() + "/Random.svg";
            break;
        case SpecialCompanyType.Remove:
            resourceIcon = "coui://" + mod.id.toLowerCase() + "/Remove.svg";
            break;
    }

    // Get the game's translated text for the resource or special company.
    // The game uses "+" for the concatenator character for all languages.
    const { translate } = useLocalization();
    let resourceText: string; 
    switch (specialCompanyType)
    {
        case SpecialCompanyType.None:
            resourceText = (translate("Resources.TITLE[" + resource + "]") || resource) + (trailingConcatenator ? " + " : "");
            break;
        case SpecialCompanyType.Random:
            resourceText = translate(mod.id + ".Random") || "Random";
            break;
        case SpecialCompanyType.Remove:
            resourceText = translate(mod.id + ".Remove") || "Remove";
            break;
    }

    // Return the resource icon and label.
    return (
        <div className={styles.resourceIconLabel}>
            <img className={ModuleResolver.instance.CompanySectionClasses.icon} src={resourceIcon} />
            <div className={styles.resourceLabel}>{resourceText}</div>
        </div>
    );
}