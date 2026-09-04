# One More Sec

**Team No Bugs Inshallah**

A 3D first-person multiplayer survival shooter for 2–6 players, set on a Mars colony
construction site. Oxygen is both the clock and the currency: it drains every second, it
kills you when it runs out, and it is the only thing you can spend on the suit upgrades
that keep you alive.

---

## Built on Unity's Multiplayer FPS template

This project is built on Unity's **Multiplayer FPS** template, which uses the DOTS stack —
**Netcode for Entities** for replication, plus the template's **GhostBridge** hybrid
authoring layer that lets networked objects be written as ordinary MonoBehaviours.

### What the template provided

| Area | What came with it |
|---|---|
| Networking | Netcode for Entities, ghost replication, RPCs, client prediction and reconciliation |
| Connection flow | Main menu, host / join, Relay and direct connect, lobby, session handling |
| Player | First-person character controller, movement prediction, camera, animation state machines |
| Combat | Hitscan and projectile weapons, hit detection, health, weapon registry, kill feed |
| Infrastructure | Server bootstrap, dedicated-server builds, spawn points, ghost prefab pipeline, HUD scaffolding, leaderboard |

### What we built on top of it

Everything in the table below is ours. None of it existed in the template.

| System | Folder |
|---|---|
| Suit upgrade system (3 upgrades, shop, oxygen economy) | `Assets/Scripts/UpgradeSystem` |
| Oxygen as a survival clock and a currency | `Assets/Scripts/OxygenSystem` |
| Pickup hierarchy (med kits, oxygen tanks, special weapons) | `Assets/Scripts/PickupSystem` |
| Minimap and the reveal service behind Player Scan | `Assets/Scripts/MapRevealSystem` |
| Round-based match flow, scoring and results | `Assets/Scripts/MatchSystem` |

We also modified template files where a feature had to reach into them — the movement
code for the speed multiplier, the shooting code for the damage multiplier, the input
reader for the purchase keys, the spawn system for the round rules, and the leaderboard
for round wins.

---

## Features

### Oxygen — the core mechanic

* Starts at **120 seconds** and drains **1 per second**, with **no maximum cap**.
* At zero the player starts suffocating and loses **5 HP per second**.
* It is also the wallet: every suit upgrade is paid for in oxygen seconds, so buying
  power always costs you time alive.
* Server-owned and replicated. A client cannot mint its own oxygen.

### Suit upgrades

Three upgrades, all bought with oxygen and all timed:

| Upgrade | Effect | Key |
|---|---|---|
| **Speed Boost** | Multiplies move speed for its duration | `1` |
| **Damage Boost** | Multiplies outgoing weapon damage | `2` |
| **Player Scan** | Reveals the nearest opponent on the minimap | `3` |

* Bought from a shop panel in the bottom-left: one cell per upgrade with its icon and
  price, lit while running, with a bar that drains as the effect times out. Cells the
  player cannot afford are dimmed and their price turns red.
* Every rule — in the catalogue, not already running, affordable — lives in
  `UpgradeController` on the **server**. The panel only draws what it is told.
* Damage is captured at the moment a shot is fired, so a rocket already in the air keeps
  the damage it was fired with even if the boost expires mid-flight.

### Match flow

* Formats: **One Round**, **First to 3**, **First to 5**, chosen by the host in the lobby.
* Rounds run for a configurable **2:30**.
* **Death is final for the round.** A player who is killed sits out until the next round
  starts, rather than respawning a few seconds later.
* A round is won by the **last player standing**. If the clock runs out with more than one
  player alive the round is **void** — nobody takes a win from surviving a timer.
* Every round starts with all players respawned fresh: same health, ammo and position for
  everybody, so the previous round's winner carries no advantage.
* A results board at the end ranks players by **rounds won**, then kills, then deaths.

### Minimap and scanning

* A round minimap in the bottom-right, drawn from world positions rather than a second
  camera — so it costs nothing to render and works on any map.
* North stays up; a fixed dot marks the player and a pointer orbits it to show facing.
* **Opponents are not drawn by default.** Only players the server has revealed to you
  appear, which is the entire value of buying a scan.
* Reveals are stored on the player who is *looking*, and replicated to them alone, so no
  other client is ever told that somebody was found.

### Mars gravity

Gravity is tuned to Mars' **0.38 g**, with the jump height raised to match — otherwise
lower gravity only makes you float, it does not make you jump higher.

### Loadout

Everyone starts a round with the shotgun. The rifle and other strong gear are meant to be
won from supply drops rather than chosen in a menu, and the server decides the loadout so a
modified client cannot pick its own.

---

## Controls

| Action | Key |
|---|---|
| Move | `W` `A` `S` `D` |
| Sprint | `Shift` |
| Crouch | `C` |
| Jump | `Space` |
| Shoot | `Left Mouse` |
| Reload | `R` |
| Recharge oxygen | `E` |
| Buy Speed Boost | `1` |
| Buy Damage Boost | `2` |
| Buy Player Scan | `3` |

The number keys are placeholders for a clickable shop panel.

---

## Architecture notes

**The server decides, the client draws.** Every rule that can change the game — spending
oxygen, applying an upgrade, dealing damage, ending a round, revealing an opponent — runs
on the server. Client-side classes read replicated state and render it, and nothing more.

**Contracts instead of concrete classes.** Each system has a `Contracts/` folder holding
the small interfaces it needs from other systems. `SpeedBoost` knows about
`IMovementModifier`, not about the movement code; `SpecialWeapon` knows about
`IWeaponHolder`, not about the combat code. That let the systems be written in parallel by
different people, and kept the upgrade classes free of Unity and networking types.

**Bridges adapt DOTS to those contracts.** Movement, combat and the map are ECS systems,
not MonoBehaviours, so `PlayerUpgradeBridge` and `PlayerPickupBridge` sit on the player and
implement the interfaces on their behalf. This is why the upgrade classes never mention a
ghost, a component or a network id.

**State that a screen needs is replicated as a ghost field.** Running upgrades, reveals and
the match state all travel as `[GhostField]` data on ghost components, and the owner-only
ones are marked `SendToOwner` so they are not broadcast to everybody.

---

## Class diagram

Every class in the project, laid out in one interactive drawing. Click any class to isolate
what it touches, or drag to pan and scroll to zoom.

**[Open class diagram](https://claude.ai/code/artifact/c2c7d8f3-ce3a-41e0-a6a0-46082d1bb0af)**

Each class is outlined in a colour that says who wrote it:

| Outline | Meaning | Count |
|---|---|---|
| **Teal** | Written by us. Nothing like it existed in the template. | 28 |
| **Yellow** | A template class we modified so one of our features could reach into it. | 10 |
| **Grey** | A template class we did not touch, shown only for context. | 5 |
| **Dashed grey** | Planned but not built yet. | 4 |

Relationship lines follow the usual UML notation — filled diamond for composition, hollow
diamond for aggregation, solid arrow for inheritance, dashed purple arrow for implementing
an interface, and a dotted arrow for a plain dependency. A full key sits under the drawing.

---

## Not yet implemented

* `PickupManager` — spawning med kits around the map each round.
* `SupplyDropManager`, `Spaceship`, `SupplyDrop` — the supply ship that flies over each
  round and drops one high-value item.
* `InventoryComponent` and `InventorySlot` are written but not yet wired to the player —
  they are exercised only by the sandbox test dummy.

----

<img width="416" height="295" alt="image" src="https://github.com/user-attachments/assets/a9f4c4dd-23af-4e42-a68f-aa083811f7ee" />

