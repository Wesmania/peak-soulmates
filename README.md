# SharedDamage

A multiplayer mod that shares damage between all players who have it installed.

## Features

- Damage taken by any player is transferred to all other players additively
- Configurable damage types (Poison, Injury, Thorns, Cold, Hot, Drowsy, Curse)
- Hunger and Weight are never shared
- Only adds damage, never removes it (healing and recovery remains individual)

## Configuration

Each player configures which affliction types they want to **receive** from others. This does not affect what you send.

If you take fire damage with "EnableHot" enabled, it will transfer to all other players with the mod, regardless of their config settings. However, if another player has "EnableHot" disabled, they will not receive fire damage from others.

## Installation

1. Extract the mod folder into `BepInEx/plugins`

## Multiplayer Compatibility

This mod uses custom network events. Because of this it's best that all players in the lobby have the mod installed. Players without the mod will not send or receive shared damage.