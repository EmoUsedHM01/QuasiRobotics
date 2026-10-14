# Changelog

## 1.0.0 (2026-10-11)
First Steam Workshop release.

- New project layout and name; based on AR Recovery 1.2.4.
- Assembly, namespace, Harmony id and config folder renamed to QuasiRobotics.
- Console command ar_deployer renamed to qr_deployer.
- Saves made with AR Recovery still load their recovered packages and robots.
- Stock robots added to the roster start with only their variant's weapon arm (drill, machinegun or blade): no random augments, implants, gear or ammo.
- Robot rows in Manage Operators have a Dismantle button (with confirmation): gear goes to Ship's Cargo and the robot package is dismantled for parts.
- Droid packages can no longer be used by the player in missions (no Use option, hotbar or double-click); robots deploy only through the roster. Turrets, drones and enemy deployers are unchanged.
- New Robotics ship room ("HIS Droid R&D") with one device, Printyard: top-left of its tech tree, costs Welding Machine + Robot Parts + Reanimatron. Optional Mod Config Menu toggle (default off) to start new campaigns with it.
- The Robots tab in Manage Operators and the Robots section on the departure screen appear only once Printyard is owned.
- Mod Config Menu "Uninstall mode" toggle: saving on the ship with it on removes all QuasiRobotics data. Robots return to Ship's Cargo as stock packages with their gear, recovered packages become stock packages (gear to cargo), and Robotics devices are removed with their price refunded. Mission saves are left alone.
- Can be added mid-campaign: existing saves get the Robotics room on load, and "Start with Printyard" also installs Printyard in loaded campaigns that lack it.
- Second Robotics device, Salvage Drones (passive; Construction Drone + 2 Toolbox + Ledger). Robots destroyed on a mission return to the ship only if it is owned; otherwise they stay broken where they fell and leave the roster. Optional Mod Config Menu toggle (default off) to start with it.
- Seven Robotics upgrades under Printyard: Fusion Welds (+10 Health), Redundancy Systems (+20 Health, leads to Salvage Drones), Ablative Plating (+4 all Resistances), Reinforced Alloys (+12), Targeting Systems (+6 Accuracy), Enhanced Optics (+12), Overclocker (+1 AP). Bonuses stack and apply to every roster robot.
- Robot slots come from Robotics: Printyard gives 2, and Expanded Bays I-V (top row) give +2 each, for 12 in total. A Mod Config Menu slider (1-5, default 2) sets the slots per upgrade (at 3: up to 18). Expanded Bays II also leads to Salvage Drones.
- Tech tree finished. From Salvage Drones: Restorative Nanobots (+1 Health Regen) and Bolstered Swarm (+2), applied at the start of each robot turn.
- Third Robotics device, Drop Pods (2 Geoscanner + Driller + 2 Welding Machine + Toolbox): a mission active with 2 charges, 48h recharge. Mission Control gets a "Call Drop Pods" option under "Call the capsule" (key X by default; a real game hotkey, rebindable with the native key selector in Mod Config Menu or in Options > Keybindings). Every robot left aboard (not destroyed or in repair) is dropped at a random spot on the map: an FGP Type-7 blast hits the spot, then the robot lands there. Not available on station visits or in story scenes that forbid allies. Optional Mod Config Menu toggle (default off) to start with it.
- Drop Pods upgrades: Rapid Response Unit (robots repair twice as fast on the ship), Foam Canisters (robots put out Burning at the end of their turn), Electric Fence? (enemies that hit a robot in melee take 1-40 Shock damage), Flat Pack Furniture (Drop Pods recharge in 24h).
- "Add Robot" on the departure screen opens a Select Robot window (the native Select Operator window), like picking an operator.
- Robots revived with a Reanimatron stay the same roster robot (name, upgrades and return to the ship).
- Allied robots inspected from afar show their base variant's description.
- Change side on the departure screen keeps the robots you picked.
- Every upgrade node has its own price (salvage, armor plates, electronics and the like) instead of one Welding Machine. Prices show in the tech tree.
- No Friendly Fire (by NBKRedSpy) built in: allies such as robots and turrets no longer hurt you or each other; your own shots still hit everyone. Mod Config Menu toggle "Disable friendly fire" (default on).
- All mod text moved to Localization/, one file per game language (English for now in each). The mod follows the game language and falls back to English. See Localization/README.md to translate.
- Robots brought on a mission always land on nearby tiles they can walk to the shuttle from, never behind a wall or on fire, gas or mines.
- Robots still land next to you when the landing spot is boxed in (closed shuttle door, tight start area). Before, they could stay aboard.
- Robots revived with a Reanimatron keep their inventory exactly as it was: no extra Fist in a weapon slot, and the vest and backpack stay in their slots.
- "Repairs complete: <robot>" notification on the ship when a robot finishes repairing, like the clone healing one.
- The ship camera now glides into and out of the Robotics room like the other rooms, instead of snapping.
- Robots now come home whenever the operator extracts alive, including leaving a Bramfatura through a portal or evacuating early. Before, only a won mission brought them back, so they (and Salvage Drones wrecks) were lost.
- Robots whose gun is empty with no ammo to reload now switch to a weapon that can still fire, including weapon limbs like the Judge ABC on Combat ARs, instead of walking up to punch.
- Robots can no longer be given a vest or backpack, and their vest and backpack slots are hidden. Mod Config Menu option "Robots can wear vests and backpacks" (default off) brings them back. A slot that still holds gear stays visible until you take the gear off.
- Mod Config Menu option "Allies act before enemies" (default off): turn order becomes Player, allies, enemies instead of Player, enemies, allies.
- Robots can only be given Robotic augments; Recreational ARs can take any. Mod Config Menu option "Robots take Robotic augments only" (default on). Implants and augments already installed are not affected.
- Robots on the map now add their passive Quasimorphosis gain each turn (e.g. Skull of Amaxatli), like the operator. Mod Config Menu option "Robots add passive Quasimorphosis" (default on).
- Requires Mod Configuration Menu (Crynano_ModConfigMenu).
- Works with Visible Augments (Workshop 3732022272): if it is installed, roster robots show their installed augments on the 3D model like the player does, and the robot body under each new part is hidden so it does not clip through. Enemy cyborgs are unchanged.
