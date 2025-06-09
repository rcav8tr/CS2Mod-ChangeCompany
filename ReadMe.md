# Change Company
Change the company for a commercial, industrial, and office building.
- Commercial includes mixed residential buildings.
- Industrial includes production and storage buildings.

# Description
Ways you might use this mod:
- Better manage the production chain to produce, sell, or store more or less of a resource.
- Group together or spread out production of the same resource.
- Place a storage building near where a resource is extracted, required, produced, or sold (e.g. using **Plop the Growables** mod).
  Then use **Change Company** to change the company (i.e. resource) stored at the storage building.
  This may improve traffic flow by reducing the distance trucks need to travel to obtain or deliver resources.
- Use together with my **Resource Locator** mod which locates buildings where resources are required, produced, sold, and stored.

To change the company on a building:
- Select a building to display its information.
- If the company can be changed (see Restrictions below), the Change Company section will be displayed.
- Choose the new company in the dropdown based on the resource you want the new company to sell, produce, or store.
- Click on the Change Now button.
  The company is changed immediately, even if the simulation is paused.

After the company is changed:
- All employees of any existing company are layed off.
  The new company will hire its own employees.
- If the building has no custom name, the building will be named according to the brand of the new company.
  If the building has a custom name, the custom name will be retained after the company is changed.
  To restore a building name to its default (i.e. company brand and address), change the building name to blank.
- The new company operates the same as if the game assigned the company to the building.
  Note that the company might still eventually move away based on the game's normal logic.


# Restrictions
The mod places the following restrictions on changing a company:
- The company cannot be changed on a building that is:
  service (has no company), signature, extractor, under construction, abandoned, condemned, deleted, destroyed, or outside connection.
- The company cannot be changed on a building that allows only one resource to be sold, produced, or stored.
  For example, a gas station can sell only Petrochemicals.
  The company on a gas station cannot be changed to sell any resource other than Petrochemicals.
- The company cannot be changed to one that sells, produces, or stores a resource other than the building allows.
  For example, an ore storage building allows only Ore, Coal, or Stone to be stored.
  The company on an ore storage building cannot be changed to store any resource other than Ore, Coal, or Stone.
- An industrial building cannot be changed between production and storage by changing the company.

To work around some of the restrictions above, use a mod like **Plop the Growables** to place a building
that allows the desired resource to be sold, produced, or stored or that is the correct production vs storage building.
Then use **Change Company** to change the company in that building.


# Compatibility
The mod is translated into all the languages supported by the base game.

There are no known compatibility issues with any other mods.

This mod can be safely disabled or unsubscribed at any time.
Disabling or unsubscribing this mod does not affect companies that were changed using this mod 
(e.g. a building will not revert to its previous company).


# Possible Future Enhancements
Here are some possible future enhancements that were thought about during development but not included initially:
- Allow a company to be "locked" on its building to prevent the company from moving away based on the game's normal logic.
  The game's normal logic may move a company away because of bankruptcy or random chance.
  The random chance of a company moving away increases with its tax rate and lack of workers.

# Acknowledgements
The following mods were used in the development of this mod:
- Scene Explorer by krzychu124
- Extended Tooltip by Mimonsi
- Plop the Growables by algernon
