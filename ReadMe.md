# Main Features
The **Change Company** mod includes 4 main features:
- **Change Company**:  Allows you to change the company for a mixed residential, commercial, industrial, storage, or office building.
- **Production Balance**:  Automatically balances production of industrial and office resources.
- **Lock Company**:  Allows you to lock companies to prevent them from moving away.
- **Company Workplaces**:  Allows you to override the number of workplaces for a company.

These features are described in the following sections.

 
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
- Select a mixed residential, commercial, industrial, storage, or office building to display its information.
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
- All employees of any old company are layed off.
  The new company will hire its own employees, which may or may not be the same employees that were layed off.
- If the old company was individually locked (see **Lock Company** below), the new company will be individually locked.
- If the old company had a workplaces override and the **Keep Workplaces Override After Change** option is set (see **Options** below), the override will remain.
  If the option is not set, the override will be lost.
- If the building has no custom name, the building will be named according to the brand of the new company.
  If the building has a custom name, the custom name will be retained after the company is changed.
  To restore a building name to its default (i.e. company brand and address), change the building name to blank.
- The new company operates the same as if the game assigned the company to the building.

Upon removing a company:
- All employees of the old company are layed off.
- If the old company was individually locked, the lock is lost.
- If the old company had a workplaces override, the override is lost.
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


# Production Balance - Description
The objective of **Production Balance** is to correct the situation where the city produces surpluses of some resources
while at the same time has deficits of other resources which have to be imported.

When enabled (see **Options** below), production of industrial and office resources
are automatically balanced according to the surplus or deficit of the resources.
See **Retrictions** below.

On the **Check Interval** you choose (see **Options** below), the following is automatically performed
once for industrial resources and once for office resources:
- Determine the resource that has the highest surplus.
- Determine the resource that has the highest deficit.
- Among the companies that produce the resource with the highest surplus, the company with the lowest total worth is chosen.
- Automatically change the chosen company to a company that produces the highest deficit.
  This company change is the same as described above for **Change Company**.

Achieveing balance may take several game minutes to a few game months depending on factors like:
- **Check Interval**.  A higher interval takes longer.
- Number of companies.  More companies takes longer.
- How far the resources are currently out of balance.  More out of balance takes longer.
Industrial will usually take longer to balance than office.
 
During and after **Production Balance**, you may need to adjust the amount of extraction, commercial, storage, and other items in your city.

If the option is set to **Lock After Change** (see **Options** below),
the company will be individually locked for a change due to **Production Balance**.
Locked companies (individually or entire city) can be changed by **Production Balance**.

### Production Balance - Restrictions
The **Production Balance** feature has the following restrictions and rules:
- Production balance is performed only while the simulation is running.
- Production balance is performed only on companies that can be manually changed by the **Change Company** feature.
- Production balance is performed only if there are a minimum of 50 industrial companies or 15 office companies.
  These values can be adjusted in **Options** and viewed in **Statistics** (see below).
- The standard deviation of the resource surpluses is computed as a percent of the average company production.
  Production balance is performed only if the standard deviation is a minimum of 80%.
  This value can be adjusted in **Options** and viewed in **Statistics** (see below).
- Production balance changes a company only if the company' production is a maximum of 50% of the city's production of the surplus resource.
  This value can be adjusted in **Options** (see below).

Some of these restrictions and rules are to prevent excessive company changes when the production gets close to balanced and in small cities.

### Production Balance - Statistics
The current statistics computed by production balance are displayed on the **Production Balance Statistics** screen.
The screen can be displayed by clicking on the activaton button in the upper left
or by pressing the keyboard shortcut which by default is Shift+Ctrl+B (see **Options** below).
The screen can be dragged and the new position will be remembered between games.
You can use the statistics to help when deciding if or how to adjust the production balance options.

The following statistics are displayed for industrial and for office:
- Companies: number of companies.
- Minimum Companies: minimum number of companies to do production balance (from **Options**).
- Standard Deviation: standard deviation of resource surpluses as a percent of average production.
- Minimum Standard Deviation: minimum standard deviation percent to do production balance (from **Options**).
- Last Change Date Time: date and time of last company change.
- Old Resource: the last company to change produced this resource.
- New Resource: the last company to change now produces this resource.
- Next Check Date Time: date and time of the next production balance check.

Companies and Standard Deviation require the simulation to run a bit for the values to be calculated and displayed.
The last change statistics are only since the game was loaded and are displayed only after a production balance is successfully performed.


# Lock Company - Description
Locking a company prevents the game from the moving the company away.
The game's normal logic may move a company away because of bankruptcy or random chance.
The random chance of a company moving away increases with its tax rate and lack of workers.
You may want to lock a company after changing it (see **Lock After Change** in **Options** below).

To lock a company on its building:
- Select a building to display its information.
- If the company can be locked (see **Restrictions** below), the **Lock Company** section will be displayed.
- The locked status of the current company is shown by the **lock indicator**.
  To toggle the current company between locked and unlocked, click the **lock indicator**.
- To lock or unlock all existing individual companies that are like the current company, click **Lock All** or **Unlock All**.
  The current company will be affected.
  New companies will not be affected.

To lock or unlock all companies in the city, see **Options** below.

### Lock Company - Restrictions
The **Lock Company** feature has the following restrictions:
- The company cannot be locked on a building that is:  service (has no company), extractor (never moves away), storage (never moves away),
  under construction, abandoned, condemned, deleted, destroyed, or outside connection.
- The company cannot be locked on a building that does not already have a company (i.e. cannot not lock something that does not exist).
- The company cannot be locked if the **Lock All Companies** option is set (see **Options** below).


# Company Workplaces - Description
The **Company Workplaces** feature allows you to override the number of workplaces for a company.

To override the workplaces for a company:
- Select a building to display its information.
- If the building has a company with workplaces (see **Restrictions** below), the **Company Workplaces** section will be displayed.
  This includes commercial, extractor, industrial, office, and signature buildings with companies.
- The current override status of the company is displayed.
- To override the total workplaces for this company, enter a new override (5 to 9999) and click **@@Apply**.
- To remove an existing override, click **@@Reset**.
  A default number of workplaces will be set initially.
- After applying or removing an override, the simulation must run for the game to adjust the current number of employees.

### Company Workplaces - Restrictions
The **Company Workplaces** feature has the following restrictions:
- Only companies with workplaces can be overridden (of course).
  Note that industrial warehouses do not have workplaces.
- The new override value must be from 5 to 9999.
  5 is the minimum that the normal game logic allows.
- Game logic normally adjusts workplaces up or down over time based on factors like: available storage, lot size, and building level.
  A company workplaces override prevents this normal game logic.


# Options
The mod options are described below.

## Production Balance
The options for **Production Balance** are described below.

### Production Balance Enabled - Industrial and Office
Production balance is enabled or disabled.
- Default is disabled.

### Check Interval - Industrial and Office
Game minutes between production balance checks.
- Default is 10.
- Each production balance check will change only one company.
- A lower interval will balance faster but may be a greater shock to the city.
- A higher interval will balance more gradually and may be useful to maintain balance without excessive company changes.

### Minimum Companies - Industrial and Office
Minimum number of companies to allow production balance.
- Default is 50 for industrial and 15 for office.
- A lower value will allow balancing on smaller cities, but possibly with excessive company changes.
- A higher value will help prevent excessive company changes, especially on smaller cities.

### Minimum Standard Deviation - Industrial and Office
Minimum standard deviation of resource surpluses as a percent of average company production to allow production balance.
- Default is 80%.
- A lower value will try to get a closer balance, but possibly with excessive company changes.
- A higher value will help prevent excessive company changes, but may result in less closely balanced production.

### Maximum Company Production - Industrial and Office
Maximum production of a company as a percent of the city's production of the resource to allow that company to change.
- Default is 50%.
- This option is mostly for small cities where a single company may be responsible for all or most of the city's production of a resource.
- In this case, the balance logic may continuously rotate production between 2 or a few resources.
- Use a lower value to reduce company changes.
- Use a higher value to allow more company changes.

### Hide Production Balance Statistics Button
Hide the button for **Production Balance Statistics**.
- Default is visible (unchecked).
- When the button is hidden, the keyboard shortcut can still be used to display or hide the statistics.

### Statistics Keyboard Shortcut
Keyboard shortcut for displaying and hiding the **Production Balance Statistics**.
- Default is Shift+Ctrl+B.

### Reset
Reset production balance options to default values.
Reset also hides the **Production Balance Statistics** screen and resets its position.


## Lock Company
The options for **Lock Company** are described below.

### Lock After Change
When this option is clear (default):
- The new company is not automatically locked after the company is changed.
- The new company is still automatically locked if the old company was locked before being changed.

When this option is set:
- The new company is automatically locked after the company is changed.
- This happens even if the old company was not locked before being changed.
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


## Company Workplaces
The options for **Company Workplaces** are described below.

### Keep Workplaces Override After Change
When this option is clear (default):
- After a company is changed, the new company does not keep any workplaces override that was present before the change.

When this option is set:
- After a company is changed, the new company keeps any workplaces override that was present before the change.

### Remove All Overrides
Remove workplaces override from all companies in the city.
This option is available only while in a game.


# Compatibility
This mod is translated into all languages supported by the base game.

Compatibility with other mods:
- This mod is not compatible with the **Economy Fixes** mod by nucleartux, which is currently broken.
- This mod is compatible with **Realistic Workplaces and Households** (RWH) mod.
  A workplaces override from this mod will override the company workplaces calculation from the RWH mod, as intended.
  If an override from this mod is removed or this mod is disabled or unsubscribed, the RWH calculation will be restored when the simulation runs.
- There are no known compatibility issues with any other mods.

This mod can be safely disabled or unsubscribed at any time.  If this mod is disabled or unsubscribed:
- Companies that were changed using this mod are not affected (i.e. a building will not revert to its previous company).
- All individual company locks will be lost when the game is saved.
- All workplace overrides will be lost when the game is saved.
  Normal game logic will adjust the workplaces up or down over time from the last override value
  (i.e. workplaces will NOT be initialized to a default value).
  To initialize overridden companies to a default value, use the **Remove All Overrides** option before disabling or unsubscribing this mod.

To receive an invitation to the Discord server for mod discussion, go to:  [https://discord.gg/HTav7ARPs2](https://discord.gg/HTav7ARPs2)


# Acknowledgements
The following mods were used in the development of this mod:
- Scene Explorer by krzychu124
- Extended Tooltip by Mimonsi
- Plop the Growables by algernon
