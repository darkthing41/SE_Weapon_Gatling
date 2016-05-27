# SE_Weapon_Gatling
Space Engineers: Controller for gatling gun.

> This project is configured to be built within a separate IDE to allow for error checking, code completion etc..
> Only the section in `#region CodeEditor` should be copied into the Space Engineers editor. This region has been automatically extracted into the corresponding `txt` file.

##Description
Attempts to fire Gun `n` when it is within some tolerance of the target Drive Rotor angle.
Assuming the guns are evenly spaced and sequentially named, this will fire all guns from the same position.

Additionally, it will not fire the same gun twice in a row.
This allows it to sequentially fire guns regardless of direction of rotation.

To work on a gatling gun with any real speed, this script should be run at the full physics simulation speed of 60Hz.
This may be achieved by setting a Timer Block to "Trigger [self]" and "Run [program]".

##Hardware
| Block(s)      | number        | Configurable  |
| ------------- | ------------- | ------------- |
| Drive Rotor   | single        | by name constant
| Guns          | n             | by name and count constants

##Configuration
+ `nameRotorDrive`: the name of the Rotor which rotates the gatling gun body
+ `nameGunPrefix`: the name shared by all guns that are controlled by the script
+ `gunCount`: the number of guns controlled by the script
+ `angleTarget`: the angle (in Radians) of the Drive Rotor at which the guns should fire
+ `angleTolerance`: how far from `angleTarget` the guns are allowed to be fired

##Standard Blocks
+ `FindBlock()`: find blocks during setup
+ `ValidateBlock()`: check that found blocks are usable
+ Status/Initialise/Validate framework