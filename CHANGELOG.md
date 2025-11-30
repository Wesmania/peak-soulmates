# Changelog

## V0.1.0

Release

## V0.1.8

First kind of working version

## V0.1.9

* Split things up into files
* Hopefully fix weight calculations
* Hack around morale boost function being called hundreds of times

## V0.1.10

* Fixed picking new soulmates at campfire
* Less spammy and more useful logs

## V0.1.12

* Experimental features: sharing bonks, extra stamina, lolliopos and energy drinks

## V0.1.17

* Sharing slipping.
* Test status:
  * Recalculating soulmates at campfire doesn't work sometimes.
  * Receiving heat stops you from getting cold at night?

## V0.2.0

* Don't try to synchronize status when soulmate schange. Too complex, and
  probably unintuitive for players.
* Use Photon's inMasterClient to decide who should recalculate soulmates.

## V0.2.1

* Probably fix incorrect weights/thorns calculations.

## V0.2.2

* Soulmate color is now green.
* Fixed add/subtract messages sent on recursive calls. Probably doesn't fix the heat bug.

## V0.2.3

* Fixed the heat bug. Problem was with arguments to postfix being modified by the original function.

## V0.2.4

* Bigger soulbound groups.
* Configurable soulbound effect strength.
* Unretardate configuration file.
* Mark each soulbound group with a nick color. Your soulmates are always green.
* Maybe fix the jittery weight/thorns bug.

## V0.2.5

* Fixed the most obvious bugs.

## V0.2.6

* Added a message cache for tiny updates. Hopefully the mod will stop spamming
  a bajillion messages per second now. Should help with the lag I've seen other
  players have.

## V0.2.7

* Updated the mod just enough to successfully load in the Roots update. Full
  check-up pending.

## V0.2.8

* Change most Photon messages to unreliable. This might help with some players
  lagging a lot.
* New mod icon. Thank you, /v/!
