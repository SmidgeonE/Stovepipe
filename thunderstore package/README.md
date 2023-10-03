# Double Feeds

https://youtu.be/pm3irC9ZwD8 - Video of Double Feeds.

Newly added double feeds function for all closed bolt weapons, ARs/BRs/SMGs. The position is procedural, so may not be perfect in places. There are multiple steps to remedying a double feed failure:

1. Remove the magazine.

2. Rack the bolt back, to see if the bullets fall out on their own. It is possible only the lower bullet / none of the bullets will fall out.

3. If this doesn't work, you can rack the bolt + shake the firearm, this may also only do the lower bullet, or none at all.

4. Finally, if all else fails, you can manually remove the bullets my interacting with them.


All of these steps have probabilities associated with them, which may be edited in the config file, namely smidgeon.failuretoeject.cfg.

I do plan on adding this to handguns as well in future.

# Note on Double Feeds

If you think the process of removing the bullets manually is too much effort, you can simply put the probabilities in the config file to 1, so you'll never have to remove them by hand.



# Stovepipes

https://www.youtube.com/watch?v=VNYMUrb8CqU - New debug mode.
https://youtu.be/OUB9Hz1-eLw - New stovepipe manipulations.

New default positions were added thanks to @drummerdude2003, for 160+ guns in the vanilla game!

You can now grab the stovepiped round with your hand, as well as get rid of it using inertia.

For ARS/SMGS/BRs, you have to make sure the stovepiped bullet can actually fall out the gun (i.e. its facing towards the floor or it can fall out with inertia). Otherwise, it will not fall out. You can now also grab the bullet.

If you want any changes to any of the positions, DM me (details below).


This is a small mod that implements stovepipe failures for guns. Currently in developement. You can change the probability as your heart desires.

As far as I know, there is no compatibility issue with Meatyceiver.

Some of the bullet poses are slighlty odd (may clip through weapon slightly), because parts of this mod are very hacky :)



# Debug mode

There is now a debug mode, this allows you to update the position / rotation of the bullet as well as allowing you to change the point where the slide stops. To access this, you need to change the failuretoeject.cfg in the config folder in thunderstore, specifically where it says "isDebug" to true.

Next, once you are holding a firearm, you may press right on the joystick you are using to hold the weapon.

This will spawn a debug bullet, you can move this around, you may also manipulate the slide. In debug mode it will stay where it is once you let go. 

To save this, simply press down on the right joystick again and it will save it to User/./AppData/Roaming/StovepipeData/userdefinitions.json.

Currently works for:

   (Stovepipes)			    
1. Closed Bolt Weapons		
2. Handguns		
3. Tube Fed Shotguns
4. Open Bolt Weapons


# Note with Debug mode

If you let go of the bullet while its in debug mode, you won't be able to move it again, due to limitations of the physics logic. If you mess up the position, simply restart the debug with joystick right + down.

I will likely add to the defaults.json file, which will improve the default positions over time.



# Contact

If you wish to support me, my kofi is https://ko-fi.com/smidgeon

If you have any issues / ideas / need help with modding, I am always available on discord in the homebrew server under the name Smidgeon, tag ප bir𝛿 ꧁꧂#9320 (not sure if thunderstoreeven supports those characters).



# Changelog

3.1.7 - Excluded ModulMX, as they can't stovepipe.

3.1.6 - Fixed double feeding issue with caseless weapons.

3.1.5 - Fixed yet another bug.

3.1.4 - Fixed another bug.

3.1.3 - Fixed multiple bugs. Can now discard changes in debug mode by pressing upwards.

3.1.2 - Added probability creep, this modifies the probability so its less likely to jam again right after another jam, this can be adjusted in the config, as well as a flat-out stopping of any jams after a certain number of shots after it has already jammed.

3.1.1 - Added stovepipes to open bolt weapons (with debug). Fixed issue with double-feeding on integrated magazine weapons. Lots of new high-quality default positions for handguns, and shotguns thanks to @drummerdude2003.

3.1.0 - Added Double Feeds to Handguns. Added Stovepipes to Tube Fed Shotguns. Added Handgun Debug Mode. Added Tube Fed Shotgun Debug Mode.

3.0.1 - Fixed issue with lower bullet falling back into magazine, causing an error.

3.0.0 - Added double-feed failures to closed bolt weapons. Added sound effect for manually removed stovepipe rounds.

2.2.3 - Fixed bug where slide moves forward after ungrabbing in debug mode.

2.2.2 - New default positions created with debug tool. Adjusted default probabilities slightly higher.

2.2.1 - Fixed description + small bug.

2.2.0 - Added debug mode, so you may customise the positions as you please. It also persists so once you've changed it, it will stay! If you want to remove your changes, simply go to User/./AppData/Roaming/Stovepipe/userdefinitions.json and delete the entries you don't like, or delete the entire file if desired.

2.1.2 - Updated description. Don't use new debug mode as it doesn't work yet.

2.1.1 - Fixed crash source

2.1.0 - Added ability to grab the bullet + Use inertia to unstovepipe. Changed default values so they are now lower.

2.0.4 - Changed desc.

2.0.3 - Adjusted so that bullets now take the momentum of the gun after being unstovepiped (so they dont just fly straight down)

2.0.2 - Fixed weird bug where bullet goes crazy on some guns.

2.0.1 - Rifle and Handgun probabilities can now be changed separately. Previous user values should be inherited. Fixed so bullet can fall out when bolt is locked back (i.e. with mp5)

2.0.0 - Expanded to work with closed bolt weapons.

1.1.6 - Fixed bug with reloading scenes.

1.1.5 - Update positions so they are now much better. Also added special case where the bullets eject to the left of the gun.

1.1.4 - Properly fixed issue with caseless ammo :)

1.1.3 - Fixed issue with caseless ammo.

1.1.2 - Fixed more bugs.

1.1.1 - Fixed potential crash source.

1.1.0 - Added ability to un-stovepipe gun with physics interactions (i.e. racking slide with environment). Also tweaked bullet position logic slightly, should be better on most guns (some did get slightly worse though lol)

1.0.0 - Initial release, only works for handguns :)


