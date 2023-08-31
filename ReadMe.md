ASCOM Telescope Driver for TTS-160

ASCOM Driver created specifically for the TTS-160 mount

This driver is written for the TTS-160 Panther telescope.  In part, it uses code adapted from the Meade LX200 Classic Driver (https://github.com/kickitharder/Meade-LX200-Classic--ASCOM-Driver) as noted in inline comments within the code.  The implementation includes some simulations and estimations required due to the limited implementation of the LX200 protocol by the mount in an attempt to maximize the methods available to programs while maintaining as close as possible to the ASCOM philosophy of reporting actual truth of hardware state.

The driving force behind this driver was due to the issues surrounding the other available drivers in use which were general LX200 implementations, particularly felt when ASCOM 6.6 was released near the end of 2022.  This driver is intended to be able to be maintained by the Panther community to prevent those issues from occurring in the future (or at least corrected more quickly!).

Please note that the current implementation of the driver will only support one program to use it at a time (only NINA, not NINA and PHD2, for example).  In order to allow multiple programs to interface with the mount, use the ASCOM Device Hub driver within those programs, and configure Device Hub to use this driver.  There is a notional plan to recreate the driver as a local server, which would correct the allow for that multiple access capability at some point in the future, but for now I have taken steps to ensure that this driver plays well with Device Hub.  Short answer for why I chose this path is that the local server is a more difficult implementation and at the start of the project I had no experience in .NET development, COM development, ASCOM development, use of Visual Studio, or the C# language, barring the sorta/kinda similarity it has to C/Python/MATLAB.  So definitely a case where perfection would have very much gotten in the way of good enough.

The manual slew rate has 3 speed options (1.4, 2.2, 3 deg/sec) as well as a much slower speed, the actual value of which is dependent on the guide rate setting of the hand pad.  For that case, the displayed rate may not be reflective of the actual manual slew rate.

Also note that the mount returns site location only to the nearest minute.  So if your astronomy program is reporting a different site location than you have entered (and verified) in your handpad, that precision limitation is likely the culprit.

To foot stomp the point: this driver will NOT change the mount's location as set in the handpad, regardless of what you see in any logs or display!  The site altitude/elevation, however, is tracked in the driver and CAN be changed.

Another source of potentially strange behavior is if you go off of the operations screen in the handpad while the mount is doing stuff.  Currently, remote communications from the handpad only happen in the operations screen.  If something is not working right, first step should be to completely disconnect (from all programs and from the ASCOM device chooser, if used) and reconnect the mount.

Lastly, be aware that the mount will not resume tracking if a slew is canceled (it will resume tracking when a slew is completed).  If tracking does not seem to be working correctly, even if the program you are using indicates that tracking is enabled, use the handpad to turn it off, then turn it on (I go one step further and return to the operations menu between the off and on steps).  That has always fixed the issue in my testing.

The source code for the driver can be found at: https://github.com/rwsmythe/TTS-160-ASCOM-Driver

Enjoy, and let me know if you have any issues.

--Reid

----------------------------------------

Setup Window Clarifications:

Site altitude is used to report site altitude as needed.  If you change site location in the mount, you may also want to change the site altitude in the driver.  Please not that programs can NOT update the site location (lat long) stored in the handpad, however adjustments to altitude CAN be stored.  This is program dependent and may result in somewhat erratic behavior if you try to update the mount location from a program.

Slew settling time is essentially a wait period before the mount reports that a slew is complete (generally determined by the mount returning to a tracking state).  There is typically a short settling time after the mount starts tracking before it has settled.  This may be useful to set to a larger value to account for rOTAtor motion if you don't want a slew to be considered complete until after the rOTAtor is finished moving.  Limited to integer seconds based on ASCOM compliance.

Recorded site lat/long are read-only fields that show what the handpad location was set to the last time the driver read the site location.  If it has changed between readings then you will see the old location until you have connected the mount and that location is refreshed.

App Compatibility Mode strategically "breaks" ASCOM compliance in benign ways to support specific programs (in this case, Moon Panorama Maker being the only one coded for, so far).  Selecting 'none' will restore ASCOM compliance.  This may result in occasional strange behavior in other programs if they try to execute specific functions.  If you see that happen and this option is set to MPM, just set it to NONE and reconnect the mount.

Sync Mount Time to Computer on Connect is self explanatory.  There may be situations where you do not wish for this to occur.  If that is the case, simply uncheck the box.  Note that this syncs both time and date and is based off of the time zone correction on the handpad being correct.  This should prevent, for example, NINA warnings about time mismatch and it should improve goto and tracking in general, assuming your computer has accurate time.

Guiding Compensation allows for E/W pulse guides to compensate for altitude which should result in better guiding.  Note that PHD2 by default has a pulse-time + 1 sec limit.  Compensated pulses greater than that limit may cause PHD2 to fail guiding.  The buffer option is used to calculate a maximum corrected pulse duration to prevent exceeding the 1 second limit due to the command rate of PHD2 when querying pulse command status.