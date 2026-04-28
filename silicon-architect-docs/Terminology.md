# Terminology

## Why This Exists

The project started as a motherboard simulation and pivoted toward a city-builder / idle-merge game. To avoid unnecessary breakage, the code still uses some legacy technical names internally while the player-facing language has been shifted toward city terms.

## Player-Facing Terms

| Internal Concept | Player-Facing Term |
| --- | --- |
| `Transistor` | Home |
| `LogicGate` | Apartment |
| `Processor` | Commercial Hub |
| `PowerRail` | Power Grid |
| `Fan` | Park |
| `Heat` | Pollution |
| `Data` / output | Income |
| total currency | Cash |

## Rule

If text is visible to the player, it should come from the city-facing language.

## Implementation Note

`CityTerminology.cs` is the single source of truth for player-facing strings.

## Safe vs Risky Renames

### Safe

- HUD text
- status messages
- tooltip labels
- private method names
- local variable names

### Risky

- exported field names
- enum names used across scenes/resources
- inspector-bound node names unless checked carefully

## Guidance

For v1, prefer translation rather than aggressive renaming.