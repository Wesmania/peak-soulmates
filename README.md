# Soulmates

A multiplayer mod that matches players into pairs, where each player in a pair shares damage of the other. Based on the "shared damage" mod.

## Features

Implemented:

* Sharing damage!
* Sharing hunger and weight!
* Sharing healing!
* Sharing bonks! (configurable)
* Sharing extra stamina gain! (configurable)
* Sharing extra stamina USE! (configurable)
* Shared lollipops and energy drinks! (configurable)
* Shared slipping! (configurable)
* New soulmates chosen at every biome!

Implemented, but maybe buggy:

* Players that disconnect and rejoin should re-discover what their soulmate is from other players.
* Collecting players into larger soulbound groups than 2 players! (configurable)
* Sharing soulmate damage/healing at a different rate than one-to-one! (configurable)

## Planned features

* Logo that's not a half-assed "shared damage" ripoff.
* Fix bugs.

## Installation

1. Extract the mod folder into `BepInEx/plugins`

## Configuration

1. Run the mod once.
2. Open and edit the file `BepInEx/config/com.github.Wesmania.Soulmates.cfg` in PEAK's directory.

## Multiplayer Compatibility

This mod uses custom network events. Because of this it's best that all players in the lobby have the mod installed. If some players don't have the mod, weird and bad things will probably happen.
