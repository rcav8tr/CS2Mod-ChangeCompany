import { ModRegistrar } from "cs2/modding";

import { ChangeCompanyComponent  } from "changeCompanyComponent"
import { LockCompanyComponent    } from "lockCompanyComponent"
import   mod                       from "../mod.json";
import { ProductionBalanceButton } from "productionBalanceButton"
import { ProductionBalancePanel  } from "productionBalancePanel"

const register: ModRegistrar = (moduleRegistry) =>
{
    // Add this mod's components to the selected info sections.
    moduleRegistry.extend("game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", "selectedInfoSectionComponents", ChangeCompanyComponent)
    moduleRegistry.extend("game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", "selectedInfoSectionComponents", LockCompanyComponent)

    // Append production balance button to info view button list in game's top left.
    moduleRegistry.append("GameTopLeft", ProductionBalanceButton);

    // Append production balance panel to the game's main panel.
    moduleRegistry.append("Game", ProductionBalancePanel);

    // Registration is complete.
    console.log(mod.id + " registration complete.");
}

export default register;