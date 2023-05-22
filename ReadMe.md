ASCOM Telescope Driver for TTS-160

ASCOM Driver created specifically for the TTS-160 mount

This driver is written for the TTS-160 Panther telescope.  In part, it uses code adapted from the Meade LX200 Classic Driver (https://github.com/kickitharder/Meade-LX200-Classic--ASCOM-Driver) as noted in inline comments within the code.  The implementation includes some simulations and estimations required due to the limited implementation of the LX200 protocol by the mount in an attempt to maximize the methods available to programs while maintaining as close as possible to the ASCOM philosophy of reporting actual truth of hardware state.

The driving force behind this driver was due to the issues surrounding the other available drivers in use which were general LX200 implementations, particularly felt when ASCOM 6.6 was released near the end of 2022.  This driver is intended to be able to be maintained by the Panther community to prevent those issues from occurring in the future (or at least corrected more quickly!).

Please note that the current implementation of the driver will only support one program to use it at a time (only NINA, not NINA and PHD2, for example).  In order to allow multiple programs to interface with the mount, use the ASCOM Device Hub driver within those programs, and configure Device Hub to use this driver.  There is a notional plan to recreate the driver as a local server, which would correct the allow for that multiple access capability at some point in the future, but for now I have taken steps to ensure that this driver plays well with Device Hub.  Short answer for why I chose this path is that the local server is a more difficult implementation and at the start of the project I had no experience in .NET development, COM development, ASCOM development, use of Visual Studio, or the C# language, barring the sorta/kinda similarity it has to C/Python/MATLAB.  So definitely a case where perfection would have very much gotten in the way of good enough.

Some system characteristics are hard-coded for now, such as SlewSettleTime (set at 2 seconds), future revisions will allow changing those values from the Setup dialog, which right now is limited to selection of the COM port and choosing whether to enable the trace logger (used for debugging)
The MoveAxis capability of the mount is limited to moving at the tracking rate set by the handpad, and tracking rates, slewing rates, and turning tracking on and off are not able to be set by the driver.  This limits the adjustments that can be made for pulse guiding as those are implemented using MoveAxis.

The source code for the driver can be found at: https://github.com/rwsmythe/TTS-160-ASCOM-Driver

Enjoy, and let me know if you have any issues.

--Reid
