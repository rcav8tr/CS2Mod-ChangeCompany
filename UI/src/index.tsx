import { ModRegistrar } from "cs2/modding";

import { ChangeCompanyComponent } from "changeCompanyComponent"
import mod from "../mod.json";

const register: ModRegistrar = (moduleRegistry) =>
{
    // Add this mod's component to the selected info sections.
    moduleRegistry.extend("game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", "selectedInfoSectionComponents", ChangeCompanyComponent)

    // Registration is complete.
    console.log(mod.id + " registration complete.");
}

export default register;