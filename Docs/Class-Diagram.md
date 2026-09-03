# One More Sec — Master Class Diagram

All 45 classes and every relation, written out from the team's master diagram
(`No Bugs Inshallah · Master Class Diagram · v1.0`).

3D first-person multiplayer survival shooter, 4–6 players, Mars colony construction site.
The diagram reads left to right, from the session root out to the interface.

| # | Column | Classes |
| --- | --- | --- |
| 0 | ROOT · SESSION + NETCODE | 6 |
| 1 | MATCH & WORLD | 7 |
| 2 | PLAYER HUB · SPAWNERS | 4 |
| 3 | SUIT SYSTEMS | 6 |
| 4 | COMBAT · INVENTORY · ECONOMY | 6 |
| 5 | UPGRADE HIERARCHY | 4 |
| 6 | PICKUP HIERARCHY | 6 |
| 7 | INTERFACE | 6 |

---

## The four rules the design hangs on

**Oxygen is a timer, not a bar.** It starts at 120s, drains 1 per second, has **no maximum
cap**, costs 5 HP/sec once it hits zero, and pays for every upgrade.

**One way out of a round.** Weapons, suffocation and med kits all write to `HealthSystem`.
Only `HealthSystem` calls `MatchManager.EliminatePlayer()`.

**Recharge is cancelled by movement.** `RechargeSystem` locks movement, and any movement
input cancels the recharge — which also clears the reveal on every opponent's map.

**Two loot paths, one hierarchy.** `PickupManager` only spawns standard med kits, every
30–45s. Everything under `HighValuePickup` reaches players through the spaceship drop
instead, every 60–90s, one item per event.

---

## 0 · ROOT · SESSION + NETCODE

```mermaid
classDiagram
    direction LR
    class GameManager {
        -instance GameManager
        -matchManager
        -networkManager
        -uiManager
        +Initialize()
        +StartGame()
        +EndGame()
    }
    class NetworkManager {
        -isHost bool
        -isConnected bool
        -connectedPlayers
        +HostLobby()
        +JoinLobby(code)
        +Disconnect()
        +StartNetworkMatch()
        +SpawnNetworkPlayer()
    }
    class LobbyManager {
        -players
        -host NetworkPlayer
        -selectedFormat
        +CreateLobby()
        +JoinLobby()
        +LeaveLobby()
        +SelectFormat(format)
        +StartMatch()
    }
    class NetworkPlayer {
        -clientId ulong
        -playerId int
        -isOwner bool
        +SyncPosition()
        +SyncHealth()
        +SyncOxygen()
        +SyncState()
    }
    class MatchFormat {
        <<enumeration>>
        OneRound
        FT3 - 2 round wins
        FT5 - 3 round wins
    }
    class MatchStats {
        -matchesPlayed int
        -wins int
        -totalKills int
        -avgKillsPerMatch float
        +Record(result)
    }

    GameManager *-- NetworkManager
    NetworkManager o-- "4..6" NetworkPlayer
    NetworkManager --> LobbyManager
    LobbyManager o-- "4..6" NetworkPlayer
    LobbyManager --> MatchFormat
```

`GameManager` is the singleton root: it owns `MatchManager`, `NetworkManager` and
`UIManager`. `MatchStats` is marked *full version* — it is the persistent career record,
not part of a single match.

---

## 1 · MATCH & WORLD

```mermaid
classDiagram
    direction LR
    class MatchManager {
        -state MatchState
        -players List
        -currentRound int
        -maxPlayers 6
        -roundWins Dictionary
        +StartMatch()
        +StartRound()
        +EndRound()
        +CheckWinCondition()
        +EliminatePlayer(p)
        +EndMatch()
    }
    class MatchState {
        <<enumeration>>
        Waiting
        Starting
        InProgress
        RoundEnded
        MatchEnded
    }
    class SpawnManager {
        -spawnPoints List
        +GetRandomSpawnPoint()
        +SpawnPlayers(players)
    }
    class SpawnPoint {
        -position Vector3
        -rotation Quaternion
    }
    class ArenaMap {
        -gravityScale float
        -spawnPoints List
        -supplyDropPoint
        +GetSpawnPoints()
        +GetSupplyDropPoint()
    }
    class SupplyDropPoint {
        -position Vector3
        -isCentralParkour true
    }
    class Spaceship {
        -flyoverPath
        -isDelivering bool
        +Arrive()
        +DeliverDrop(point)
        +Leave()
    }

    MatchManager --> MatchState
    MatchManager --> SpawnManager
    SpawnManager o-- "4..6" SpawnPoint
    ArenaMap o-- "4..6" SpawnPoint
    ArenaMap *-- SupplyDropPoint
```

`MatchManager` is the round authority. `EliminatePlayer()` has exactly one caller in the
whole system: `HealthSystem`.

---

## 2 · PLAYER HUB · SPAWNERS

```mermaid
classDiagram
    direction LR
    class PlayerController {
        -playerId ulong
        -playerName string
        -isAlive bool
        -movement
        -health
        -oxygen
        -recharge
        -combat
        -inventory
        -upgrades
        +Initialize()
        +HandleInput()
        +TakeDamage()
        +OnEliminated()
    }
    class PickupManager {
        -spawnPoints List
        -medKitPrefab MedKit
        -intervalMin 30s
        -intervalMax 45s
        +SpawnRandomMedKit()
        +DespawnPickup()
    }
    class SupplyDropManager {
        -intervalMin 60s
        -intervalMax 90s
        -activeDrop
        -ship Spaceship
        +Start()
        +SpawnSupplyDrop()
        +EndSupplyDrop()
    }
    class SupplyDrop {
        -dropPosition Vector3
        -isOpened bool
        -reward
        +Open(player)
        +GetReward()
    }

    SupplyDropManager *-- SupplyDrop
```

`PlayerController` is the hub: it owns seven sub-systems by composition, so they are
created with it and die with it.

---

## 3 · SUIT SYSTEMS

```mermaid
classDiagram
    direction LR
    class PlayerMovement {
        -moveSpeed float
        -jumpForce float
        -isGrounded bool
        +Move(input)
        +Look(input)
        +Jump()
        +StopMovement()
    }
    class LowGravityController {
        -gravityScale float
        -fallMultiplier float
        +ApplyGravity()
        +GetGravityScale()
    }
    class HealthSystem {
        -maxHealth 100
        -currentHealth float
        +TakeDamage(amt, attacker)
        +Heal(amount)
        +IsDead()
    }
    class OxygenSystem {
        -secondsRemaining 120
        -drainPerSecond 1
        -starvationDmg 5 HP per s
        +Tick(deltaTime)
        +AddSeconds(amount)
        +SpendSeconds(amount)
        +CanSpend(amount)
        +IsDepleted()
    }
    class RechargeSystem {
        -gainRate plus 2s per 1s
        -isRecharging bool
        +StartRecharge()
        +CancelOnMove()
        +StopRecharge()
        +Tick(deltaTime)
    }
    class MapRevealSystem {
        -revealedPlayers List
        +RevealPlayer(p)
        +HidePlayer(p)
        +RevealNearestPlayer(p)
    }

    PlayerMovement --> LowGravityController
    OxygenSystem --> HealthSystem
    RechargeSystem --> OxygenSystem
    RechargeSystem --> PlayerMovement
    RechargeSystem --> MapRevealSystem
    PlayerMovement --> RechargeSystem
```

`OxygenSystem` has **no maximum cap** — that is what makes an oxygen tank worth taking even
at full health. The two arrows between `RechargeSystem` and `PlayerMovement` are the
cancel-on-move rule, one in each direction.

---

## 4 · COMBAT · INVENTORY · ECONOMY

```mermaid
classDiagram
    direction LR
    class CombatSystem {
        -currentWeapon Weapon
        -kills int
        +Shoot()
        +Reload()
        +EquipWeapon(weapon)
        +AddKill()
        +CanShoot()
    }
    class Weapon {
        -weaponId int
        -weaponName string
        -damage 20 HP
        -fireRate 5 per sec
        +Fire()
        +Reload()
        +CanFire()
    }
    class AmmoComponent {
        -currentAmmo int
        -magazineSize 20
        -reserveAmmo 60
        +Consume(amount)
        +Reload()
        +AddAmmo(amount)
        +IsEmpty()
    }
    class InventoryComponent {
        -slots List
        +AddItem(item)
        +RemoveItem(item)
        +HasItem(itemId)
        +GetItem(itemId)
    }
    class InventorySlot {
        -item Pickup
        -quantity int
        +IsEmpty()
    }
    class UpgradeController {
        -availableUpgrades
        -activeUpgrades
        +OpenShop()
        +CanAfford(u) bool
        +PurchaseUpgrade(u)
        +ActivateUpgrade(u)
        +RemoveUpgrade(u)
    }

    CombatSystem *-- Weapon
    Weapon *-- AmmoComponent
    InventoryComponent *-- "0..*" InventorySlot
```

---

## 5 · UPGRADE HIERARCHY

```mermaid
classDiagram
    direction TB
    class SuitUpgrade {
        <<abstract>>
        -upgradeId int
        -costOxygen float
        -duration float
        -isActive bool
        +Activate(player)
        +Deactivate(player)
    }
    class SpeedBoost {
        -cost 15s oxygen
        -speedMultiplier plus 30 percent
        -duration 10s
        +Activate(player)
        +Deactivate(player)
    }
    class DamageBoost {
        -cost 20s oxygen
        -damageMultiplier plus 25 percent
        -duration 10s
        +Activate(player)
        +Deactivate(player)
    }
    class PlayerScan {
        -cost 15s oxygen
        -scanDuration 5s
        +Activate(player)
    }

    SuitUpgrade <|-- SpeedBoost
    SuitUpgrade <|-- DamageBoost
    SuitUpgrade <|-- PlayerScan
```

Every upgrade is bought with oxygen seconds, which is why `UpgradeController` talks to
`OxygenSystem`. Each subclass reaches a different sub-system: speed, damage, or the map.

---

## 6 · PICKUP HIERARCHY

```mermaid
classDiagram
    direction TB
    class Pickup {
        <<abstract>>
        -pickupId int
        -description string
        +OnCollected(player)
    }
    class MedKit {
        -healAmount plus 25 HP
        +OnCollected(player)
    }
    class HighValuePickup {
        <<abstract>>
        -valueTier int
        one per drop event
    }
    class OxygenTank {
        -oxygenAmount plus 60s
        +OnCollected(player)
    }
    class SpecialWeapon {
        -weapon Weapon
        replaces current
        +OnCollected(player)
    }
    class LargeMedKit {
        -healAmount plus 60 HP
        +OnCollected(player)
    }

    Pickup <|-- MedKit
    Pickup <|-- HighValuePickup
    HighValuePickup <|-- OxygenTank
    HighValuePickup <|-- SpecialWeapon
    HighValuePickup <|-- LargeMedKit
```

Two loot paths, one hierarchy: `MedKit` comes from `PickupManager` every 30–45s, while
everything under `HighValuePickup` only arrives inside a `SupplyDrop`, one item per event.

---

## 7 · INTERFACE

```mermaid
classDiagram
    direction LR
    class UIManager {
        -hud HUDController
        -minimap
        -panels
        -messages
        +Initialize()
        +ShowMessage(msg)
    }
    class MenuManager {
        -current ScreenState
        +OpenMainMenu()
        +OpenHostJoin()
        +OpenLobbyScreen()
        +ReturnToMenu()
    }
    class HUDController {
        +UpdateHealth(value)
        +UpdateOxygen(value)
        +UpdateAmmo(cur, res)
        +UpdateKills(value)
        +ShowRechargeState()
        +ShowUpgradeStatus()
        +ShowRevealWarning()
    }
    class MinimapController {
        +ShowPlayer(p)
        +RevealPlayer(p)
        +HidePlayer(p)
        +ShowSupplyDrop(pos)
        +ShowNearestPlayer(p)
    }
    class PanelManager {
        +OpenUpgradePanel()
        +CloseUpgradePanel()
        +OpenVictoryScreen()
        +OpenDefeatScreen()
        +OpenResultsScreen()
    }
    class MessageSystem {
        +ShowWarning(msg)
        +ShowInfo(msg)
        +ShowKillFeed(msg)
    }

    UIManager *-- HUDController
    UIManager *-- MinimapController
    UIManager *-- PanelManager
    UIManager *-- MessageSystem
    UIManager *-- MenuManager
```

---

## Every relation

**Composition ◆** — the owner creates the part and the part dies with it.

| Owner | Part | Multiplicity |
| --- | --- | --- |
| `GameManager` | `MatchManager` | 1 |
| `GameManager` | `NetworkManager` | 1 |
| `GameManager` | `UIManager` | 1 |
| `ArenaMap` | `SupplyDropPoint` | 1 |
| `PlayerController` | `PlayerMovement` | 1 |
| `PlayerController` | `HealthSystem` | 1 |
| `PlayerController` | `OxygenSystem` | 1 |
| `PlayerController` | `RechargeSystem` | 1 |
| `PlayerController` | `CombatSystem` | 1 |
| `PlayerController` | `InventoryComponent` | 1 |
| `PlayerController` | `UpgradeController` | 1 |
| `CombatSystem` | `Weapon` | 1 |
| `Weapon` | `AmmoComponent` | 1 |
| `InventoryComponent` | `InventorySlot` | 0..* |
| `SupplyDropManager` | `SupplyDrop` | 1 |
| `SupplyDropManager` | `Spaceship` | 1 |
| `SupplyDrop` | `HighValuePickup` | 1 |
| `UIManager` | `HUDController` | 1 |
| `UIManager` | `MinimapController` | 1 |
| `UIManager` | `PanelManager` | 1 |
| `UIManager` | `MessageSystem` | 1 |
| `UIManager` | `MenuManager` | 1 |

**Aggregation ◇** — the holder keeps a reference but does not own the lifetime.

| Holder | Item | Multiplicity |
| --- | --- | --- |
| `NetworkManager` | `NetworkPlayer` | 4..6 |
| `LobbyManager` | `NetworkPlayer` | 4..6 |
| `MatchManager` | `PlayerController` | 4..6 |
| `SpawnManager` | `SpawnPoint` | 4..6 |
| `ArenaMap` | `SpawnPoint` | 4..6 |
| `UpgradeController` | `SuitUpgrade` | 0..* |
| `PickupManager` | `MedKit` | 0..* |

**Inheritance ▷** — "is a".

| Child | Parent |
| --- | --- |
| `SpeedBoost` | `SuitUpgrade` |
| `DamageBoost` | `SuitUpgrade` |
| `PlayerScan` | `SuitUpgrade` |
| `MedKit` | `Pickup` |
| `HighValuePickup` | `Pickup` |
| `OxygenTank` | `HighValuePickup` |
| `SpecialWeapon` | `HighValuePickup` |
| `LargeMedKit` | `HighValuePickup` |

**Association →** — uses, but does not hold.

| From | To | What it is |
| --- | --- | --- |
| `NetworkManager` | `LobbyManager` | lobby lifecycle |
| `NetworkManager` | `MatchManager` | starts the match |
| `LobbyManager` | `MatchFormat` | one round / FT3 / FT5 |
| `LobbyManager` | `MatchManager` | hands the lobby over |
| `NetworkPlayer` | `PlayerController` | 1, the local avatar |
| `MatchManager` | `MatchState` | current phase |
| `MatchManager` | `SpawnManager` | spawns the round |
| `MatchManager` | `SupplyDropManager` | starts the drop cycle |
| `MatchManager` | `PanelManager` | victory / defeat / results |
| `MatchManager` | `MatchStats` | records the result |
| `ArenaMap` | `LowGravityController` | map gravity scale |
| `PlayerMovement` | `LowGravityController` | applies gravity |
| `PlayerMovement` | `RechargeSystem` | movement cancels recharge |
| `RechargeSystem` | `PlayerMovement` | recharge locks movement |
| `RechargeSystem` | `OxygenSystem` | +2s per 1s |
| `RechargeSystem` | `CombatSystem` | cannot shoot while recharging |
| `RechargeSystem` | `MapRevealSystem` | recharging reveals you |
| `OxygenSystem` | `HealthSystem` | 5 HP/s once empty |
| `HealthSystem` | `MatchManager` | **the only call to EliminatePlayer** |
| `CombatSystem` | `HealthSystem` | applies damage |
| `Weapon` | `HealthSystem` | 20 HP per shot |
| `InventorySlot` | `Pickup` | 1, the item held |
| `UpgradeController` | `OxygenSystem` | pays for upgrades |
| `UpgradeController` | `PanelManager` | opens the shop |
| `SpeedBoost` | `PlayerMovement` | +30% speed |
| `DamageBoost` | `CombatSystem` | +25% damage |
| `PlayerScan` | `MapRevealSystem` | reveals nearest |
| `PlayerScan` | `MinimapController` | draws the marker |
| `MedKit` | `HealthSystem` | +25 HP |
| `LargeMedKit` | `HealthSystem` | +60 HP |
| `OxygenTank` | `OxygenSystem` | +60s |
| `SpecialWeapon` | `CombatSystem` | replaces the weapon |
| `SupplyDropManager` | `SupplyDropPoint` | where it lands |
| `SupplyDropManager` | `MinimapController` | marks the drop |
| `SupplyDropManager` | `UIManager` | announces the drop |
| `Spaceship` | `SupplyDrop` | delivers it |
| `MapRevealSystem` | `MinimapController` | draws reveals |
| `MenuManager` | `LobbyManager` | host / join screens |
| `HealthSystem` | `HUDController` | health readout |
| `OxygenSystem` | `HUDController` | oxygen timer |
| `AmmoComponent` | `HUDController` | ammo counter |
| `RechargeSystem` | `HUDController` | recharge state |
| `UpgradeController` | `HUDController` | active upgrade status |
| `CombatSystem` | `HUDController` | kill counter |

---

## Reading the notation

| Symbol | Name | Means | In C# |
| --- | --- | --- | --- |
| ◆ filled diamond | Composition | owns it, dies with it | field created with `new` |
| ◇ hollow diamond | Aggregation | holds it, does not own it | a list passed in from outside |
| ▷ hollow triangle | Inheritance | "is a" | `class Child : Parent` |
| → open arrow | Association | uses it | a method call on a reference |

The interactive version of this diagram, where clicking a class isolates everything it
touches, lives at
<https://claude.ai/code/artifact/c2c7d8f3-ce3a-41e0-a6a0-46082d1bb0af>.
