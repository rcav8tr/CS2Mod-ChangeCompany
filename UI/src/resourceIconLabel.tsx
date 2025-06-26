import { useLocalization    } from "cs2/l10n";

import { ModuleResolver     } from "moduleResolver";
import   styles               from "resourceIconLabel.module.scss";

// Props for ResourceIconLabel.
interface ResourceIconLabelProps
{
    resource:               string,
    trailingConcatenator:   boolean,
}

// Custom component to combine the icon and label for a resource
// so the game does not split the icon from the label when wrapping in the company selector.
export const ResourceIconLabel = ({ resource, trailingConcatenator }: ResourceIconLabelProps) =>
{
    // Get game's icon for the resource.
    const resourceIcon: string = "Media/Game/Resources/" + resource + ".svg";

    // Get the game's translated text for the resource.
    // The game uses "+" for the concatenator character for all languages.
    const { translate } = useLocalization();
    const resourceText: string = (translate("Resources.TITLE[" + resource + "]") || resource) + (trailingConcatenator ? " + " : "");

    // Return the resource icon and label.
    return (
        <div className={styles.resourceIconLabel}>
            <img className={ModuleResolver.instance.CompanySectionClasses.icon} src={resourceIcon} />
            <div className={styles.resourceLabel}>{resourceText}</div>
        </div>
    );
}