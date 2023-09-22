//tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM Telescope driver for TTS-160
//
// Description:	This driver is written for the TTS-160 Panther telescope.  In part, it uses code adapted
//              from the Meade LX200 Classic Driver (https://github.com/kickitharder/Meade-LX200-Classic--ASCOM-Driver)
//              as noted in inline comments within the code.  The implementation includes some
//              simulations and estimations required due to the limited implementation of the LX200 protocol
//              by the mount in an attempt to maximize the methods available to astro programs while
//              maintaining as close as possible to the ASCOM philosophy of reporting actual truth.
//
//              The driving force behind this driver was due to the issues surrounding the other available
//              drivers in use which were general LX200 implementations, particularly felt when ASCOM 6.6 was released
//              near the end of 2022.  This driver is intended to be able to be maintained by the Panther community
//              to prevent those issues from occuring in the future (or at least corrected more quickly!).
//
// Implements:	ASCOM Telescope interface version: 3
// Author:		Reid Smythe <rwsmythe@gmail.com>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// 31AUG2023    RWS 353.0.0 Adding features for 353 firmware
// 21JUL2023    RWS 1.0.1   Added in selectable slew speeds
// 06JUL2023    RWS 1.0.1RC4 Added in guiding compensation in azimuth based off of target altitude
// 13JUN2023    RWS 1.0.1RC1 Troubleshooting missing pulseguide command and apparently stuck IsPulseGuiding value
// 09JUN2023    RWS 1.0.0   First release version
// 08JUN2023    RWS 0.9.5   Added in App Compatability feature for MPM and time sync feature
// 03JUN2023    RWS 0.9.4   Added in capability to add site elevation and adjust Slew Settling Time in setup dialog
// 29MAY2023    RWS 0.9.3   Corrected issues in Sync, UTCDate, SiderealTime, MoveAxis, and AxisRates
// 23MAY2023    RWS 0.9.1   Added in the native PulseGuide commands
// 21MAY2023    RWS 0.9.0   Passed ASCOM Compliance testing, ready to begin field testing
// 26APR2023    RWS 0.0.2   Further feature addition, commenced use of MiscResources
//                          in part to simulate features normally done in hardware
// 15APR2023	RWS	0.0.1	Initial edit, created from ASCOM driver template
// --------------------------------------------------------------------------------
//


// This is used to define code in the template that is specific to one class implementation
// unused code can be deleted and this definition removed.
#define Telescope

using ASCOM;
using ASCOM.Astrometry;
using ASCOM.Astrometry.AstroUtils;
using ASCOM.Astrometry.Transform;
using ASCOM.Astrometry.NOVAS;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.IO.Ports;
using System.Security.Cryptography;
using ASCOM.DriverAccess;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing.Text;
using System.Runtime.Serialization.Formatters;
using System.Diagnostics.Eventing.Reader;
using System.Windows.Forms;
using System.Linq;
using System.Reflection;

namespace ASCOM.TTS160
{
    //
    // Your driver's DeviceID is ASCOM.TTS160.Telescope
    //
    // The Guid attribute sets the CLSID for ASCOM.TTS160.Telescope
    // The ClassInterface/None attribute prevents an empty interface called
    // _TTS160 from being created and used as the [default] interface
    //
    //

    /// <summary>
    /// ASCOM Telescope Driver for TTS160.
    /// </summary>
    [Guid("cd042e76-0a96-4c79-be7c-16fc38264c44")]
    [ClassInterface(ClassInterfaceType.None)]
    public class Telescope : ITelescopeV3
    {
        /// <summary>
        /// ASCOM DeviceID (COM ProgID) for this driver.
        /// The DeviceID is used by ASCOM applications to load the driver at runtime.
        /// </summary>
        internal static string driverID = "ASCOM.TTS160.Telescope";
        /// This driver is intended to specifically support TTS-160 Panther mount, based on the LX200 protocol.
        /// Driver description that displays in the ASCOM Chooser.
        /// </summary>
        private static string driverVersion = "353.0.0b4";
        private static string driverDescription = "TTS-160 v." + driverVersion;
        private Serial serialPort;

        internal static string comPortProfileName = "COM Port"; // Constants used for Profile persistence
        internal static string comPortDefault = "COM1";
        internal static string traceStateProfileName = "Trace Level";
        internal static string traceStateDefault = "false";

        internal static string comPort; // Variables to hold the current device configuration

        internal static string siteElevationProfileName = "Site Elevation";
        internal static string siteElevationDefault = "0";
        internal static string SlewSettleTimeName = "Slew Settle Time";
        internal static string SlewSettleTimeDefault = "2";
        internal static string SiteLatitudeName = "Site Latitude";
        internal static string SiteLatitudeDefault = "100";
        internal static string SiteLongitudeName = "Site Longitude";
        internal static string SiteLongitudeDefault = "200";
        internal static string CompatModeName = "Compatibility Mode";
        internal static string CompatModeDefault = "0";
        internal static string CanSetGuideRatesOverrideName = "CanSetGuideRates Override";
        internal static string CanSetGuideRatesOverrideDefault = "false";
        internal static string SyncTimeOnConnectName = "Sync Time on Connect";
        internal static string SyncTimeOnConnectDefault = "true";
        internal static string GuideCompName = "Guiding Compensation";
        internal static string GuideCompDefault = "0";
        internal static string GuideCompMaxDeltaName = "Guiding Compensation Max Delta";
        internal static string GuideCompMaxDeltaDefault = "1000";
        internal static string GuideCompBufferName = "Guiding Compensation Buffer";
        internal static string GuideCompBufferDefault = "20";
        internal static string TrackingRateOnConnectName = "Tracking Rate on Connect";
        internal static string TrackingRateOnConnectDefault = "0";

        internal static int MOVEAXIS_WAIT_TIME = 2000; //minimum delay between moveaxis commands

        /// <summary>
        /// Private variable to hold the connected state
        /// </summary>
        private bool connectedState;

        /// <summary>
        /// Private variable to hold an ASCOM Utilities object
        /// </summary>
        private Util utilities;

        /// <summary>
        /// Private variable to hold an ASCOM AstroUtilities object to provide the Range method
        /// </summary>
        private AstroUtils astroUtilities;

        /// <summary>
        /// Variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
        /// </summary>
        internal TraceLogger tl;

        // object used for locking to prevent multiple drivers accessing common code at the same time
        private static readonly object LockObject = new object();

        /// <summary>
        /// Variable to provide coordinate Transforms
        /// </summary>
        private readonly Transform T;

        ///<summary>
        ///Accessible profile to apply changes to
        /// </summary>
        private ProfileProperties profileProperties = new ProfileProperties();

        /// <summary>
        /// Initializes a new instance of the <see cref="TTS160"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Telescope()
        {
            tl = new TraceLogger("", "TTS160 v. " + driverVersion);
            profileProperties = ReadProfile();
            tl.LogMessage("Telescope", "Starting initialization");

            connectedState = false; // Initialise connected to false
            utilities = new Util(); //Initialise util object
            astroUtilities = new AstroUtils(); // Initialise astro-utilities object
            T = new Transform();
            Slewing = false;

            //TODO: Implement your additional construction here

            tl.LogMessage("Telescope", "Completed initialization");
        }


        //
        // PUBLIC COM INTERFACE ITelescopeV3 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog()
        {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            if (IsConnected)
                System.Windows.Forms.MessageBox.Show("Already connected, just press OK");

            ReadProfile();

            using (SetupDialogForm F = new SetupDialogForm(tl))
            {
                F.SetProfile(profileProperties);
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    ProfileProperties currentProfile = F.GetProfile( profileProperties );
                    WriteProfile( currentProfile ); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        /// <summary>Returns the list of custom action names supported by this driver.</summary>
        /// <value>An ArrayList of strings (SafeArray collection) containing the names of supported actions.</value>
        public ArrayList SupportedActions
        {
            get
            {
                tl.LogMessage("SupportedActions Get", "Returning arraylist");
                return new ArrayList()
                {
                    "FieldRotationAngle"
                };
            }
        }

        /// <summary>Invokes the specified device-specific custom action.</summary>
        /// <param name="ActionName">A well known name agreed by interested parties that represents the action to be carried out.</param>
        /// <param name="ActionParameters">List of required parameters or an <see cref="String.Empty">Empty String</see> if none are required.</param>
        /// <returns>A string response. The meaning of returned strings is set by the driver author.
        /// <para>Suppose filter wheels start to appear with automatic wheel changers; new actions could be <c>QueryWheels</c> and <c>SelectWheel</c>. The former returning a formatted list
        /// of wheel names and the second taking a wheel name and making the change, returning appropriate values to indicate success or failure.</para>
        /// </returns>
        public string Action(string actionName, string actionParameters)
        {
            tl.LogMessage("Action", "Action: " + actionName + "; Parameters: " + actionParameters);
            try
            {

                CheckConnected("Action");

                actionName = actionName.ToLower();
                switch (actionName)
                {

                    case "fieldrotationangle":
                        tl.LogMessage("Action", "FieldRotationAngle - Retrieving FieldRotationAngle");
                        var result = Commander(":ra#", true, 2);
                        tl.LogMessage("Action", "FieldRotationAngle - Retrieved String: " + result);
                        return result;

                    default:
                        throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");


                }

            }
            catch (Exception ex)
            {
                tl.LogMessage("Action", $"Error: {ex.Message}");
                throw;
            }

        }

        public string Commander(string command, bool raw, int commandtype)
        {
            lock (LockObject)
            {
                try
                {
                    switch (commandtype)
                    {
                        case 0:
                            CommandBlind(command, raw);
                            return "";

                        case 1:
                            bool resultbool = CommandBool(command, raw);
                            return resultbool.ToString();

                        case 2:
                            string resultstr = CommandString(command, raw);
                            return resultstr;

                        default:
                            throw new ASCOM.DriverException("Invalid Command Type: " + commandtype.ToString());
                    }
                }
                catch (Exception ex)
                {
                    tl.LogMessage("Commander", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and does not wait for a response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        public void CommandBlind(string command, bool raw)
        {

            try
            {

                CheckConnected("CommandBlind");
                CheckParked("CommandBlind");

                tl.LogMessage("CommandBlind", $"raw: {raw} command {command}");

                if (!raw) { command = ":" + command + "#"; }

                serialPort.ClearBuffers();
                serialPort.Transmit(command);
                tl.LogMessage("CommandBlind", $"{command} Completed");
            }
            catch (Exception ex)
            {
                tl.LogMessage("CommandBlind", $"Error: {ex.Message}; Command: {command}");
                throw;
            }
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a boolean response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the interpreted boolean response received from the device.
        /// </returns>
        public bool CommandBool(string command, bool raw)
        {

            try
            {
                // TODO The optional CommandBool method should either be implemented OR throw a MethodNotImplementedException
                // If implemented, CommandBool must send the supplied command to the mount, wait for a response and parse this to return a True or False value

                CheckConnected("CommandBool");
                CheckParked("CommandBool");

                tl.LogMessage("CommandBool", $"raw: {raw} command {command}");

                if (!raw) { command = ":" + command + "#"; }

                serialPort.ClearBuffers();
                serialPort.Transmit(command);
                                                                    //Does not take into account if retString[0] is not 1 or 0...            
                var result = serialPort.ReceiveCounted(1);
                bool retBool = char.GetNumericValue(result[0]) == 1; // Parse the returned string and create a boolean True / False value
                                                                     //serialPort.ClearBuffers();
                tl.LogMessage("CommandBool", $"{command} Completed: {result} Parsed as: {retBool}");
                if (retBool && command.Equals(":MS#"))
                {
                    var clrbuf = serialPort.ReceiveTerminated("#");
                    tl.LogMessage("CommandBool", "Dumping String: " + clrbuf);
                }
                return retBool; // Return the boolean value to the client
            }
            catch (Exception ex)
            {
                tl.LogMessage("CommandBool", $"Error: {ex.Message}; Command: {command}");
                throw;
            }
        }

        /// <summary>
        /// Transmits an arbitrary string to the device and waits for a string response.
        /// Optionally, protocol framing characters may be added to the string before transmission.
        /// </summary>
        /// <param name="Command">The literal command string to be transmitted.</param>
        /// <param name="Raw">
        /// if set to <c>true</c> the string is transmitted 'as-is'.
        /// If set to <c>false</c> then protocol framing characters may be added prior to transmission.
        /// </param>
        /// <returns>
        /// Returns the string response received from the device.
        /// </returns>
        public string CommandString(string command, bool raw)
        {

            try
            {
                CheckConnected("CommandString");
                CheckParked("CommandString");

                tl.LogMessage("CommandString", $"raw: {raw} command {command}");

                if (!raw) { command = ":" + command + "#"; }

                serialPort.ClearBuffers();
                serialPort.Transmit(command);
                var result = serialPort.ReceiveTerminated("#");  //assumes that all return strings are # terminated...is this true?
                                                                 //tl.LogMessage("CommandString", "utilities.WaitForMilliseconds(TRANSMIT_WAIT_TIME);");
                                                                 //utilities.WaitForMilliseconds(TRANSMIT_WAIT_TIME); //limit transmit rate
                                                                 //tl.LogMessage("CommandString", "completed serial port receive...");
                tl.LogMessage("CommandString", $"{command} Completed: {result}");

                return result;
            }
            catch (Exception ex)
            {
                tl.LogMessage("CommandString", $"Error: {ex.Message}; Command: {command}");
                throw;
            }

        }

        /// <summary>
        /// Dispose the late-bound interface, if needed. Will release it via COM
        /// if it is a COM object, else if native .NET will just dereference it
        /// for GC.
        /// </summary>
        public void Dispose()
        {
            // Clean up the trace logger and util objects
            tl.Enabled = false;
            tl.Dispose();
            tl = null;
            utilities.Dispose();
            utilities = null;
            astroUtilities.Dispose();
            astroUtilities = null;
        }

        /// <summary>
        /// Set True to connect to the device hardware. Set False to disconnect from the device hardware.
        /// You can also read the property to check whether it is connected. This reports the current hardware state.
        /// </summary>
        /// <value><c>true</c> if connected to the hardware; otherwise, <c>false</c>.</value>
        public bool Connected
        {
            get
            {
                tl.LogMessage("Connected", "Get " + IsConnected.ToString());
                return IsConnected;
            }
            set
            {
                tl.LogMessage("Connected", "Set " + value.ToString());
                if (value == IsConnected)
                    return;

                if (value)
                {
                    try
                    {
                        if (AtPark)
                        {
                            tl.LogMessage("Connected", "Set - Mount appears parked.  Cycle power and close all Device Hub windows to reconnect");
                            throw new ASCOM.ParkedException("Mount appears parked.  Cycle mount power and close all Device Hub windows to reconnect");
                        }

                        //Define new serial object.  TTS-160 connects at 9600 baud, 8 data, no parity, 1 stop
                        serialPort = new Serial
                        {
                            PortName = comPort,
                            Speed = SerialSpeed.ps9600,
                            Parity = SerialParity.None,
                            DataBits = 8,
                            StopBits = SerialStopBits.One,
                            Connected = true
                        };

                        connectedState = true;
                        tl.LogMessage("Connected", "Success");
                        tl.LogMessage("Connected", "Connected with " + driverDescription);
                        tl.LogMessage("Connected", "Updating Site Lat and Long");
                        profileProperties.SiteLatitude = SiteLatitude;
                        profileProperties.SiteLongitude = SiteLongitude;
                        tl.LogMessage("Connected", "Lat: " + SiteLatitude.ToString());
                        tl.LogMessage("Connected", "Long: " + SiteLongitude.ToString());
                        WriteProfile(profileProperties);

                        tl.LogMessage("Connected", "Sync Time on Connect - " + profileProperties.SyncTimeOnConnect.ToString());
                        if (profileProperties.SyncTimeOnConnect)
                        {
                            tl.LogMessage("Connected", "Pre Sync Mount UTC: " + UTCDate.ToString("MM/dd/yy HH:mm:ss"));
                            tl.LogMessage("Connected", "Pre Sync Computer UTC: " + DateTime.UtcNow.ToString("MM/dd/yy HH:mm:ss"));
                            UTCDate = DateTime.UtcNow;
                            tl.LogMessage("Connected", "Post Sync Mount UTC: " + UTCDate.ToString("MM/dd/yy HH:mm:ss"));
                            tl.LogMessage("Connected", "Post Sync Computer UTC: " + DateTime.UtcNow.ToString("MM/dd/yy HH:mm:ss"));

                        }

                        tl.LogMessage("Connected", "Establishing Tracking Rate - " + profileProperties.TrackingRateOnConnect.ToString());
                        switch (profileProperties.TrackingRateOnConnect)
                        {
                            case 0:
                                TrackingRate = DriveRates.driveSidereal;
                                break;
                            case 1:
                                TrackingRate = DriveRates.driveLunar;
                                break;
                            case 2:
                                TrackingRate = DriveRates.driveSolar;
                                break;
                            default:
                                throw new ASCOM.InvalidValueException("Unexpected TrackingRateOnConnect Value: " + profileProperties.TrackingRateOnConnect.ToString());

                        }
                        

                    }
                    catch (Exception ex)
                    {
                        // report any error
                        throw new ASCOM.NotConnectedException($"Serial port connection error: {ex}");
                    }

                    tl.LogMessage("Connected Set", "Connecting to port " + comPort.ToString());
                }
                else
                {
                    tl.LogMessage("Connected Set", "Disconnecting from port " + comPort.ToString());

                    try
                    {
                        profileProperties.SiteLatitude = SiteLatitude;
                        profileProperties.SiteLongitude = SiteLongitude;
                        WriteProfile(profileProperties);

                        serialPort.Connected = false;
                        connectedState = false;
                    }
                    catch (Exception ex) 
                    {
                        
                        throw new ASCOM.DriverException($"Serial port disconnect error: {ex}");

                    }

                }
            }
        }

        /// <summary>
        /// Returns a description of the device, such as manufacturer and modelnumber. Any ASCII characters may be used.
        /// </summary>
        /// <value>The description.</value>
        public string Description
        {
            // TODO customise this device description
            get
            {
                tl.LogMessage("Description Get", driverDescription);
                return driverDescription;
            }
        }

        /// <summary>
        /// Descriptive and version information about this ASCOM driver.
        /// </summary>
        public string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                // TODO customise this driver description
                string driverInfo = "Driver for TTS-160. Version: " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        /// <summary>
        /// A string containing only the major and minor version of the driver formatted as 'm.n'.
        /// </summary>
        public string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                tl.LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        /// <summary>
        /// The interface version number that this device supports. 
        /// </summary>
        public short InterfaceVersion
        {
            // set by the driver wizard
            get
            {
                LogMessage("InterfaceVersion Get", "3");
                return Convert.ToInt16("3");
            }
        }

        /// <summary>
        /// The short name of the driver, for display purposes
        /// </summary>
        public string Name
        {
            get
            {
                string name = "TTS-160";
                tl.LogMessage("Name Get", name);
                return name;
            }
        }

        #endregion

        #region ITelescope Implementation

        /// <summary>
        /// Stops a slew in progress.
        /// </summary>
        public void AbortSlew()
        {
            try
            {
                //Per ASCOM standards, should not send command unless "Slewing" is true.
                //TTS-160 will ignore this command if it is not slewing, so this allows 'universal abort'
                //This provides a measure of safety in case something goes wrong with the Slewing property
                //or IsSlewingToTarget variable in MiscResources.

                tl.LogMessage("AbortSlew", "Aborting Slew, CommandBlind :Q#");
                CheckConnected("AbortSlew");
                Commander(":Q#", true, 0);
                Slewing = false;
                MiscResources.IsSlewingAsync = false;
                MiscResources.IsSlewingToTarget = false;
                MiscResources.SlewSettleStart = DateTime.MinValue;
                IsPulseGuiding = false;
                MiscResources.IsPulseGuiding = false;
                MiscResources.MovingPrimary = false;
                MiscResources.MovingSecondary = false;
                Tracking = MiscResources.TrackSetFollower;

                tl.LogMessage("AbortSlew", "Completed");
            }
            catch (Exception ex)
            {
                tl.LogMessage("AbortSlew", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// The alignment mode of the mount (Alt/Az, Polar, German Polar).
        /// </summary>
        public AlignmentModes AlignmentMode
        {
            get
            {
                try
                {
                    CheckConnected("AlignmentMode");

                    //String ret = CommandString(":GW#", true);
                    String ret = Commander(":GW#", true, 2);
                    switch (ret[0])
                    {
                        case 'A': return DeviceInterface.AlignmentModes.algAltAz;
                        case 'P': return DeviceInterface.AlignmentModes.algPolar;  //This should be the only response from TTS-160
                        case 'G': return DeviceInterface.AlignmentModes.algGermanPolar;
                        default: throw new DriverException("Unknown AlignmentMode Reported");
                    }

                }
                catch (Exception ex)
                {
                    tl.LogMessage("AlignmentMode Get", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// The Altitude above the local horizon of the telescope's current position (degrees, positive up)
        /// </summary>
        public double Altitude
        {
            get
            {
                try
                {

                    tl.LogMessage("Altitude Get", "Getting Altitude");
                    CheckConnected("Altitude Get");

                    //var result = CommandString(":GA#", true);
                    var result = Commander(":GA#", true, 2);
                    //:GA# Get telescope altitude
                    //Returns: DDD*MM# or DDD*MM'SS#
                    //The current telescope Altitude depending on the selected precision.

                    double alt = utilities.DMSToDegrees(result);

                    tl.LogMessage("Altitude Get", $"{alt}");
                    return alt;

                }
                catch (Exception ex)
                {
                    tl.LogMessage("Altitude Get", $"Error: {ex.Message}");
                    throw;
                }

            }
        }

        /// <summary>
        /// The area of the telescope's aperture, taking into account any obstructions (square meters)
        /// </summary>
        public double ApertureArea
        {
            get
            {
                tl.LogMessage("ApertureArea Get", "Not implemented");
                throw new PropertyNotImplementedException("ApertureArea", false);
            }
        }

        /// <summary>
        /// The telescope's effective aperture diameter (meters)
        /// </summary>
        public double ApertureDiameter
        {
            get
            {
                tl.LogMessage("ApertureDiameter Get", "Not implemented");
                throw new PropertyNotImplementedException("ApertureDiameter", false);
            }
        }

        /// <summary>
        /// True if the telescope is stopped in the Home position. Set only following a <see cref="FindHome"></see> operation,
        /// and reset with any slew operation. This property must be False if the telescope does not support homing.
        /// </summary>
        public bool AtHome
        {
            get
            {
                //TTS-160 does not support Homing at this time.  Return False.
                tl.LogMessage("AtHome", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if the telescope has been put into the parked state by the <see cref="Park" /> method. Set False by calling the Unpark() method.
        /// </summary>
        public bool AtPark
        {
            get
            {
                tl.LogMessage("AtPark", "Get - " + MiscResources.IsParked.ToString());
                return MiscResources.IsParked;
            }
            set
            {
                tl.LogMessage("AtPark", "Set - " + value.ToString());
                MiscResources.IsParked = value;
            }
        }

        /// <summary>
        /// Determine the rates at which the telescope may be moved about the specified axis by the <see cref="MoveAxis" /> method.
        /// </summary>
        /// <param name="Axis">The axis about which rate information is desired (TelescopeAxes value)</param>
        /// <returns>Collection of <see cref="IRate" /> rate objects</returns>
        public IAxisRates AxisRates(TelescopeAxes Axis)
        {
            try
            {
                CheckConnected("AxisRates");
                tl.LogMessage("AxisRates", "Get - " + Axis.ToString());
                var buf = new AxisRates(Axis);
                tl.LogMessage("AxisRates", "Returning - " + buf.ToString() + "; Count: " + buf.Count.ToString());
                return buf;
            }
            catch (Exception ex)
            {
                tl.LogMessage("AxisRates", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// The azimuth at the local horizon of the telescope's current position (degrees, North-referenced, positive East/clockwise).
        /// </summary>
        public double Azimuth
        {
            get
            {
                try
                {

                    tl.LogMessage("Azimuth Get", "Getting Azimuth");
                    CheckConnected("Azimuth Get");

                    //var result = CommandString(":GZ#", true);
                    var result = Commander(":GZ#", true, 2);
                    //:GZ# Get telescope azimuth
                    //Returns: DDD*MM#T or DDD*MM'SS# verify low precision returns with T at the end!
                    //The current telescope Azimuth depending on the selected precision.

                    double az = utilities.DMSToDegrees(result);

                    tl.LogMessage("Azimuth Get", $"{az}");
                    return az;

                }
                catch (Exception ex)
                {
                    tl.LogMessage("Azimuth Get", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed finding its home position (<see cref="FindHome" /> method).
        /// </summary>
        public bool CanFindHome
        {
            get
            {
                try
                {
                    CheckConnected("CanFindHome");

                    //TTS-160 does not have 'Home' functionality implemented at this time, return false
                    tl.LogMessage("CanFindHome", "Get - " + false.ToString());
                    return false;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanFindHome", $"Error: {ex.Message}");
                    throw;
                }

            }
        }

        /// <summary>
        /// True if this telescope can move the requested axis
        /// </summary>
        public bool CanMoveAxis(TelescopeAxes Axis)
        {
            try
            {
                CheckConnected("CanMoveAxis");
                tl.LogMessage("CanMoveAxis", "Get - " + Axis.ToString());
                switch (Axis)
                {                  
                    case TelescopeAxes.axisPrimary: return true;
                    case TelescopeAxes.axisSecondary: return true;
                    case TelescopeAxes.axisTertiary: return false;
                    default: throw new InvalidValueException("CanMoveAxis", Axis.ToString(), "0 to 2");
                }
            }
            catch (Exception ex)
            {
                tl.LogMessage("CanFindHome", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed parking (<see cref="Park" />method)
        /// </summary>
        public bool CanPark
        {
            get
            {
                try
                {
                    CheckConnected("CanPark");

                    tl.LogMessage("CanPark", "Get - " + true.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanPark", $"Error: {ex.Message}");
                    throw;
                }

            }
        }

        /// <summary>
        /// True if this telescope is capable of software-pulsed guiding (via the <see cref="PulseGuide" /> method)
        /// </summary>
        public bool CanPulseGuide
        {
            get
            {
                try
                {
                    CheckConnected("CanPulseGuide");
                    
                    //Pulse guiding is implemented => true
                    tl.LogMessage("CanPulseGuide", "Get - " + true.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanPulseGuide", $"Error: {ex.Message}");
                    throw;
                }
                

            }
        }

        /// <summary>
        /// True if the <see cref="DeclinationRate" /> property can be changed to provide offset tracking in the declination axis.
        /// </summary>
        public bool CanSetDeclinationRate
        {
            get
            {
                try
                {
                    CheckConnected("CanSetDeclinationRate");
                    
                    //SetDeclinationRate is not implemented in TTS-160, return false
                    tl.LogMessage("CanSetDeclinationRate", "Get - " + false.ToString());
                    return false;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSetDeclinationRate", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if the guide rate properties used for <see cref="PulseGuide" /> can ba adjusted.
        /// </summary>
        public bool CanSetGuideRates
        {
            get
            {
                try
                {
                    CheckConnected("CanSetGuideRates");

                    //Check for override for App Compatibility Mode
                    if (profileProperties.CanSetGuideRatesOverride)
                    {
                        tl.LogMessage("CanSetGuideRates Override", "Showing CanSetGuideRates as True");
                        tl.LogMessage("CanSetGuideRates", "Get - " + true.ToString());
                        return true;
                    }
                    else
                    {
                        //TTS-160 does not support SetGuideRates, return false
                        tl.LogMessage("CanSetGuideRates", "Get - " + false.ToString());
                        return false;
                    }

                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSetGuideRates", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed setting of its park position (<see cref="SetPark" /> method)
        /// </summary>
        public bool CanSetPark
        {
            get
            {
                try
                {
                    CheckConnected("CanSetPark");

                    //Set Park is not implemented by TTS-160, return false
                    tl.LogMessage("CanSetPark", "Get - " + false.ToString());
                    return false;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSetPark", $"Error: {ex.Message}");
                    throw;
                }
                
            }
        }

        /// <summary>
        /// True if the <see cref="SideOfPier" /> property can be set, meaning that the mount can be forced to flip.
        /// </summary>
        public bool CanSetPierSide
        {
            get
            {
                try
                {
                    CheckConnected("CanSetPierSide");

                    //TTS-160 does not have Set PierSide implemented, return false
                    tl.LogMessage("CanSetPierSide", "Get - " + false.ToString());
                    return false;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSetPierSide", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if the <see cref="RightAscensionRate" /> property can be changed to provide offset tracking in the right ascension axis.
        /// </summary>
        public bool CanSetRightAscensionRate
        {
            get
            {
                try
                {
                    CheckConnected("CanSetRightAscensionRate");
                    
                    //TTS-160 has not implemented SetRightAscensionRate, return false
                    tl.LogMessage("CanSetRightAscensionRate", "Get - " + false.ToString());
                    return false;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSetRightAscensionRate", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if the <see cref="Tracking" /> property can be changed, turning telescope sidereal tracking on and off.
        /// </summary>
        public bool CanSetTracking
        {
            get
            {
                try
                {
                                  
                    CheckConnected("CanSetTracking");

                    tl.LogMessage("CanSetTracking", "Get - " + true.ToString());
                    return true;

                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSetTracking", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed slewing (synchronous or asynchronous) to equatorial coordinates
        /// </summary>
        public bool CanSlew
        {
            get
            {
                try
                {
                    CheckConnected("CanSlew");
                    
                    tl.LogMessage("CanSlew", "Get - " + true.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSlew", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed slewing (synchronous or asynchronous) to local horizontal coordinates
        /// </summary>
        public bool CanSlewAltAz
        {
            get
            {
                try
                {
                    CheckConnected("CanSlewAltAz");

                    tl.LogMessage("CanSlewAltAz", "Get - " + true.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSlewAltAz", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed asynchronous slewing to local horizontal coordinates
        /// </summary>
        public bool CanSlewAltAzAsync
        {
            get
            {
                try
                {
                    CheckConnected("CanSlewAltAzAsync");
                    
                    tl.LogMessage("CanSlewAltAzAsync", "Get - " + true.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSlewAltAzAsync", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed asynchronous slewing to equatorial coordinates.
        /// </summary>
        public bool CanSlewAsync
        {
            get
            {
                try
                {
                    CheckConnected("CanSlewAsync");
                    
                    tl.LogMessage("CanSlewAsync", "Get - " + true.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSlewAsync", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed synching to equatorial coordinates.
        /// </summary>
        public bool CanSync
        {
            get
            {
                try
                {
                    CheckConnected("CanSync");
                    
                    tl.LogMessage("CanSync", "Get - " + true.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSync", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed synching to local horizontal coordinates
        /// </summary>
        public bool CanSyncAltAz
        {
            get
            {
                try
                {
                    CheckConnected("CanSyncAltAz");
                    
                    tl.LogMessage("CanSyncAltAz", "Get - " + true.ToString());
                    return true;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanSyncAltAz", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed unparking (<see cref="Unpark" /> method).
        /// </summary>
        public bool CanUnpark
        {
            get
            {
                try
                {
                    CheckConnected("CanUnpark");

                    tl.LogMessage("CanUnpark", "Get - " + false.ToString());
                    return false;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("CanUnpark", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// The declination (degrees) of the telescope's current equatorial coordinates, in the coordinate system given by the <see cref="EquatorialSystem" /> property.
        /// Reading the property will raise an error if the value is unavailable.
        /// </summary>
        public double Declination
        {
            get
            {
                try
                {

                    //tl.LogMessage("Declination Get", "Getting Declination");
                    CheckConnected("Declination Get");

                    //var result = CommandString(":GD#", true);
                    var result = Commander(":GD#", true, 2);
                    //:GD# Get telescope Declination
                    //Returns: DDD*MM#T or DDD*MM'SS#
                    //The current telescope Declination depending on the selected precision.

                    double declination = utilities.DMSToDegrees(result);

                    tl.LogMessage("Declination", "Get - " + utilities.DegreesToDMS(declination, ":", ":"));
                    return declination;

                }
                catch (Exception ex)
                {
                    tl.LogMessage("Declination Get", $"Error: {ex.Message}");
                    throw;
                }

            }
        }

        /// <summary>
        /// The declination tracking rate (arcseconds per SI second, default = 0.0)
        /// </summary>
        public double DeclinationRate
        {
            get
            {
                //Declination Rate not implemented by TTS-160, return 0.0
                double declinationRate = 0.0;
                tl.LogMessage("DeclinationRate", "Get - " + declinationRate.ToString());
                return declinationRate;
            }
            set
            {
                //Declination Rate not implemented by TTS-160
                tl.LogMessage("DeclinationRate Set", "Not implemented");
                throw new PropertyNotImplementedException("DeclinationRate", true);
            }
        }

        /// <summary>
        /// Predict side of pier for German equatorial mounts at the provided coordinates
        /// </summary>
        public PierSide DestinationSideOfPier(double rightAscension, double Declination)
        {
            //tl.LogMessage("DestinationSideOfPier Get", "Not implemented");
            //throw new PropertyNotImplementedException("DestinationSideOfPier", false);
            try
            {
                CheckConnected("DestinationSideOfPier");

                var destinationSOP = CalculateSideOfPier(rightAscension);

                LogMessage("DestinationSideOfPier",
                    $"Destination SOP of RA {rightAscension.ToString(CultureInfo.InvariantCulture)} is {destinationSOP}");

                return destinationSOP;
            }
            catch (Exception ex)
            {
                LogMessage("DestinationSideOfPier", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// True if the telescope or driver applies atmospheric refraction to coordinates.
        /// </summary>
        public bool DoesRefraction
        {
            get
            {
                //Refraction not implemented by TTS-160
                tl.LogMessage("DoesRefraction Get", "Not implemented");
                throw new PropertyNotImplementedException("DoesRefraction", false);
            }
            set
            {
                //Refraction not implemented by TTS-160
                tl.LogMessage("DoesRefraction Set", "Not implemented");
                throw new PropertyNotImplementedException("DoesRefraction", true);
            }
        }

        /// <summary>
        /// Equatorial coordinate system used by this telescope (e.g. Topocentric or J2000).
        /// </summary>
        public EquatorialCoordinateType EquatorialSystem
        {
            get
            {
                try
                {
                    CheckConnected("EquatorialCoordinateType");
                    
                    //TTS-160 uses accepts Topocentric coordinates

                    EquatorialCoordinateType equatorialSystem = EquatorialCoordinateType.equTopocentric;
                    tl.LogMessage("EquatorialCoordinateType", "Get - " + equatorialSystem.ToString());
                    return equatorialSystem;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("EquatorialCoordinateType", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Locates the telescope's "home" position (synchronous)
        /// </summary>
        public void FindHome()
        {
            tl.LogMessage("FindHome", "Not implemented");
            throw new MethodNotImplementedException("FindHome");
        }

        /// <summary>
        /// The telescope's focal length, meters
        /// </summary>
        public double FocalLength
        {
            get
            {
                tl.LogMessage("FocalLength Get", "Not implemented");
                throw new PropertyNotImplementedException("FocalLength", false);
            }
        }

        /// <summary>
        /// The current Declination movement rate offset for telescope guiding (degrees/sec)
        /// </summary>
        public double GuideRateDeclination
        {
            get
            {
                //Check for CanSetGuideRates Override...
                if (profileProperties.CanSetGuideRatesOverride)
                {
                    double DefaultGuideRate = 0.00277777777777778; // deg/sec
                    tl.LogMessage("CanSetGuideRates Override", "Get GuideRateDeclination command received, returning default rate");
                    tl.LogMessage("GuideRateDeclination", "Get - " + DefaultGuideRate.ToString());
                    return DefaultGuideRate;
                }
                else
                {
                    tl.LogMessage("GuideRateDeclination Get", "Not implemented");
                    throw new PropertyNotImplementedException("GuideRateDeclination", false);
                }

            }
            set
            {
                //Check for CanSetGuideRates Override...
                if (profileProperties.CanSetGuideRatesOverride)
                {
                    tl.LogMessage("CanSetGuideRates Override", "Set GuideRateDeclination " + value.ToString() + " command received");
                    tl.LogMessage("GuideRateDeclination", "Set - " + value.ToString() + " assigned to nothing...");
                }
                else
                {
                    tl.LogMessage("GuideRateDeclination Set", "Not implemented");
                    throw new PropertyNotImplementedException("GuideRateDeclination", true);
                }

            }
        }

        /// <summary>
        /// The current Right Ascension movement rate offset for telescope guiding (degrees/sec)
        /// </summary>
        public double GuideRateRightAscension
        {
            get
            {

                if (profileProperties.CanSetGuideRatesOverride)
                {
                    double DefaultGuideRate = 0.00277777777777778; // deg/sec
                    tl.LogMessage("CanSetGuideRates Override", "Get GuideRateRightAscension command received, returning default rate");
                    tl.LogMessage("GuideRateRightAscension", "Get - " + DefaultGuideRate.ToString());
                    return DefaultGuideRate;
                }
                else
                {
                    tl.LogMessage("GuideRateRightAscension Get", "Not implemented");
                    throw new PropertyNotImplementedException("GuideRateRightAscension", false);
                }


            }
            set
            {
                //Check for CanSetGuideRates Override...
                if (profileProperties.CanSetGuideRatesOverride)
                {
                    tl.LogMessage("CanSetGuideRates Override", "Set GuideRateRightAscension " + value.ToString() + " command received");
                    tl.LogMessage("GuideRateRightAscension", "Set - " + value.ToString() + " assigned to nothing...");
                }
                else
                {
                    tl.LogMessage("GuideRateRightAscension Set", "Not implemented");
                    throw new PropertyNotImplementedException("GuideRateRightAscension", true);
                }
            }
        }

        /// <summary>
        /// True if a <see cref="PulseGuide" /> command is in progress, False otherwise
        /// </summary>
        public bool IsPulseGuiding
        {
            //Pulse Guide query is not implemented in TTS-160 => track in driver
            get
            {
                try
                {
                    CheckConnected("IsPulseGuiding");

                    tl.LogMessage("IsPulseGuiding", "Get - " + MiscResources.IsPulseGuiding.ToString());
                    return MiscResources.IsPulseGuiding;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("EquatorialCoordinateType", $"Error: {ex.Message}");
                    throw;
                }
            }

            set
            {
                tl.LogMessage("IsPulseGuding", "Set - " + value.ToString());
                MiscResources.IsPulseGuiding = value;
            }
        }

        /// <summary>
        /// Move the telescope in one axis at the given rate.
        /// </summary>
        /// <param name="Axis">The physical axis about which movement is desired</param>
        /// <param name="Rate">The rate of motion (deg/sec) about the specified axis</param>
        // Implementation of this function based on code from Generic Meade Driver
        // Note: ASCOM non-compliance in that Tracking is not able to be disabled
        // by the driver
        public void MoveAxis(TelescopeAxes Axis, double Rate)
        {
            try
            {

                tl.LogMessage("MoveAxis", $"Axis={Axis} rate={Rate}");
                CheckConnected("MoveAxis");
                CheckParked("MoveAxis");

                var absRate = Math.Abs(Rate);
                tl.LogMessage("MoveAxis", "Setting rate to " + absRate.ToString() + " deg/sec");

                switch (absRate)
                {

                    case (0):
                        //do nothing, it's ok this time as we're halting the slew.
                        break;

                    case (0.000277777777777778):
                        Commander(":RG#", true, 0);

                        break;

                    case (1.4):
                        Commander(":RM#", true, 0);
                        break;

                    case (2.2):
                        Commander(":RC#", true, 0);
                        break;

                    case (3):
                        Commander(":RS#", true, 0);
                        break;

                    default:
                        //invalid rate exception
                        throw new InvalidValueException($"Rate {absRate} deg/sec not supported");
                }

                switch (Axis)
                {
                    case TelescopeAxes.axisPrimary:
                        switch (Rate.Compare(0))
                        {
                            case ComparisonResult.Equals:
                                //if (!MiscResources.IsGuiding)
                                //{
                                //Not implemented for TTS-160 driver
                                //SetSlewingMinEndTime();
                                //}
                               
                                tl.LogMessage("MoveAxis", "Primary Axis Stop Movement");
                                Commander(":Qe#", true, 0);
                                    //:Qe# Halt eastward Slews
                                    //Returns: Nothing
                                Commander(":Qw#", true, 0);
                                //:Qw# Halt westward Slews
                                //Returns: Nothing

                                int LOOP_WAIT_TIME = 100; //ms
                                 
                                tl.LogMessage("MoveAxis", "Movement finished, waiting for " + MOVEAXIS_WAIT_TIME.ToString() + " ms or until tracking restarts");                                  
                                int iter = Convert.ToInt32(Convert.ToDouble(MOVEAXIS_WAIT_TIME) / Convert.ToDouble(LOOP_WAIT_TIME));                                  
                                int i = 0;   
                                while (i <= iter)
                                    {
                                        if (Tracking) { break; }  //if tracking is restored, no need to wait!
                                        Thread.Sleep(LOOP_WAIT_TIME);
                                        i++;
                                    }

                                MiscResources.MovingPrimary = false;
                                //Per ASCOM standard, SHOULD be incorporating SlewSettleTime

                                tl.LogMessage("MoveAxis", "Primary Axis Stop Movement - Slewing False");
                                Slewing = false;
                                break;

                            case ComparisonResult.Greater:
                                tl.LogMessage("MoveAxis", "Move East");
                                if (MiscResources.MovingPrimary)
                                {
                                    tl.LogMessage("MoveAxis", "Still moving primary axis, waiting " + MOVEAXIS_WAIT_TIME.ToString() + " ms and retrying...");
                                    if (MiscResources.MovingPrimary)
                                    {
                                        tl.LogMessage("MoveAxis", "Retry failed after wait period.");
                                        throw new ASCOM.DriverException("Axis already in motion");
                                    } 
                                }
                                Commander(":Me#", true, 0);
                                //:Me# Move Telescope East at current slew rate
                                //Returns: Nothing
                                MiscResources.MovingPrimary = true;
                                Slewing = true;
                                break;

                            case ComparisonResult.Lower:
                                tl.LogMessage("MoveAxis", "Move West");
                                if (MiscResources.MovingPrimary)
                                {
                                    tl.LogMessage("MoveAxis", "Still moving primary axis, waiting " + MOVEAXIS_WAIT_TIME.ToString() + " ms and retrying...");
                                    if (MiscResources.MovingPrimary)
                                    {
                                        tl.LogMessage("MoveAxis", "Retry failed after wait period.");
                                        throw new ASCOM.DriverException("Axis already in motion");
                                    }
                                }
                                Commander(":Mw#", true, 0);
                                //:Mw# Move Telescope West at current slew rate
                                //Returns: Nothing
                                MiscResources.MovingPrimary = true;
                                Slewing = true;
                                break;
                        }

                        break;
                    case TelescopeAxes.axisSecondary:
                        switch (Rate.Compare(0))
                        {
                            case ComparisonResult.Equals:
                                tl.LogMessage("MoveAxis", "Secondary Axis Stop Movement");
                                Commander(":Qn#", true, 0);
                                    //:Qn# Halt northward Slews
                                    //Returns: Nothing
                                Commander(":Qs#", true, 0);
                                    //:Qs# Halt southward Slews
                                    //Returns: Nothing                              
                                MiscResources.MovingSecondary = false;
                                //Per ASCOM standard, SHOULD be incorporating SlewSettleTime
                                tl.LogMessage("MoveAxis", "Secondary Axis Stop Movement - Slewing False");
                                Slewing = false;
                                break;
                            case ComparisonResult.Greater:
                                tl.LogMessage("MoveAxis", "Move North");
                                if (MiscResources.MovingSecondary)
                                {
                                    tl.LogMessage("MoveAxis", "Still moving secondary axis, waiting " + MOVEAXIS_WAIT_TIME.ToString() + " ms and retrying...");
                                    if (MiscResources.MovingSecondary)
                                    {
                                        tl.LogMessage("MoveAxis", "Retry failed after wait period.");
                                        throw new ASCOM.DriverException("Axis already in motion");
                                    }
                                }
                                Commander(":Mn#", true, 0);
                                //:Mn# Move Telescope North at current slew rate
                                //Returns: Nothing
                                MiscResources.MovingSecondary = true;
                                Slewing = true;
                                break;
                            case ComparisonResult.Lower:
                                tl.LogMessage("MoveAxis", "Move South");
                                if (MiscResources.MovingSecondary)
                                {
                                    tl.LogMessage("MoveAxis", "Still moving secondary axis, waiting " + MOVEAXIS_WAIT_TIME.ToString() + " ms and retrying...");
                                    if (MiscResources.MovingSecondary)
                                    {
                                        tl.LogMessage("MoveAxis", "Retry failed after wait period.");
                                        throw new ASCOM.DriverException("Axis already in motion");
                                    }
                                }
                                Commander(":Ms#", true, 0);
                                //:Ms# Move Telescope South at current slew rate
                                //Returns: Nothing
                                MiscResources.MovingSecondary = true;
                                Slewing = true;
                                break;
                        }

                        break;
                    default:
                        throw new InvalidValueException("Can not move this axis.");
                }
            }
            catch (Exception ex)
            {
                tl.LogMessage("MoveAxis", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Move the telescope to its park position, stop all motion (or restrict to a small safe range), and set <see cref="AtPark" /> to True.
        /// </summary>
        public void Park()
        {
            try
            {
                CheckConnected("Park");
                tl.LogMessage("Park", "Parking Mount");
                if (!AtPark)
                {
                    Commander(":hP#", true, 0);
                    AtPark = true;
                    tl.LogMessage("Park", "Mount is Parked");
                }
                else
                {
                    tl.LogMessage("Park", "AtPark is " + AtPark.ToString());
                    tl.LogMessage("Park", "Ignoring Park command");
                }
            }
            catch (Exception ex)
            {
                tl.LogMessage("Park", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Moves the scope in the given direction for the given interval or time at
        /// the rate given by the corresponding guide rate property
        /// </summary>
        /// <param name="Direction">The direction in which the guide-rate motion is to be made</param>
        /// <param name="Duration">The duration of the guide-rate motion (milliseconds)</param>
        public void PulseGuide(GuideDirections Direction, int Duration)
        {

            //Note that it is not clear if TTS-160 responds in body frame or LH frame
            tl.LogMessage("PulseGuide", $"pulse guide direction {Direction} duration {Duration}");
            try
            {
                //TODO Need to check for valid direction.  Or not, should be verified because it is enumerated
                CheckConnected("PulseGuide");
                //Park is not yet implemented...
                //CheckParked();

                if (MiscResources.IsSlewingToTarget) { throw new InvalidOperationException("Unable to PulseGuide while slewing to target."); }
                if (Duration > 9999) { throw new InvalidValueException("Duration greater than 9999 msec"); }
                if (Duration < 0) { throw new InvalidValueException("Duration less than 0 msec"); }           
                
                if (MiscResources.MovingPrimary &&
                    (Direction == GuideDirections.guideEast || Direction == GuideDirections.guideWest))
                    throw new InvalidOperationException("Unable to PulseGuide while moving same axis.");
                
                if (MiscResources.MovingSecondary &&
                    (Direction == GuideDirections.guideNorth || Direction == GuideDirections.guideSouth))
                    throw new InvalidOperationException("Unable to PulseGuide while moving same axis.");

                //Check to see if GuideComp is enabled, then correct pulse length if required
                int maxcomp = profileProperties.GuideCompMaxDelta; //set maximum allowable compensation time in msec (PHD2 is 1 sec)
                int bufftime = profileProperties.GuideCompBuffer; //set buffer time to decrement from max in msec to prevent tripping PHD2 limit
                double maxalt = 89; //Sufficiently close to 90 to allow exceeding maxcomp while preventing divide by zero

                if (profileProperties.GuideComp == 1)
                {
                    switch (Direction)
                    {
                        case GuideDirections.guideEast: 
                        case GuideDirections.guideWest:

                            tl.LogMessage("PulseGuideComp", "Applying Altitude Compensation");
                            double alt = Altitude;

                            if (alt > maxalt) { alt = maxalt; }; //Prevent receiving divide by zero by limiting altitude to <90 deg

                            double altrad = alt * Math.PI / 180; //convert to radians
                            int compDuration = (int)Math.Round(Duration / Math.Cos(altrad)); //calculate compensated duration
                            tl.LogMessage("PulseGuideComp", "Altitude: " + alt.ToString() + " deg (" + altrad.ToString() + " rad)");
                            tl.LogMessage("PulseGuideComp", "Compensated Time: " + compDuration.ToString("D4"));

                            if (compDuration > (Duration + maxcomp)) //verify we do not exceed maximum time value
                            {
                                compDuration = Duration + maxcomp - bufftime; //clip compensated time to maximum time value (with some buffer)
                                tl.LogMessage("PulseGuideComp", "Compensated Time exceeds maximum: " + (Duration + maxcomp).ToString("D4"));
                                tl.LogMessage("PulseGuideComp", "Setting compensated time to: " + compDuration.ToString("D4"));
                            }
                            Duration = compDuration; //Compensated time is verified good, replace the ordered Duration
                            break;

                    }
                   
                }
               
                IsPulseGuiding = true;
                tl.LogMessage("PulseGuide", "Guiding with Pulse Guide command");
                switch (Direction)
                {
                    case GuideDirections.guideEast:
                        var guidecmde = ":Mge" + Duration.ToString("D4") + "#";
                        tl.LogMessage("GuideEast", guidecmde);
                        //CommandBlind(guidecmde, true);
                        Commander(guidecmde, true, 0);
                        Thread.Sleep(Duration);
                        break;
                    case GuideDirections.guideNorth:
                        var guidecmdn = ":Mgn" + Duration.ToString("D4") + "#";
                        tl.LogMessage("GuideNorth", guidecmdn);
                        //CommandBlind(guidecmdn, true);
                        Commander(guidecmdn, true, 0);
                        Thread.Sleep(Duration);
                        break;
                    case GuideDirections.guideSouth:
                        var guidecmds = ":Mgs" + Duration.ToString("D4") + "#";
                        tl.LogMessage("GuideSouth", guidecmds);
                        //CommandBlind(guidecmds, true);
                        Commander(guidecmds, true, 0);
                        Thread.Sleep(Duration);
                        break;
                    case GuideDirections.guideWest:
                        var guidecmdw = ":Mgw" + Duration.ToString("D4") + "#";
                        tl.LogMessage("GuideWest", guidecmdw);
                        //CommandBlind(guidecmdw, true);
                        Commander(guidecmdw, true, 0);
                        Thread.Sleep(Duration);
                        break;
                }
                IsPulseGuiding = false;

                tl.LogMessage("PulseGuide", "pulse guide complete");                            
                
            }
            catch (Exception ex)
            {
                tl.LogMessage("PulseGuide", $"Error performing pulse guide: {ex.Message}");
                IsPulseGuiding = false;
                throw;
            }
        }

        private MiscResources.EquatorialCoordinates GetTelescopeRaAndDec()
        {
            return new MiscResources.EquatorialCoordinates
            {
                RightAscension = RightAscension,
                Declination = Declination
            };
        }

        /// <summary>
        /// The right ascension (hours) of the telescope's current equatorial coordinates,
        /// in the coordinate system given by the EquatorialSystem property
        /// </summary>
        public double RightAscension
        {
            get
            {
                try
                {
                    //tl.LogMessage("Right Ascension Get", "Getting Right Ascension");
                    CheckConnected("Right Ascension Get");

                    //var result = CommandString(":GR#", true);
                    var result = Commander(":GR#", true, 2);
                    //:GR# Get telescope Right Ascension
                    //Returns: HH:MM.T# or HH:MM:SS#
                    //The current telescope Right Ascension depending on the selected precision.

                    double rightAscension = utilities.HMSToHours(result);

                    tl.LogMessage("Right Ascension", "Get - " + utilities.HoursToHMS(rightAscension, ":", ":"));
                    return rightAscension;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("Right Ascension Get", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// The right ascension tracking rate offset from sidereal (seconds per sidereal second, default = 0.0)
        /// </summary>
        public double RightAscensionRate
        {
            get
            {
                //RightAscensionRate is not implemented by TTS-160, return 0.0
                double rightAscensionRate = 0.0;
                tl.LogMessage("RightAscensionRate", "Get - " + rightAscensionRate.ToString());
                return rightAscensionRate;
            }
            set
            {
                tl.LogMessage("RightAscensionRate Set", "Not implemented");
                throw new PropertyNotImplementedException("RightAscensionRate", true);
            }
        }

        /// <summary>
        /// Sets the telescope's park position to be its current position.
        /// </summary>
        public void SetPark()
        {
            tl.LogMessage("SetPark", "Not implemented");
            throw new MethodNotImplementedException("SetPark");
        }

        private PierSide CalculateSideOfPier(double rightAscension)
        {
            double hourAngle = astroUtilities.ConditionHA(SiderealTime - rightAscension);

            var destinationSOP = hourAngle > 0
                ? PierSide.pierEast
                : PierSide.pierWest;
            return destinationSOP;
        }

        /// <summary>
        /// Indicates the pointing state of the mount. Read the articles installed with the ASCOM Developer
        /// Components for more detailed information.
        /// </summary>
        public PierSide SideOfPier
        {
            get
            {
                //tl.LogMessage("SideOfPier Get", "Not implemented");
                //throw new PropertyNotImplementedException("SideOfPier", false);
                var pierSide = CalculateSideOfPier(RightAscension);

                LogMessage("SideOfPier", "Get - " + pierSide);
                return pierSide;
            }
            set
            {
                tl.LogMessage("SideOfPier Set", "Not implemented");
                throw new PropertyNotImplementedException("SideOfPier", true);
            }
        }

        /// <summary>
        /// The local apparent sidereal time from the telescope's internal clock (hours, sidereal)
        /// </summary>
        public double SiderealTime
        {
            get
            {
                try
                {
                    CheckConnected("SiderealTime");
                    var result = Commander(":GS#", true, 2).TrimEnd('#');
                    double siderealTime = utilities.HMSToHours(result);
                    double siteLongitude = SiteLongitude;
                    tl.LogMessage("SiderealTime", "Get GMST - " + siderealTime.ToString());
                    siderealTime += siteLongitude / 360.0 * 24.0;
                    siderealTime = astroUtilities.ConditionRA(siderealTime);
                    tl.LogMessage("SiderealTime", "Local Sidereal - " + siderealTime.ToString());
                    return siderealTime;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("Sidereal Time", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// The elevation above mean sea level (meters) of the site at which the telescope is located
        /// </summary>
        public double SiteElevation
        {
            get
            {
                return profileProperties.SiteElevation;
                //tl.LogMessage("SiteElevation Get", "Not implemented");
                //throw new PropertyNotImplementedException("SiteElevation", false);
            }
            set
            {
                try
                {
                    if ((value < -300) || (value > 10000)) { throw new ASCOM.InvalidValueException($"Invalid Site Elevation ${value}"); }
                    profileProperties.SiteElevation = value;
                    WriteProfile(profileProperties);
                }
                catch (Exception ex)
                {
                    tl.LogMessage("Site Altitude Set", $"Error: {ex.Message}");
                    throw;
                }

                //tl.LogMessage("SiteElevation Set", "Not implemented");
                //throw new PropertyNotImplementedException("SiteElevation", true);
            }
        }

        /// <summary>
        /// The geodetic(map) latitude (degrees, positive North, WGS84) of the site at which the telescope is located.
        /// </summary>
        public double SiteLatitude
        {
            get
            {
                try
                {

                    tl.LogMessage("SiteLatitude Get", "Getting Site Latitude");
                    CheckConnected("SiteLatitude Get");

                    //var result = CommandString(":Gt#", true);
                    var result = Commander(":Gt#", true, 2);
                    //:Gt# Get Site Latitude
                    //Returns: sDD*MM#

                    double siteLatitude = utilities.DMSToDegrees(result);

                    tl.LogMessage("SiteLatitude", "Get - " + utilities.DegreesToDMS(siteLatitude, ":", ":"));
                    return siteLatitude;

                }
                catch (Exception ex)
                {
                    tl.LogMessage("siteLatitude Get", $"Error: {ex.Message}");
                    throw;
                }
            }
            set
            {
                tl.LogMessage("SiteLatitude Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteLatitude", true);
            }
        }

        /// <summary>
        /// The longitude (degrees, positive East, WGS84) of the site at which the telescope is located.
        /// </summary>
        public double SiteLongitude
        {
            get
            {
                tl.LogMessage("SiteLongitude Get", "Getting Site Longitude");
                try
                {

                    tl.LogMessage("SiteLongitude Get", "Getting Site Longitude");
                    CheckConnected("SiteLongitude Get");

                    //var result = CommandString(":Gg#", true);
                    var result = Commander(":Gg#", true, 2);
                    //:Gg# Get Site Longitude
                    //Returns: sDDD*MM#, east negative

                    double siteLongitude = -1*utilities.DMSToDegrees(result); //correct to West negative

                    tl.LogMessage("SiteLongitude", "Get - " + utilities.DegreesToDMS(siteLongitude, ":", ":"));
                    return siteLongitude;

                }
                catch (Exception ex)
                {
                    tl.LogMessage("siteLongitude Get", $"Error: {ex.Message}");
                    throw;
                }
            }
            set
            {
                tl.LogMessage("SiteLongitude Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteLongitude", true);
            }
        }

        /// <summary>
        /// Specifies a post-slew settling time (sec.).
        /// </summary>
        public short SlewSettleTime
        {
            get
            {
                return profileProperties.SlewSettleTime;
            }
            set
            {
                try
                {
                    if (value >= 0) 
                    { 
                        profileProperties.SlewSettleTime = value;
                        WriteProfile(profileProperties);
                    }
                    else { throw new InvalidValueException("Settle Time must be >= 0"); }
                }
                catch (Exception ex)
                {
                    tl.LogMessage("SlewSettleTime Set", ex.ToString());
                    throw;
                }
            }
        }

        /// <summary>
        /// Move the telescope to the given local horizontal coordinates
        /// This method must be implemented if <see cref="CanSlewAltAz" /> returns True.
        /// It does not return until the slew is complete.
        /// </summary>
        public void SlewToAltAz(double Azimuth, double Altitude)
        {
            try
            {
                CheckConnected("SlewToAltAz");
                CheckParked("SlewToAltAz");
                //if (AtPark) { throw new ASCOM.ParkedException("Cannot SlewToAltAz while mount is parked"); }
                if (Tracking) { throw new ASCOM.InvalidOperationException("Cannot SlewToAltAz while Tracking"); }

                if ((Azimuth < 0) || (Azimuth > 360)) { throw new ASCOM.InvalidValueException($"Invalid Azimuth ${Azimuth}"); }
                if ((Altitude < 0) || (Altitude > 90)) { throw new ASCOM.InvalidValueException($"Invalid Altitude ${Altitude}"); }

                tl.LogMessage("SlewToAltAz", "Az: " + Azimuth.ToString() + "; Alt: " + Altitude.ToString());

                //Convert AltAz to RaDec Topocentric
                
                T.SiteLatitude = SiteLatitude;
                T.SiteLongitude = SiteLongitude;
                T.SiteElevation = SiteElevation;
                T.SiteTemperature = 20;
                T.Refraction = false;
                T.SetAzimuthElevation(Azimuth, Altitude);

                double curtargDec = 0;
                double curtargRA = 0;
                try
                {
                    curtargDec = TargetDeclination;
                }
                catch
                {
                    curtargDec = 0;
                }
                try
                {
                    curtargRA = TargetRightAscension;
                }
                catch
                {
                    curtargRA = 0;
                }

                MiscResources.SlewAltAzTrackOverride = true;
                tl.LogMessage("SlewToAltAz", "Calling SlewToCoordinates, track override enabled");
                tl.LogMessage("SlewToAltAz", "Az: " + Azimuth.ToString() + "; Alt: " + Altitude.ToString());
                tl.LogMessage("SlewToAltAz", "Derived Ra: " + utilities.HoursToHMS(T.RATopocentric, ":", ":") + "; Derived Dec: " + utilities.DegreesToDMS(T.DECTopocentric, ":", ":"));
                SlewToCoordinates(T.RATopocentric, T.DECTopocentric);
                MiscResources.SlewAltAzTrackOverride = false;
                tl.LogMessage("SlewToAltAz", "Track override disabled");

                TargetDeclination = curtargDec;
                TargetRightAscension = curtargRA;

            }
            catch (Exception ex)
            {
                tl.LogMessage("SlewToAltAz", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Move the telescope to the given local horizontal coordinates.
        /// This method must be implemented if <see cref="CanSlewAltAzAsync" /> returns True.
        /// It returns immediately, with <see cref="Slewing" /> set to True
        /// </summary>
        /// <param name="Azimuth">Azimuth to which to move</param>
        /// <param name="Altitude">Altitude to which to move to</param>
        public void SlewToAltAzAsync(double Azimuth, double Altitude)
        {
            try
            {
                CheckConnected("SlewToAltAzAsync");
                CheckParked("SlewToAltAzAsync");
                //if (AtPark) { throw new ASCOM.ParkedException("Cannot SlewToAltAzAsync while mount is parked"); }
                if (Tracking) { throw new ASCOM.InvalidOperationException("Cannot SlewToAltAzAsync while Tracking"); }
                
                if ((Azimuth < 0) || (Azimuth > 360)) { throw new ASCOM.InvalidValueException($"Invalid Azimuth ${Azimuth}"); }
                if ((Altitude < 0) || (Altitude > 90)) { throw new ASCOM.InvalidValueException($"Invalid Altitude ${Altitude}"); }

                tl.LogMessage("SlewToAltAzAsync", "Az: " + Azimuth.ToString() + "; Alt: " + Altitude.ToString());

                T.SiteLatitude = SiteLatitude;
                T.SiteLongitude = SiteLongitude;
                T.SiteElevation = SiteElevation;
                T.SiteTemperature = 20;
                T.Refraction = false;
                T.SetAzimuthElevation(Azimuth, Altitude);

                double curtargDec = 0;
                double curtargRA = 0;
                try
                {
                    curtargDec = TargetDeclination;
                }
                catch
                {
                    curtargDec = 0;
                }
                try
                {
                    curtargRA = TargetRightAscension;
                }
                catch
                {
                    curtargRA = 0;
                }

                MiscResources.SlewAltAzTrackOverride = true;
                tl.LogMessage("SlewToAltAzAsync", "Calling SlewToCoordinatesAsync, track override enabled");
                tl.LogMessage("SlewToAltAzAsync", "Az: " + Azimuth.ToString() + "; Alt: " + Altitude.ToString());
                tl.LogMessage("SlewToAltAzAsync", "Derived Ra: " + utilities.HoursToHMS(T.RATopocentric, ":", ":") + "; Derived Dec: " + utilities.DegreesToDMS(T.DECTopocentric, ":", ":"));
                SlewToCoordinatesAsync(T.RATopocentric, T.DECTopocentric);
                MiscResources.SlewAltAzTrackOverride = false;
                tl.LogMessage("SlewToAltAzAsync", "Track override disabled");

                TargetDeclination = curtargDec;
                TargetRightAscension = curtargRA;
            }
            catch (Exception ex)
            {
                tl.LogMessage("SlewToAltAzAsync", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Move the telescope to the given equatorial coordinates.  
        /// This method must be implemented if <see cref="CanSlew" /> returns True.
        /// It does not return until the slew is complete.
        /// </summary>
        public void SlewToCoordinates(double RightAscension, double Declination)
        {
            tl.LogMessage("SlewToCoordinates", "Setting Coordinates as Target and Slewing");
            try
            {
                CheckConnected("SlewToCoordinates");
                CheckParked("SlewToCoordinates");
                //if (AtPark) { throw new ASCOM.ParkedException("Cannot SlewToCoordinates while mount is parked"); }

                if (!Tracking && !MiscResources.SlewAltAzTrackOverride) { throw new ASCOM.InvalidOperationException("Cannot SlewToCoordinates while not Tracking"); }

                if ((Declination >= -90) && (Declination <= 90))
                {
                    TargetDeclination = Declination;
                }
                else
                {
                    throw new ASCOM.InvalidValueException($"Invalid Declination: {Declination}");
                }
                if ((RightAscension >= 0) && (RightAscension <=24))
                {
                    TargetRightAscension = RightAscension;
                }
                else
                {
                    throw new ASCOM.InvalidValueException($"Invalid Right Ascension: {RightAscension}");
                }
                
                SlewToTarget();
            }
            catch (Exception ex)
            {
                tl.LogMessage("SlewToCoordinates", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Move the telescope to the given equatorial coordinates.
        /// This method must be implemented if <see cref="CanSlewAsync" /> returns True.
        /// It returns immediately, with <see cref="Slewing" /> set to True
        /// </summary>
        public void SlewToCoordinatesAsync(double RightAscension, double Declination)
        {
            tl.LogMessage("SlewToCoordinatesAsync", "Setting Coordinates as Target and Slewing");
            try
            {
                CheckConnected("SlewToCoordinatesAsync");
                CheckParked("SlewToCoordinatesAsync");
                //if (AtPark) { throw new ASCOM.ParkedException("Cannot SlewToCoordinatesAsync while mount is parked"); }
                if (!Tracking && !MiscResources.SlewAltAzTrackOverride) { throw new ASCOM.InvalidOperationException("Cannot SlewToCoordinatesAsync while not Tracking"); }

                if ((Declination >= -90) && (Declination <= 90))
                {
                    TargetDeclination = Declination;
                }
                else
                {
                    throw new ASCOM.InvalidValueException($"Invalid Declination: {Declination}");
                }
                if ((RightAscension >= 0) && (RightAscension <= 24))
                {
                    TargetRightAscension = RightAscension;
                }
                else
                {
                    throw new ASCOM.InvalidValueException($"Invalid Right Ascension: {RightAscension}");
                }
                tl.LogMessage("SlewToCoordinatesAsync","Starting Async Slew: RA - " + RightAscension.ToString() + "; Dec - " + Declination.ToString());
                SlewToTargetAsync();
                tl.LogMessage("SlewToCoordinatesAsync", "Slew Commenced");
            }
            catch (Exception ex)
            {
                tl.LogMessage("SlewToCoordinatesAsync", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Move the telescope to the <see cref="TargetRightAscension" /> and <see cref="TargetDeclination" /> coordinates.
        /// This method must be implemented if <see cref="CanSlew" /> returns True.
        /// It does not return until the slew is complete.
        /// </summary>
               
        public void SlewToTarget()
        {
            tl.LogMessage("SlewToTarget", "Slewing To Target");

            try
            {
                if (!MiscResources.IsTargetSet) { throw new Exception("Target Not Set"); }
                CheckConnected("SlewToTarget");
                CheckParked("SlewToTarget");
                //if (AtPark) { throw new ASCOM.ParkedException("Cannot SlewToTarget while mount is parked"); }
                if (!Tracking && !MiscResources.SlewAltAzTrackOverride) { throw new ASCOM.InvalidOperationException("Cannot SlewToTarget while not Tracking"); }

                if (MiscResources.IsSlewingToTarget) //Are we currently in a GoTo?
                {
                    //ToDo: Decide whether to throw SlewInProgress exception or simply wait...
                    
                    throw new InvalidOperationException("Error: GoTo In Progress");                    
                    //while (MiscResources.IsSlewingToTarget) { utilities.WaitForMilliseconds(200); }
                }

                bool wasTracking = Tracking;

                double TargRA = MiscResources.Target.RightAscension;
                double TargDec = MiscResources.Target.Declination;
                //Assume Target is valid due to setting checks
                
                //bool result = CommandBool(":MS#", true);
                bool result = bool.Parse(Commander(":MS#", true, 1));
                if (result) { throw new Exception("Unable to slew:" + result + " Object Below Horizon"); }  //Need to review other implementation

                Slewing = true;
                MiscResources.IsSlewingToTarget = true;

                //If we were tracking before, TTS-160 will stop tracking on commencement of slew
                //then resume tracking when slew complete.  If TTS-160 was NOT tracking, we need to
                //implement a loop to check on slew status using mount position rates

                if (wasTracking)
                {
                    int counter = 0;
                    int RateLimit = 200; //Wait time between queries, in msec
                    int TimeLimit = 300; //How long to wait for slew to finish before throwing error, in sec
                    while (!Tracking)
                    {
                        utilities.WaitForMilliseconds(200); //limit asking rate to 0.2 Hz
                        counter++;
                        if (counter > TimeLimit * 1000 / RateLimit)
                        {
                            AbortSlew();
                            throw new ASCOM.DriverException("SlewToTarget Failed: Timeout");
                        }
                    }
                    //utilities.WaitForMilliseconds(SlewSettleTime * 1000);
                    Thread.Sleep(SlewSettleTime * 1000);
                    Slewing = false;
                    MiscResources.IsSlewingToTarget = false;
                    return;
                }
                else
                {
                    //Create loop to monitor slewing and return when done
                    utilities.WaitForMilliseconds(500); //give motors time to start
                    double resid = 1000; //some number greater than .0001 (~0.5/3600)
                    double targresid = 1000;
                    double threshold = 0.5 / 3600; //0.5 second accuracy
                    double targthreshold = 10; //start checking w/in 10 seconds of target
                    int inc = 3; //3 readings <.0001 to determine at target
                    int faultinc = 300; //Long term check used to check for no movement
                    int interval = 100; //100 msec between readings
                    var CoordOld = GetTelescopeRaAndDec();  //Get initial readings

                    while (inc >= 0)
                    {
                        utilities.WaitForMilliseconds(interval); //let the mount move a bit
                        var CoordNew = GetTelescopeRaAndDec(); //get telescope coords
                        double RADelt = CoordNew.RightAscension - CoordOld.RightAscension;
                        double DecDelt = CoordNew.Declination - CoordOld.Declination;
                        double RADeltTarg = CoordNew.RightAscension - TargRA;
                        double DecDeltTarg = CoordNew.Declination - TargDec;
                        resid = Math.Sqrt(Math.Pow(RADelt, 2) + Math.Pow(DecDelt, 2)); //need to convert this to alt/az rather than RA/Dec
                        targresid = Math.Sqrt(Math.Pow(RADeltTarg, 2) + Math.Pow(DecDeltTarg, 2));
                        if ((resid <= threshold) & (targresid <= targthreshold))
                        {
                            switch (inc)  //We are good, decrement the count
                            {
                                case 0:
                                    utilities.WaitForMilliseconds(SlewSettleTime * 1000);
                                    Slewing = false;
                                    MiscResources.IsSlewingToTarget = false;
                                    return;
                                default:
                                    inc--;
                                    break;
                            }
                        }
                        else
                        {
                            switch (inc)  //We are bad, increment the count up to 3
                            {
                                case 2:
                                    inc++;
                                    break;
                                case 1:
                                    inc++;
                                    break;
                                case 0:
                                    inc++;
                                    break;
                            }
                        }

                        if (resid <= threshold)
                        {
                            switch (faultinc)  //No motion detected, decrement the count
                            {
                                case 0:
                                    Slewing = false;
                                    MiscResources.IsSlewingToTarget = false;
                                    //CommandBlind(":Q#", true);
                                    Commander(":Q#", true, 0);
                                    throw new ASCOM.DriverException("SlewToTarget Failed");
                                default:
                                    faultinc--;
                                    break;
                            }
                        }
                        else
                        {
                            //Motion detected, increment back up
                            if (faultinc < 300)
                            {
                                faultinc++;
                            }
                        }

                        CoordOld.RightAscension = CoordNew.RightAscension;
                        CoordOld.Declination = CoordNew.Declination;
                        CoordNew.RightAscension = 0;
                        CoordNew.Declination = 0;

                    }
                }
            }
            catch (Exception ex)
            {
                tl.LogMessage("SlewToTarget", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Move the telescope to the <see cref="TargetRightAscension" /> and <see cref="TargetDeclination" />  coordinates.
        /// This method must be implemented if <see cref="CanSlewAsync" /> returns True.
        /// It returns immediately, with <see cref="Slewing" /> set to True
        /// </summary>
        public void SlewToTargetAsync()
        {

            tl.LogMessage("SlewToTargetAsync", "Slewing To Target");

            try
            {
                if (!MiscResources.IsTargetSet) { throw new ASCOM.ValueNotSetException("Target Not Set"); }
                CheckConnected("SlewToTargetAsync");
                CheckParked("SlewToTargetAsync");
                //if (AtPark) { throw new ASCOM.ParkedException("Cannot SlewToTargetAsync while mount is parked"); }
                if (!Tracking && !MiscResources.SlewAltAzTrackOverride) { throw new ASCOM.InvalidOperationException("Cannot SlewToTargetAsync while not Tracking"); }

                if (MiscResources.IsSlewingToTarget) //Are we currently in a GoTo?
                {
                    //ToDo: Decide whether to throw SlewInProgress exception or simply wait...

                    throw new ASCOM.InvalidOperationException("Error: GoTo In Progress");
                    //while (MiscResources.IsSlewingToTarget) { utilities.WaitForMilliseconds(200); }
                }

                //double TargRA = MiscResources.Target.RightAscension;
                //double TargDec = MiscResources.Target.Declination;
                //Assume Target is valid due to setting checks

                //bool result = CommandBool(":MS#", true);
                bool result = bool.Parse(Commander(":MS#", true, 1));
                if (result) { throw new ASCOM.InvalidOperationException("Unable to slew: target below horizon"); }  //Need to review other implementation

                Slewing = true;
                MiscResources.IsSlewingToTarget = true;  //Might be redundant...
                MiscResources.IsSlewingAsync = true;

            }
            catch (Exception ex)
            {
                tl.LogMessage("SlewToTargetAsync", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// True if telescope is in the process of moving in response to one of the
        /// Slew methods or the <see cref="MoveAxis" /> method, False at all other times.
        /// </summary>
        public bool Slewing
        {
            //'Slewing' query (:D#) is not implemented in TTS-160, keep track in driver.
            get
            {
                try
                {
                    CheckConnected("Slewing");
                    tl.LogMessage("Slewing - Get", "Getting Slew Status");
                    //Catching the end of an async slew event
                    //TODO - Add error checking to see if we actually ended where we wanted
                    if ((MiscResources.IsSlewing) && (MiscResources.IsSlewingAsync) && (Tracking))
                    {
                        MiscResources.IsSlewingToTarget = false;
                        MiscResources.IsSlewingAsync = false;
                        if ((SlewSettleTime > 0) && (MiscResources.SlewSettleStart == DateTime.MinValue))
                        {
                            MiscResources.SlewSettleStart = DateTime.Now;
                            tl.LogMessage("Slewing Status", false.ToString() + "; Commencing Slew Settling");
                            return MiscResources.IsSlewing;
                        }
                        else
                        {
                            tl.LogMessage("Slewing Status", false.ToString());
                            MiscResources.IsSlewing = false;
                            return false;
                        }
                    }
                    else if ((MiscResources.IsSlewing) && (MiscResources.SlewSettleStart > DateTime.MinValue))
                    {
                        TimeSpan ts = DateTime.Now.Subtract(MiscResources.SlewSettleStart);
                        if (ts.TotalSeconds >= SlewSettleTime)
                        {
                            tl.LogMessage("Slewing Status", false.ToString() + "; Slew Settling Complete");
                            MiscResources.IsSlewing = false;
                            MiscResources.SlewSettleStart = DateTime.MinValue;
                            return false;
                        }
                        else
                        {
                            tl.LogMessage("Slewing Status", false.ToString() + "; Slew Settling in progress");
                            return MiscResources.IsSlewing;
                        }
                    }
                    else
                    {
                        tl.LogMessage("Slewing Status", MiscResources.IsSlewing.ToString());
                        return MiscResources.IsSlewing;
                    }    


                }
                catch (Exception ex)
                {
                    tl.LogMessage("Slewing", $"Error: {ex.Message}");
                    throw;
                }

            }
            set
            {
                MiscResources.IsSlewing = value;
            }
        }

        /// <summary>
        /// Matches the scope's local horizontal coordinates to the given local horizontal coordinates.
        /// </summary>
        public void SyncToAltAz(double TAzimuth, double TAltitude)
        {
            try
            {
                CheckConnected("SyncToAltAz");
                CheckParked("SyncToAltAz");

                if (Tracking) { throw new ASCOM.InvalidOperationException("Cannot SyncToAltAz while Tracking"); }

                if ((TAzimuth < 0) || (TAzimuth > 360)) { throw new ASCOM.InvalidValueException($"Invalid Azimuth ${TAzimuth}"); }
                if ((TAltitude < 0) || (TAltitude > 90)) { throw new ASCOM.InvalidValueException($"Invalid Altitude ${TAltitude}"); }

                T.SiteLatitude = SiteLatitude;
                T.SiteLongitude = SiteLongitude;
                T.SiteElevation = SiteElevation;
                T.SiteTemperature = 20;
                T.Refraction = false;
                T.SetAzimuthElevation(TAzimuth, TAltitude);
             
                tl.LogMessage("SyncToAltAz", "PreSync: Ra - " + utilities.HoursToHMS(RightAscension, ":", ":") + "; Dec - " + utilities.DegreesToDMS(Declination, ":", ":"));
                SyncToCoordinates(T.RATopocentric, T.DECTopocentric);
                tl.LogMessage("SyncToAltAz", "Complete");
                tl.LogMessage("SyncToAltAz", "Target: Az - " + TAzimuth.ToString() + "; Alt - " + TAltitude.ToString());
                tl.LogMessage("SyncToAltAz", "Derived Ra: " + utilities.HoursToHMS(T.RATopocentric, ":", ":") + "; Derived Dec: " + utilities.DegreesToDMS(T.DECTopocentric, ":", ":"));
                tl.LogMessage("SyncToAltAz", "Current: Az - " + Azimuth.ToString() + "; Alt - " + Altitude.ToString());
                tl.LogMessage("SyncToAltAz", "Current: Ra - " + utilities.HoursToHMS(RightAscension, ":", ":") + "; Dec - " + utilities.DegreesToDMS(Declination, ":", ":"));
            }
            catch (Exception ex)
            {
                tl.LogMessage("SyncToAltAz", $"Error: {ex.Message}");
                throw;
            }          
        }

        /// <summary>
        /// Matches the scope's equatorial coordinates to the given equatorial coordinates.
        /// </summary>
        public void SyncToCoordinates(double TRightAscension, double TDeclination)
        {
            tl.LogMessage("SyncToCoordinates", "Setting Coordinates as Target and Syncing");
            try
            {
                CheckConnected("SyncToCoordinates");
                CheckParked("SyncToCoordinates");

                //TODO Tracking control is not implemented in TTS-160, no point in checking it <---TODO: It now is, FIX THIS?!

                if ((TDeclination >= -90) && (TDeclination <= 90))
                {
                    TargetDeclination = TDeclination;
                }
                else
                {
                    throw new ASCOM.InvalidValueException($"Invalid Declination: {TDeclination}");
                }
                if ((TRightAscension >= 0) && (TRightAscension <= 24))
                {
                    TargetRightAscension = TRightAscension;
                }
                else
                {
                    throw new ASCOM.InvalidValueException($"Invalid Right Ascension: {RightAscension}");
                }

                tl.LogMessage("SyncToCoordinates", "PreSync: Ra - " + utilities.HoursToHMS(RightAscension, ":", ":") + "; Dec - " + utilities.DegreesToDMS(Declination, ":", ":"));
                var ret = Commander(":CM#", true, 2);
                double dectoss1 = Declination;
                Thread.Sleep(200);
                double dectoss2 = Declination;
                tl.LogMessage("SyncToCoordinates", "DecCheck = " + (dectoss1 - dectoss2).ToString());
                tl.LogMessage("SyncToCoordinates", "Complete: " + ret);
                tl.LogMessage("SyncToCoordinates", "Target: Ra - " + utilities.HoursToHMS(TRightAscension,":",":") + "; Dec - " + utilities.DegreesToDMS(TDeclination,":",":"));
                tl.LogMessage("SyncToCoordinates", "Current: Ra - " + utilities.HoursToHMS(RightAscension,":",":") + "; Dec - " + utilities.DegreesToDMS(Declination,":",":"));

            }
            catch (Exception ex)
            {
                tl.LogMessage("SyncToCoordinates", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Matches the scope's equatorial coordinates to the target equatorial coordinates.
        /// </summary>
        public void SyncToTarget()
        {
            tl.LogMessage("SyncToTarget", "Syncing to Target");
            try
            {
                if (!MiscResources.IsTargetSet) { throw new Exception("Target not set"); }
                CheckConnected("SyncToTarget");
                CheckParked("SyncToTarget");

                //TODO Tracking control is not implemented in TTS-160, no point in checking it.....IT NOW IS, TODO FIX IT!

                tl.LogMessage("SyncToTarget", "PreSync: Ra - " + utilities.HoursToHMS(RightAscension, ":", ":") + "; Dec - " + utilities.DegreesToDMS(Declination, ":", ":"));
                //For some reason TTS-160 returns a message and not catching it causes
                var ret = Commander(":CM#", true, 2);  //further commands to act funny (results are 1 order off despite the
                                                       //buffer clears)
                double dectoss1 = Declination;
                Thread.Sleep(200);
                double dectoss2 = Declination;
                tl.LogMessage("SyncToTarget", "DecCheck = " + (dectoss1 - dectoss2).ToString());
                tl.LogMessage("SyncToTarget", "Complete: " + ret);
                tl.LogMessage("SyncToTarget", "Assumed Target: Ra - " + utilities.HoursToHMS(TargetRightAscension, ":", ":") + "; Dec - " + utilities.DegreesToDMS(TargetDeclination, ":", ":"));
                tl.LogMessage("SyncToTarget", "Current: Ra - " + utilities.HoursToHMS(RightAscension, ":", ":") + "; Dec - " + utilities.DegreesToDMS(Declination, ":", ":"));
            }
            catch (Exception ex)
            {
                tl.LogMessage("SyncToTarget", $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// The declination (degrees, positive North) for the target of an equatorial slew or sync operation
        /// </summary>
        public double TargetDeclination
        {

            get
            {
                //Not implemented in TTS-160, simulated in driver
                try
                {
                    CheckConnected("TargetDeclination");
                    if (MiscResources.IsTargetDecSet)
                    {
                        tl.LogMessage("TargetDeclination Get", "Target Dec Get to:" + MiscResources.Target.Declination);
                        return MiscResources.Target.Declination;
                    }
                    else
                    {
                        throw new ASCOM.InvalidOperationException("Target Declination Not Set");
                    }
                }
                catch (Exception ex)
                {
                    tl.LogMessage("Target Declination Get", $"Error: {ex.Message}");
                    throw;
                }
            }

            set
            {
                try
                {
                    tl.LogMessage("TargetDeclination Set", "Setting Target Dec");
                    CheckConnected("TargetDeclination");
                    if (value < -90)
                    {
                        throw new ASCOM.InvalidValueException("Target Declination < -90 deg");
                    }
                    else if (value > 90)
                    {
                        throw new ASCOM.InvalidValueException("Target Declination > 90 deg");
                    }
                    else
                    {
                        var targDec = utilities.DegreesToDMS(value, "*", ":");
                        bool result = false;
                        if (value >= 0)
                        {
                            result = bool.Parse(Commander($":Sd+{targDec}#", true, 1));
                        }
                        else
                        {
                            result = bool.Parse(Commander($":Sd{targDec}#", true, 1));
                        }
                    
                        if (!result ) { throw new ASCOM.InvalidValueException("Invalid Target Declination:" + targDec); }

                        tl.LogMessage("TargetDeclination Set", "Target Dec Set to:" + targDec);
                        MiscResources.Target.Declination = value;
                        if (!MiscResources.IsTargetDecSet)
                        {
                            MiscResources.IsTargetDecSet = true;
                        }
                    
                        if (MiscResources.IsTargetRASet & !MiscResources.IsTargetSet)
                        {
                            MiscResources.IsTargetSet = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    tl.LogMessage("TargetDeclination Set", $"Error: {ex.Message}");
                    throw;
                }

            }
        }     
    
        /// <summary>
        /// The right ascension (hours) for the target of an equatorial slew or sync operation
        /// </summary>
        public double TargetRightAscension
        {
            get
            {
                //Not implemented in TTS-160, simulated in driver
                try
                {
                    CheckConnected("TargetRightAscension");
                    if (MiscResources.IsTargetRASet)
                    {
                        tl.LogMessage("TargetRightAscension Get", "Target RA Get to:" + MiscResources.Target.RightAscension);
                        return MiscResources.Target.RightAscension;
                    }
                    else
                    {
                        throw new ASCOM.InvalidOperationException("Target Right Ascension not set");
                    }
                }
                catch (Exception ex)
                {
                    tl.LogMessage("Target Right Ascension Get", $"Error: {ex.Message}");
                    throw;
                }
            }
            set
            {
                tl.LogMessage("TargetRightAscension Set", "Setting Target RA");
                try
                {
                    CheckConnected("Set Target RA");
                    if (value < 0)
                    {
                        throw new ASCOM.InvalidValueException("Target RA < 0h");
                    }
                    else if (value > 24)
                    {
                        throw new ASCOM.InvalidValueException("Target RA > 24h");
                    }
                    else
                    {
                        string targRA = utilities.HoursToHMS(value, ":", ":");
                        bool result = bool.Parse(Commander(":Sr" + targRA + "#", true, 1));

                        if (!result) { throw new ASCOM.InvalidValueException("Invalid Target Right Ascension:" + targRA); }

                        tl.LogMessage("TargetRightAscension Set", "Target RA Set to:" + targRA);
                        MiscResources.Target.RightAscension = value;
                        if (!MiscResources.IsTargetRASet)
                        {
                            MiscResources.IsTargetRASet = true;
                        }
                    
                        if (MiscResources.IsTargetDecSet & !MiscResources.IsTargetSet)
                        {
                            MiscResources.IsTargetSet = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    tl.LogMessage("Target Right Ascension Set", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// The state of the telescope's sidereal tracking drive.
        /// </summary>
        public bool Tracking
        {
            get
            {
                try 
                {
                    CheckConnected("GetTracking");
                    tl.LogMessage("Tracking Get", "Retrieving Tracking Status");
                    var ret = Commander(":GW#", true, 2);
                    bool tracking = (ret[1] == 'T');
                    tl.LogMessage("Tracking Get", "Get - " + tracking.ToString());
                    return tracking;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("Tracking Get", $"Error: {ex.Message}");
                    throw;
                }

            }
            set
            {

                try
                {
                    CheckConnected("SetTracking");
                    CheckSlewing("SetTracking");

                    tl.LogMessage("Tracking Set", "Set Tracking Enabled to: " + value.ToString());
                    if (value) { Commander(":T1#", true, 0); }
                    else if (!value) { Commander(":T0#", true, 0); }
                    else { throw new ASCOM.InvalidValueException("Expected True or False, received: " + value.ToString()); }
                    MiscResources.TrackSetFollower = value;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("Tracking Set", $"Error: {ex.Message}");
                    throw;
                }

            }
        }

        /// <summary>
        /// The current tracking rate of the telescope's sidereal drive
        /// </summary>
        public DriveRates TrackingRate
        {
            get
            {
                try
                {
                    CheckConnected("TrackingRate Get");

                    tl.LogMessage("TrackingRate Get", $"{MiscResources.TrackingRateCurrent}");
                    return MiscResources.TrackingRateCurrent;
                }
                catch (Exception ex) 
                {
                    tl.LogMessage("TrackingRate Get", $"Error: {ex.Message}");
                    throw;
                }

            }
            set
            {

                try
                {

                    CheckConnected("SetTrackingRate");
                    tl.LogMessage("TrackingRate Set", "Setting Tracking Rate: " + value.ToString());
                    switch (value)
                    {
                        case DriveRates.driveSidereal:
                            Commander(":TQ#", true, 0);
                            break;

                        case DriveRates.driveLunar:
                            Commander(":TL#", true, 0);
                            break;

                        case DriveRates.driveSolar:
                            Commander(":TS#", true, 0);
                            break;

                        default:
                            throw new ASCOM.InvalidValueException("Invalid Rate: " + value.ToString());
                    
                    }
                    MiscResources.TrackingRateCurrent = value;
                    tl.LogMessage("TrackingRate Set", "Tracking Rate Set To: " + value.ToString());
                }
                catch (Exception ex)
                {
                    tl.LogMessage("TrackingRate Set", $"Error: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Returns a collection of supported <see cref="DriveRates" /> values that describe the permissible
        /// values of the <see cref="TrackingRate" /> property for this telescope type.
        /// </summary>
        public ITrackingRates TrackingRates
        {
            get
            {
                ITrackingRates trackingRates = new TrackingRates();
                tl.LogMessage("TrackingRates", "Get - ");
                foreach (DriveRates driveRate in trackingRates)
                {
                    tl.LogMessage("TrackingRates", "Get - " + driveRate.ToString());
                }
                return trackingRates;
            }
        }

        /// <summary>
        /// The UTC date/time of the telescope's internal clock
        /// </summary>
        public DateTime UTCDate
        {
            //Can set local date/time, NOT UTC per TTS-160 implementation
            //This call pulls local and converts to UTC based off of UTC value
            get
            {
                try
                {
                    CheckConnected("UTCDateGet");

                    //var localdate = CommandString(":GC#", true);
                    //var localtime = CommandString(":GL#", true);
                    //var utcoffset = CommandString(":GG#", true);
                    var localdate = Commander(":GC#", true, 2);
                    var localtime = Commander(":GL#", true, 2);
                    var utcoffset = Commander(":GG#", true, 2);

                    int mo = Int32.Parse(localdate.Substring(0, 2));
                    int da = Int32.Parse(localdate.Substring(3, 2));
                    int yr = Int32.Parse(localdate.Substring(6, 2)) + 2000;

                    int hh = Int32.Parse(localtime.Substring(0, 2));
                    int mm = Int32.Parse(localtime.Substring(3, 2));
                    int ss = Int32.Parse(localtime.Substring(6, 2));

                    double utcoffsetnum = double.Parse(utcoffset.TrimEnd('#'));

                    DateTime lcl = new DateTime(yr, mo, da, hh, mm, ss, DateTimeKind.Local);
                    DateTime utcDate = lcl.AddHours(utcoffsetnum);

                    //DateTime utcDate = DateTime.UtcNow;
                    tl.LogMessage("UTCDate", "Get - " + utcDate.ToString("MM/dd/yy HH:mm:ss")); // String.Format("MM/dd/yy HH:mm:ss", utcDate));
                    return utcDate;
                }
                catch (Exception ex)
                {
                    tl.LogMessage("UTCDate Get", $"Error: {ex.Message}");
                    throw;
                }

            }
            set
            {
                //Need to determine if there are any values we need to verify
                //For now, assume that C# will verify a DateTime value is passed
                //Does not appear to be required to do this per ASCOM standards...
                try
                {
                    CheckConnected("UTCDateSet");

                    //This converts the provided UTC value to local and updates TTS-160
                    //var utcoffset = CommandString(":GG#", true);
                    var utcoffset = Commander(":GG#", true, 2);
                    double utcoffsetnum = double.Parse(utcoffset.TrimEnd('#'));
                    DateTime localdatetime = value.AddHours((-1) * utcoffsetnum);

                    string newdate = localdatetime.ToString("MM/dd/yy");
                    //string res = CommandString(":SC" + newdate + "#", true);
                    string res = Commander(":SC" + newdate + "#", true, 2);
                    bool resBool = char.GetNumericValue(res[0]) == 1;
                    if (!resBool) { throw new ASCOM.InvalidValueException("UTC Date Set Invalid Date: " + newdate); }

                    string newtime = localdatetime.ToString("HH:mm:ss");
                    //resBool = CommandBool(":SL" + newtime + "#", true);
                    resBool = bool.Parse(Commander(":SL" + newtime + "#", true, 1));
                    //resBool = char.GetNumericValue(res[0]) == 1;
                    if (!resBool) { throw new ASCOM.InvalidValueException("UTC Date Set Invalid Time: "+ newtime); }
                }
                catch (Exception ex)
                {
                    tl.LogMessage("UTCDate Set", $"Error: {ex.Message}");
                    throw;
                }

            }
        }

        /// <summary>
        /// Takes telescope out of the Parked state.
        /// </summary>
        public void Unpark()
        {
            tl.LogMessage("Unpark", "Not implemented");
            throw new MethodNotImplementedException("Unpark");
        }

        #endregion

        #region Private properties and methods
        // here are some useful properties and methods that can be used as required
        // to help with driver development

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered. 
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
        private static void RegUnregASCOM(bool bRegister)
        {
            using (var P = new ASCOM.Utilities.Profile())
            {
                P.DeviceType = "Telescope";
                if (bRegister)
                {
                    P.Register(driverID, driverDescription);
                }
                else
                {
                    P.Unregister(driverID);
                }
            }
        }

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected
        {
            get
            {
                // TODO check that the driver hardware connection exists and is connected to the hardware
                //try
                //{
                    //if (connectedState == serialPort.Connected)
                    //{
                        //return
                    //}
                //}
                return connectedState;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        /// <param name="message"></param>
        private void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new ASCOM.NotConnectedException(message);
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we are slewing
        /// </summary>
        /// <param name="message"></param>
        private void CheckSlewing(string message)
        {
            if (Slewing)
            {
                throw new ASCOM.InvalidOperationException("Unable to " + message + " while slewing");
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we are parked
        /// </summary>
        private void CheckParked(string message)
        {
            if (AtPark) { throw new ASCOM.ParkedException("Unable to use " + message + " while parked"); }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal ProfileProperties ReadProfile()
        {

            ProfileProperties profileProperties = new ProfileProperties();
            
            using (Profile driverProfile = new Profile())
            {

                driverProfile.DeviceType = "Telescope";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, traceStateDefault));
                profileProperties.TraceLogger = tl.Enabled;
                
                comPort = driverProfile.GetValue(driverID, comPortProfileName, string.Empty, comPortDefault);
                profileProperties.ComPort = comPort;

                profileProperties.SiteElevation = Double.Parse(driverProfile.GetValue(driverID, siteElevationProfileName, string.Empty, siteElevationDefault));
                profileProperties.SlewSettleTime = Int16.Parse(driverProfile.GetValue(driverID, SlewSettleTimeName, string.Empty, SlewSettleTimeDefault));
                profileProperties.SiteLatitude = Double.Parse(driverProfile.GetValue(driverID, SiteLatitudeName, string.Empty, SiteLatitudeDefault));
                profileProperties.SiteLongitude = Double.Parse(driverProfile.GetValue(driverID, SiteLongitudeName, string.Empty, SiteLongitudeDefault));
                profileProperties.CompatMode = Int32.Parse(driverProfile.GetValue(driverID, CompatModeName, string.Empty, CompatModeDefault));
                profileProperties.CanSetGuideRatesOverride = Convert.ToBoolean(driverProfile.GetValue(driverID, CanSetGuideRatesOverrideName, string.Empty, CanSetGuideRatesOverrideDefault));
                profileProperties.SyncTimeOnConnect = Convert.ToBoolean(driverProfile.GetValue(driverID, SyncTimeOnConnectName, string.Empty, SyncTimeOnConnectDefault));
                profileProperties.GuideComp = Int32.Parse(driverProfile.GetValue(driverID, GuideCompName, string.Empty, GuideCompDefault));
                profileProperties.GuideCompMaxDelta = Int32.Parse(driverProfile.GetValue(driverID, GuideCompMaxDeltaName, string.Empty, GuideCompMaxDeltaDefault));
                profileProperties.GuideCompBuffer = Int32.Parse(driverProfile.GetValue(driverID, GuideCompBufferName, string.Empty, GuideCompBufferDefault));
                profileProperties.TrackingRateOnConnect = Int32.Parse(driverProfile.GetValue(driverID, TrackingRateOnConnectName, string.Empty, TrackingRateOnConnectDefault));
            }
            return profileProperties;
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile( ProfileProperties profileProperties )
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Telescope";

                driverProfile.WriteValue(driverID, traceStateProfileName, profileProperties.TraceLogger.ToString());
                if (!(comPort is null)) driverProfile.WriteValue(driverID, comPortProfileName, comPort.ToString());

                driverProfile.WriteValue(driverID, siteElevationProfileName, profileProperties.SiteElevation.ToString());
                driverProfile.WriteValue(driverID, SlewSettleTimeName, profileProperties.SlewSettleTime.ToString());
                driverProfile.WriteValue(driverID, SiteLatitudeName, profileProperties.SiteLatitude.ToString());
                driverProfile.WriteValue(driverID, SiteLongitudeName, profileProperties.SiteLongitude.ToString());
                driverProfile.WriteValue(driverID, CompatModeName, profileProperties.CompatMode.ToString());
                driverProfile.WriteValue(driverID, CanSetGuideRatesOverrideName, profileProperties.CanSetGuideRatesOverride.ToString());
                driverProfile.WriteValue(driverID, SyncTimeOnConnectName, profileProperties.SyncTimeOnConnect.ToString());
                driverProfile.WriteValue(driverID, GuideCompName, profileProperties.GuideComp.ToString());
                driverProfile.WriteValue(driverID, GuideCompMaxDeltaName, profileProperties.GuideCompMaxDelta.ToString());
                driverProfile.WriteValue(driverID, GuideCompBufferName, profileProperties.GuideCompBuffer.ToString());
                driverProfile.WriteValue(driverID, TrackingRateOnConnectName, profileProperties.TrackingRateOnConnect.ToString());
            }
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            tl.LogMessage(identifier, msg);
        }
        #endregion
    }
}
