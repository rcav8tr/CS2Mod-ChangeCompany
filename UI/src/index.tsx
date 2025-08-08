import { ModRegistrar } from "cs2/modding";

import { ChangeCompanyComponent } from "changeCompanyComponent"
import { LockCompanyComponent   } from "lockCompanyComponent"
import mod from "../mod.json";

const register: ModRegistrar = (moduleRegistry) =>
{
    // Add this mod's components to the selected info sections.
    moduleRegistry.extend("game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", "selectedInfoSectionComponents", ChangeCompanyComponent)
    moduleRegistry.extend("game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", "selectedInfoSectionComponents", LockCompanyComponent)

    // Registration is complete.
    console.log(mod.id + " registration complete.");
}

export default register;