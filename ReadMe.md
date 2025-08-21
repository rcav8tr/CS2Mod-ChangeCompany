# Change Company
Change the company for a commercial, industrial, or office building.
- Mixed residential buildings are included.
- Industrial includes production and storage buildings.

# Lock Company
Lock a company to prevent it from moving away.

 
# Change Company - Description
Some possible reasons to change a company:
- Better manage the production chain to produce, sell, or store more or less of a resource.
- Group together or spread out production of the same resource.
- Place a storage building near where a resource is extracted, required, produced, or sold (e.g. using **Plop the Growables** mod).
  Then use **Change Company** to change the company (i.e. resource) stored at the storage building.
  This may improve traffic flow by reducing truck travel distance to obtain or deliver resources.

The **Change Company** feature is intended to be used together with my **Resource Locator** mod
which locates buildings where resources are required, produced, sold, and stored.

To change a company:
- Select a building to display its information.
- If the company can be changed (see **Restrictions** below), the **Change Company** section will be displayed.
- Choose the new company in the dropdown based on the resource you want the new company to sell, produce, or store.
  The company dropdown defaults to the current company, if any.
- To change to a random company, choose **Random** in the dropdown.
  **Random** is available only if there is more than one company to choose from.
- To remove the current company, choose **Remove** in the dropdown.
  **Remove** is available only if there is a current company.
- To change the company on the current building, click **Change This**.
- To change all existing companies that are like the current company, click **Change All**.
  If **Random** is chosen in the dropdown, each company that is changed is assigned its own random company
  (i.e. they do not all get the same random company).
- The change is immediate, even if the simulation is paused.

Upon changing a company to a new company:
- All employees of any current company are layed off.
  The new company will hire its own employees, which may or may not be the same employees that were layed off.
- If the building has no custom name, the building will be named according to the brand of the new company.
  If the building has a custom name, the custom name will be retained after the company is changed.
  To restore a building name to its default (i.e. company brand and address), change the building name to blank.
- If the current company was individually locked (see **Lock Company** below), the new company will be individually locked.
- The new company operates the same as if the game assigned the company to the building.

Upon removing a company:
- All employees of the current company are layed off.
- Once the simulation runs, the building is placed back on the market and the game may assign a new company to the building.
- The building operates the same as if the game created the building without a company.
  The game will use its normal logic to assign a new company to the building
  or you can use **Change Company** to assign a new company to the building immediately.

### Change Company - Restrictions
The **Change Company** feature has the following restrictions:
- The company cannot be changed on a building that is:
  service (has no company), extractor, under construction, abandoned, condemned, deleted, destroyed, or outside connection.
- The company cannot be changed to one that sells, produces, or stores a resource other than the building allows.
  For example, an ore storage building allows only Ore, Coal, or Stone to be stored.
  The company on an ore storage building cannot be changed to store any resource other than Ore, Coal, or Stone.
- An industrial building cannot be changed between production and storage by changing the company.

To work around some of the restrictions above, use a mod like **Plop the Growables** to place a building
that allows the desired resource to be sold, produced, or stored or that is the correct production vs storage building.
Then use **Change Company** to change the company in that building.


# Lock Company - Description
Locking a company prevents the game from the moving the company away.
The game's normal logic may move a company away because of bankruptcy or random chance.
The random chance of a company moving away increases with its tax rate and lack of workers.
You may want to lock a company after changing it (see **Lock After Change** in **Options** below).

To lock a company on its building:
- Select a building to display its information.
- If the company can be locked (see **Restrictions** below), the **Lock Company** section will be displayed.
- The locked status of the current company is shown by the lock indicator.
  To toggle the current company between locked and unlocked, click the **lock indicator**.
- To lock or unlock all existing individual companies that are like the current company, click **Lock All** or **Unlock All**.
  The current company will be affected.
  New companies will not be affected.

To lock or unlock all companies in the city, see **Options** below.

### Lock Company - Restrictions
The **Lock Company** feature has the following restrictions:
- The company cannot be locked on a building that is:
  service (has no company), extractor (never moves away), under construction, abandoned, condemned, deleted, destroyed, or outside connection.
- The company cannot be locked on a building that does not already have a company (i.e. cannot not lock something that does not exist).
- The company cannot be locked if the **Lock All Companies** option is set (see **Options** below).


# Options
The mod options are described below.

### Lock After Change
When this option is clear (default):
- The new company is not automatically locked after the company is changed.
- The new company is still automatically locked if the current company was locked before being changed.

When this option is set:
- The new company is automatically locked after the company is changed.
- This happens even if the current company was not locked before being changed.
- This happens even if the **Lock All Companies** option (below) is set.

### Lock All Companies
When this option is clear (default):
- All companies in the city are not locked.
- Use the **Lock Company** feature to lock or unlock individual companies, subject to the restrictions above.

When this option is set:
- All companies in the city are locked.
- The **Lock Company** section on the building info display is hidden.
- Any new companies will be locked.
- Any existing locks on individual companies will take effect again if this option is later cleared.

### Unlock All Companies
Remove the lock on all individual companies in the city.
This option is available only while in a game.


# Compatibility
This mod is translated into all the languages supported by the base game.

This mod is not compatible with the **Economy Fixes** mod by nucleartux, which is currently broken.
There are no known compatibility issues with any other mods.

This mod can be safely disabled or unsubscribed at any time.
- Disabling or unsubscribing this mod does not affect companies that were changed using this mod 
  (e.g. a building will not revert to its previous company).
- Disabling or unsubscribing this mod will cause all individual company locks to be lost when the game is saved.

To receive an invitation to the Discord server for mod discussion, go to:  [https://discord.gg/HTav7ARPs2](https://discord.gg/HTav7ARPs2)


# Acknowledgements
The following mods were used in the development of this mod:
- Scene Explorer by krzychu124
- Extended Tooltip by Mimonsi
- Plop the Growables by algernon
