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
        private static string driverDescription = "ASCOM Telescope Driver for TTS-160";
        private Serial serialPort;

        internal static string comPortProfileName = "COM Port"; // Constants used for Profile persistence
        internal static string comPortDefault = "COM1";
        internal static string traceStateProfileName = "Trace Level";
        internal static string traceStateDefault = "false";

        internal static string comPort; // Variables to hold the current device configuration

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

        /// <summary>
        /// Variable to provide traffic control for serial communications
        /// </summary>
        private static readonly Mutex serMutex = new Mutex();

        /// <summary>
        /// Variable to provide coordinate Transforms
        /// </summary>
        private Transform T;

        /// <summary>
        /// Initializes a new instance of the <see cref="TTS160"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Telescope()
        {
            tl = new TraceLogger("", "TTS160");
            ReadProfile(); // Read device configuration from the ASCOM Profile store

            tl.LogMessage("Telescope", "Starting initialization");

            connectedState = false; // Initialise connected to false
            utilities = new Util(); //Initialise util object
            astroUtilities = new AstroUtils(); // Initialise astro-utilities object
            T = new Transform();
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

            using (SetupDialogForm F = new SetupDialogForm(tl))
            {
                var result = F.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        /// <summary>Returns the list of custom action names supported by this driver.</summary>
        /// <value>An ArrayList of strings (SafeArray collection) containing the names of supported actions.</value>
        public ArrayList SupportedActions
        {
            get
            {
                tl.LogMessage("SupportedActions Get", "Returning empty arraylist");
                return new ArrayList();
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
            LogMessage("", "Action {0}, parameters {1} not implemented", actionName, actionParameters);
            throw new ASCOM.ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
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
            CheckConnected("CommandBlind");

            try
            {
                tl.LogMessage("CommandBlind", $"raw: {raw} command {command}");
                CheckConnected("CommandBlind");

                serMutex.WaitOne();  //Ensure no collisions!
                serialPort.ClearBuffers();
                serialPort.Transmit(command);
                serMutex.ReleaseMutex();

                //utilities.WaitForMilliseconds(100); //limit transmit rate...disable this to see how Mutex does...
                tl.LogMessage("CommandBlind", "Completed");
            }
            catch (Exception ex)
            {
                tl.LogMessage("CommandBlind", $"Error: {ex.Message}");
                throw;
            }
            // TODO The optional CommandBlind method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandBlind must send the supplied command to the device and return immediately without waiting for a response

            //throw new ASCOM.MethodNotImplementedException("CommandBlind");
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
                CheckConnected("CommandBool");
                // TODO The optional CommandBool method should either be implemented OR throw a MethodNotImplementedException
                // If implemented, CommandBool must send the supplied command to the mount, wait for a response and parse this to return a True or False value

                //string retString = CommandString(command, raw); // Send the command and wait for the response

                serMutex.WaitOne();
                serialPort.ClearBuffers();
                serialPort.Transmit(command);
                var result = serialPort.ReceiveCounted(1);  //assumes that all return strings are # terminated...is this true?
                serMutex.ReleaseMutex();

                bool retBool = char.GetNumericValue(result[0]) == 1; // Parse the returned string and create a boolean True / False value
                                                                        //Does not take into account if retString[0] is not 1 or 0...            
                return retBool; // Return the boolean value to the client
            }
            catch (Exception ex)
            {
                tl.LogMessage("CommandString", $"Error: {ex.Message}");
                throw;
            }
            //throw new ASCOM.MethodNotImplementedException("CommandBool");
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
            //CheckConnected("CommandString");
            // TODO The optional CommandString method should either be implemented OR throw a MethodNotImplementedException
            // If implemented, CommandString must send the supplied command to the mount and wait for a response before returning this to the client

            //throw new ASCOM.MethodNotImplementedException("CommandString");

            try
            {
                tl.LogMessage("CommandString", $"raw: {raw} command {command}");
                CheckConnected("CommandString");

                serMutex.WaitOne();
                serialPort.ClearBuffers();
                serialPort.Transmit(command);
                var result = serialPort.ReceiveTerminated("#");  //assumes that all return strings are # terminated...is this true?
                serMutex.ReleaseMutex();

                tl.LogMessage("CommandString", $"Completed: {result}");
                //utilities.WaitForMilliseconds(100); //limit transmit rate...disabled to see if Mutex works!
                return result;
            }
            catch (Exception ex)
            {
                tl.LogMessage("CommandString", $"Error: {ex.Message}");
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
            //serMutex.Dispose();
            //serialPort.Dispose();
            //serialPort = null;

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
                tl.LogMessage("Connected", "Get {0}", IsConnected);
                return IsConnected;
            }
            set
            {
                tl.LogMessage("Connected", "Set {0}", value);
                if (value == IsConnected)
                    return;

                if (value)
                {
                    //connectedState = true;
                    try
                    {
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
                    }
                    catch (Exception ex)
                    {
                        // report any error
                        throw new ASCOM.NotConnectedException($"Serial port connection error: {ex}");
                    }

                    LogMessage("Connected Set", "Connecting to port {0}", comPort);
                }
                else
                {
                    //TODO add error handling
                    serialPort.Connected = false;
                    connectedState = false;

                    LogMessage("Connected Set", "Disconnecting from port {0}", comPort);
                    // TODO disconnect from the device
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
                string driverInfo = "Information about the driver itself. Version: " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
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
            //tl.LogMessage("AbortSlew", "Not implemented");
            //throw new MethodNotImplementedException("AbortSlew");

            try
            {

                tl.LogMessage("AbortSlew", "Aborting Slew, CommandBlind :Q#");
                CheckConnected("AbortSlew");
                CommandBlind(":Q#", true);

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
                tl.LogMessage("AlignmentMode Get", "Not implemented");
                throw new PropertyNotImplementedException("AlignmentMode", false);
            }
        }

        /// <summary>
        /// The Altitude above the local horizon of the telescope's current position (degrees, positive up)
        /// </summary>
        public double Altitude
        {
            get
            {
                //tl.LogMessage("Altitude", "Not implemented");
                //throw new PropertyNotImplementedException("Altitude", false);

                try
                {

                    tl.LogMessage("Altitude Get", "Getting Altitude");
                    CheckConnected("Altitude Get");

                    var result = CommandString(":GA#", true);
                    //:GA# Get telescope altitude
                    //Returns: DDD*MM#T or DDD*MM'SS#
                    //The current telescope Altitude depending on the selected precision.

                    double alt = utilities.DMSToDegrees(result);

                    tl.LogMessage("Azimuth Get", $"{alt}");
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
                tl.LogMessage("AtPark", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// Determine the rates at which the telescope may be moved about the specified axis by the <see cref="MoveAxis" /> method.
        /// </summary>
        /// <param name="Axis">The axis about which rate information is desired (TelescopeAxes value)</param>
        /// <returns>Collection of <see cref="IRate" /> rate objects</returns>
        public IAxisRates AxisRates(TelescopeAxes Axis)
        {
            tl.LogMessage("AxisRates", "Get - " + Axis.ToString());
            return new AxisRates(Axis);
        }

        /// <summary>
        /// The azimuth at the local horizon of the telescope's current position (degrees, North-referenced, positive East/clockwise).
        /// </summary>
        public double Azimuth
        {
            get
            {
                //tl.LogMessage("Azimuth Get", "Not implemented");
                //throw new PropertyNotImplementedException("Azimuth", false);
                try
                {

                    tl.LogMessage("Azimuth Get", "Getting Azimuth");
                    CheckConnected("Azimuth Get");

                    var result = CommandString(":GZ#", true);
                    //:GZ# Get telescope azimuth
                    //Returns: DDD*MM#T or DDD*MM'SS#
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
                tl.LogMessage("CanFindHome", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if this telescope can move the requested axis
        /// </summary>
        public bool CanMoveAxis(TelescopeAxes Axis)
        {
            tl.LogMessage("CanMoveAxis", "Get - " + Axis.ToString());
            switch (Axis)
            {
                case TelescopeAxes.axisPrimary: return true;
                case TelescopeAxes.axisSecondary: return true;
                case TelescopeAxes.axisTertiary: return false;
                default: throw new InvalidValueException("CanMoveAxis", Axis.ToString(), "0 to 2");
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed parking (<see cref="Park" />method)
        /// </summary>
        public bool CanPark
        {
            get
            {
                tl.LogMessage("CanPark", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if this telescope is capable of software-pulsed guiding (via the <see cref="PulseGuide" /> method)
        /// </summary>
        public bool CanPulseGuide
        {
            get
            {
                tl.LogMessage("CanPulseGuide", "Get - " + true.ToString());
                return true;
            }
        }

        /// <summary>
        /// True if the <see cref="DeclinationRate" /> property can be changed to provide offset tracking in the declination axis.
        /// </summary>
        public bool CanSetDeclinationRate
        {
            get
            {
                tl.LogMessage("CanSetDeclinationRate", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if the guide rate properties used for <see cref="PulseGuide" /> can ba adjusted.
        /// </summary>
        public bool CanSetGuideRates
        {
            get
            {
                tl.LogMessage("CanSetGuideRates", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed setting of its park position (<see cref="SetPark" /> method)
        /// </summary>
        public bool CanSetPark
        {
            get
            {
                tl.LogMessage("CanSetPark", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if the <see cref="SideOfPier" /> property can be set, meaning that the mount can be forced to flip.
        /// </summary>
        public bool CanSetPierSide
        {
            get
            {
                tl.LogMessage("CanSetPierSide", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if the <see cref="RightAscensionRate" /> property can be changed to provide offset tracking in the right ascension axis.
        /// </summary>
        public bool CanSetRightAscensionRate
        {
            get
            {
                tl.LogMessage("CanSetRightAscensionRate", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if the <see cref="Tracking" /> property can be changed, turning telescope sidereal tracking on and off.
        /// </summary>
        public bool CanSetTracking
        {
            get
            {
                tl.LogMessage("CanSetTracking", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed slewing (synchronous or asynchronous) to equatorial coordinates
        /// </summary>
        public bool CanSlew
        {
            get
            {
                tl.LogMessage("CanSlew", "Get - " + true.ToString());
                return true;
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed slewing (synchronous or asynchronous) to local horizontal coordinates
        /// </summary>
        public bool CanSlewAltAz
        {
            get
            {
                tl.LogMessage("CanSlewAltAz", "Get - " + true.ToString());
                return true;
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed asynchronous slewing to local horizontal coordinates
        /// </summary>
        public bool CanSlewAltAzAsync
        {
            get
            {
                tl.LogMessage("CanSlewAltAzAsync", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed asynchronous slewing to equatorial coordinates.
        /// </summary>
        public bool CanSlewAsync
        {
            get
            {
                tl.LogMessage("CanSlewAsync", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed synching to equatorial coordinates.
        /// </summary>
        public bool CanSync
        {
            get
            {
                tl.LogMessage("CanSync", "Get - " + true.ToString());
                return true;
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed synching to local horizontal coordinates
        /// </summary>
        public bool CanSyncAltAz
        {
            get
            {
                tl.LogMessage("CanSyncAltAz", "Get - " + false.ToString());
                return false;
            }
        }

        /// <summary>
        /// True if this telescope is capable of programmed unparking (<see cref="Unpark" /> method).
        /// </summary>
        public bool CanUnpark
        {
            get
            {
                tl.LogMessage("CanUnpark", "Get - " + false.ToString());
                return false;
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

                    tl.LogMessage("Declination Get", "Getting Declination");
                    CheckConnected("Declination Get");

                    var result = CommandString(":GD#", true);
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
                double declination = 0.0;
                tl.LogMessage("DeclinationRate", "Get - " + declination.ToString());
                return declination;
            }
            set
            {
                tl.LogMessage("DeclinationRate Set", "Not implemented");
                throw new PropertyNotImplementedException("DeclinationRate", true);
            }
        }

        /// <summary>
        /// Predict side of pier for German equatorial mounts at the provided coordinates
        /// </summary>
        public PierSide DestinationSideOfPier(double RightAscension, double Declination)
        {
            tl.LogMessage("DestinationSideOfPier Get", "Not implemented");
            throw new PropertyNotImplementedException("DestinationSideOfPier", false);
        }

        /// <summary>
        /// True if the telescope or driver applies atmospheric refraction to coordinates.
        /// </summary>
        public bool DoesRefraction
        {
            get
            {
                tl.LogMessage("DoesRefraction Get", "Not implemented");
                throw new PropertyNotImplementedException("DoesRefraction", false);
            }
            set
            {
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
                EquatorialCoordinateType equatorialSystem = EquatorialCoordinateType.equTopocentric;
                tl.LogMessage("DeclinationRate", "Get - " + equatorialSystem.ToString());
                return equatorialSystem;
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
                tl.LogMessage("GuideRateDeclination Get", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateDeclination", false);
            }
            set
            {
                tl.LogMessage("GuideRateDeclination Set", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateDeclination", true);
            }
        }

        /// <summary>
        /// The current Right Ascension movement rate offset for telescope guiding (degrees/sec)
        /// </summary>
        public double GuideRateRightAscension
        {
            get
            {
                tl.LogMessage("GuideRateRightAscension Get", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateRightAscension", false);
            }
            set
            {
                tl.LogMessage("GuideRateRightAscension Set", "Not implemented");
                throw new PropertyNotImplementedException("GuideRateRightAscension", true);
            }
        }

        /// <summary>
        /// True if a <see cref="PulseGuide" /> command is in progress, False otherwise
        /// </summary>
        public bool IsPulseGuiding
        {
            //Pulse Guide query is not implemented in TTS-160 => track in driver
            //TODO Must implement ASCOM Error Conditions!
            get
            {
                return MiscResources.IsPulseGuideInProgress;
                //tl.LogMessage("IsPulseGuiding Get", "Not implemented");
                //throw new PropertyNotImplementedException("IsPulseGuiding", false);
            }

            set
            {
                MiscResources.IsPulseGuideInProgress = value;
            }
        }

        /// <summary>
        /// Move the telescope in one axis at the given rate.
        /// </summary>
        /// <param name="Axis">The physical axis about which movement is desired</param>
        /// <param name="Rate">The rate of motion (deg/sec) about the specified axis</param>
        // Implementation of this function based on code from Generic Meade Driver
        public void MoveAxis(TelescopeAxes Axis, double Rate)
        {
            try
            {
                LogMessage("MoveAxis", $"Axis={Axis} rate={Rate}");
                CheckConnected("MoveAxis");
                //Park not yet implemented
                //CheckParked();

                //Rate switching via LX200 commands is not implemented in TTS-160
                /*
                var absRate = Math.Abs(Rate);

                switch (absRate)
                {
                    case 0:
                        //do nothing, it's ok this time as we're halting the slew.
                        break;
                    case 1:
                        SharedResourcesWrapper.SendBlind(Tl, "RG");
                        //:RG# Set Slew rate to Guiding Rate (slowest)
                        //Returns: Nothing
                        break;
                    case 2:
                        SharedResourcesWrapper.SendBlind(Tl, "RC");
                        //:RC# Set Slew rate to Centering rate (2nd slowest)
                        //Returns: Nothing
                        break;
                    case 3:
                        SharedResourcesWrapper.SendBlind(Tl, "RM");
                        //:RM# Set Slew rate to Find Rate (2nd Fastest)
                        //Returns: Nothing
                        break;
                    case 4:
                        SharedResourcesWrapper.SendBlind(Tl, "RS");
                        //:RS# Set Slew rate to max (fastest)
                        //Returns: Nothing
                        break;
                    default:
                        throw new InvalidValueException($"Rate {rate} not supported");
                }
                */

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

                                //SharedResourcesWrapper.MovingPrimary = false;
                                CommandBlind(":Qe#", true);
                                //:Qe# Halt eastward Slews
                                //Returns: Nothing
                                CommandBlind(":Qw#", true);
                                //:Qw# Halt westward Slews
                                //Returns: Nothing
                                break;
                            case ComparisonResult.Greater:
                                CommandBlind(":Me#", true);
                                //:Me# Move Telescope East at current slew rate
                                //Returns: Nothing
                                MiscResources.MovingPrimary = true;
                                break;
                            case ComparisonResult.Lower:
                                CommandBlind(":Mw#", true);
                                //:Mw# Move Telescope West at current slew rate
                                //Returns: Nothing
                                MiscResources.MovingPrimary = true;
                                break;
                        }

                        break;
                    case TelescopeAxes.axisSecondary:
                        switch (Rate.Compare(0))
                        {
                            case ComparisonResult.Equals:
                                //if (!MiscResources.IsGuiding)
                                //{
                                    //Not implemented in TTS-160 Driver
                                    //SetSlewingMinEndTime();
                                //}

                                MiscResources.MovingSecondary = false;
                                CommandBlind(":Qn#", true);
                                //:Qn# Halt northward Slews
                                //Returns: Nothing
                                CommandBlind(":Qs#", true);
                                //:Qs# Halt southward Slews
                                //Returns: Nothing
                                break;
                            case ComparisonResult.Greater:
                                CommandBlind(":Mn#", true);
                                //:Mn# Move Telescope North at current slew rate
                                //Returns: Nothing
                                MiscResources.MovingSecondary = true;
                                break;
                            case ComparisonResult.Lower:
                                CommandBlind(":Ms#", true);
                                //:Ms# Move Telescope South at current slew rate
                                //Returns: Nothing
                                MiscResources.MovingSecondary = true;
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
            //tl.LogMessage("MoveAxis", "Not implemented");
            //throw new MethodNotImplementedException("MoveAxis");
        }


        /// <summary>
        /// Move the telescope to its park position, stop all motion (or restrict to a small safe range), and set <see cref="AtPark" /> to True.
        /// </summary>
        public void Park()
        {
            tl.LogMessage("Park", "Not implemented");
            throw new MethodNotImplementedException("Park");
        }

        /// <summary>
        /// Moves the scope in the given direction for the given interval or time at
        /// the rate given by the corresponding guide rate property
        /// </summary>
        /// <param name="Direction">The direction in which the guide-rate motion is to be made</param>
        /// <param name="Duration">The duration of the guide-rate motion (milliseconds)</param>
        public void PulseGuide(GuideDirections Direction, int Duration)
        {

            LogMessage("PulseGuide", $"pulse guide direction {Direction} duration {Duration}");
            try
            {
                CheckConnected("PulseGuide");
                //Park is not yet implemented...
                //CheckParked();

                if (MiscResources.IsSlewingToTarget)
                    throw new InvalidOperationException("Unable to PulseGuide while slewing to target.");

                MiscResources.IsGuiding = true;
                try
                {
                    if (MiscResources.MovingPrimary &&
                        (Direction == GuideDirections.guideEast || Direction == GuideDirections.guideWest))
                        throw new InvalidOperationException("Unable to PulseGuide while moving same axis.");

                    if (MiscResources.MovingSecondary &&
                        (Direction == GuideDirections.guideNorth || Direction == GuideDirections.guideSouth))
                        throw new InvalidOperationException("Unable to PulseGuide while moving same axis.");

                    var coordinatesBeforeMove = GetTelescopeRaAndDec();

                    tl.LogMessage("PulseGuide", "Using old pulse guiding technique");
                    switch (Direction)
                    {
                        case GuideDirections.guideEast:
                            MoveAxis(TelescopeAxes.axisPrimary, 1);
                            IsPulseGuiding = true;
                            utilities.WaitForMilliseconds(Duration);
                            MoveAxis(TelescopeAxes.axisPrimary, 0);
                            IsPulseGuiding = false;
                            break;
                        case GuideDirections.guideNorth:
                            MoveAxis(TelescopeAxes.axisSecondary, 1);
                            IsPulseGuiding = true;
                            utilities.WaitForMilliseconds(Duration);
                            MoveAxis(TelescopeAxes.axisSecondary, 0);
                            IsPulseGuiding = false;
                            break;
                        case GuideDirections.guideSouth:
                            MoveAxis(TelescopeAxes.axisSecondary, -1);
                            IsPulseGuiding = true;
                            utilities.WaitForMilliseconds(Duration);
                            MoveAxis(TelescopeAxes.axisSecondary, 0);
                            IsPulseGuiding = false;
                            break;
                        case GuideDirections.guideWest:
                            MoveAxis(TelescopeAxes.axisPrimary, -1);
                            IsPulseGuiding = true;
                            utilities.WaitForMilliseconds(Duration);
                            MoveAxis(TelescopeAxes.axisPrimary, 0);
                            IsPulseGuiding = false;
                            break;
                    }

                    tl.LogMessage("PulseGuide", "pulse guide complete");

                    var coordinatesAfterMove = GetTelescopeRaAndDec();

                    tl.LogMessage("PulseGuide",
                        $"Complete Before RA: {utilities.HoursToHMS(coordinatesBeforeMove.RightAscension)} Dec:{utilities.DegreesToDMS(coordinatesBeforeMove.Declination)}");
                    tl.LogMessage("PulseGuide",
                        $"Complete After RA: {utilities.HoursToHMS(coordinatesAfterMove.RightAscension)} Dec:{utilities.DegreesToDMS(coordinatesAfterMove.Declination)}");
                }
                finally
                {
                    MiscResources.IsGuiding = false;
                }
            }
            catch (Exception ex)
            {
                tl.LogMessage("PulseGuide", $"Error performing pulse guide: {ex.Message}");
                throw;
            }
            //tl.LogMessage("PulseGuide", "Not implemented");
            //throw new MethodNotImplementedException("PulseGuide");
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

                    tl.LogMessage("Right Ascension Get", "Getting Right Ascension");
                    CheckConnected("Right Ascension Get");

                    var result = CommandString(":GR#", true);
                    //:GD# Get telescope Right Ascension
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

        /// <summary>
        /// Indicates the pointing state of the mount. Read the articles installed with the ASCOM Developer
        /// Components for more detailed information.
        /// </summary>
        public PierSide SideOfPier
        {
            get
            {
                tl.LogMessage("SideOfPier Get", "Not implemented");
                throw new PropertyNotImplementedException("SideOfPier", false);
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
                double siderealTime = 0.0; // Sidereal time return value
                if (!IsConnected)
                {


                    // Use NOVAS 3.1 to calculate the sidereal time
                    using (var novas = new NOVAS31())
                    {
                        double julianDate = utilities.DateUTCToJulian(DateTime.UtcNow);
                        novas.SiderealTime(julianDate, 0, novas.DeltaT(julianDate), GstType.GreenwichApparentSiderealTime, Method.EquinoxBased, Accuracy.Full, ref siderealTime);
                    }

                    // Adjust the calculated sidereal time for longitude using the value returned by the SiteLongitude property, allowing for the possibility that this property has not yet been implemented
                    try
                    {
                        siderealTime += SiteLongitude / 360.0 * 24.0;
                    }
                    catch (PropertyNotImplementedException) // SiteLongitude hasn't been implemented
                    {
                        // No action, just return the calculated sidereal time unadjusted for longitude
                    }
                    catch (Exception) // Some other exception occurred so return it to the client
                    {
                        throw;
                    }

                    // Reduce sidereal time to the range 0 to 24 hours
                    siderealTime = astroUtilities.ConditionRA(siderealTime);
                }
                else
                {
                    try
                    {
                        tl.LogMessage("Sidereal Time", "Get From Mount");
                        CheckConnected("SiderealTime");
                        var result = CommandString(":GS#", true).TrimEnd('#');
                        siderealTime = utilities.HMSToHours(result);
                    }
                    catch (Exception ex)
                    {
                        tl.LogMessage("SiderealTime Get", $"Error: {ex.Message}");
                        throw;
                    }

                }

                tl.LogMessage("SiderealTime", "Get - " + siderealTime.ToString());
                return siderealTime;
            }
        }

        /// <summary>
        /// The elevation above mean sea level (meters) of the site at which the telescope is located
        /// </summary>
        public double SiteElevation
        {
            get
            {
                tl.LogMessage("SiteElevation Get", "Not implemented");
                throw new PropertyNotImplementedException("SiteElevation", false);
            }
            set
            {
                tl.LogMessage("SiteElevation Set", "Not implemented");
                throw new PropertyNotImplementedException("SiteElevation", true);
            }
        }

        /// <summary>
        /// The geodetic(map) latitude (degrees, positive North, WGS84) of the site at which the telescope is located.
        /// </summary>
        public double SiteLatitude
        {
            get
            {
                tl.LogMessage("SiteLatitude Get", "Getting Site Latitude");
                try
                {

                    tl.LogMessage("SiteLatitude Get", "Getting Site Latitude");
                    CheckConnected("SiteLatitude Get");

                    var result = CommandString(":Gt#", true);
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
                //throw new PropertyNotImplementedException("SiteLatitude", false);
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

                    var result = CommandString(":Gt#", true);
                    //:Gt# Get Site Longitude
                    //Returns: sDDD*MM#

                    double siteLongitude = utilities.DMSToDegrees(result);

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
                tl.LogMessage("SlewSettleTime Get", "Not implemented");
                throw new PropertyNotImplementedException("SlewSettleTime", false);
            }
            set
            {
                tl.LogMessage("SlewSettleTime Set", "Not implemented");
                throw new PropertyNotImplementedException("SlewSettleTime", true);
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

                T.SiteLatitude = SiteLatitude;
                T.SiteLongitude = SiteLongitude;
                T.Refraction = false;
                T.SetAzimuthElevation(Azimuth, Altitude);
                SlewToCoordinates(T.RATopocentric, T.DECTopocentric);

            }
            catch (Exception ex)
            {
                tl.LogMessage("SlewToCoordinates", $"Error: {ex.Message}");
                throw;
            }
            
            //tl.LogMessage("SlewToAltAz", "Not implemented");
            //throw new MethodNotImplementedException("SlewToAltAz");
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
                     
            tl.LogMessage("SlewToAltAzAsync", "Not implemented");
            throw new MethodNotImplementedException("SlewToAltAzAsync");
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
                TargetDeclination = Declination;
                TargetRightAscension = RightAscension;
                SlewToTarget();
                //if (result == "1") { throw new Exception("Unable to slew:" + result); }
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
            tl.LogMessage("SlewToCoordinatesAsync", "Not implemented");
            throw new MethodNotImplementedException("SlewToCoordinatesAsync");
        }

        /// <summary>
        /// Move the telescope to the <see cref="TargetRightAscension" /> and <see cref="TargetDeclination" /> coordinates.
        /// This method must be implemented if <see cref="CanSlew" /> returns True.
        /// It does not return until the slew is complete.
        /// </summary>
        public async void SlewToTarget()
        {
            tl.LogMessage("SlewToTarget", "Slewing To Target");

            try
            {
                if (!MiscResources.IsTargetSet) { throw new Exception("Target Not Set"); }
                CheckConnected("SlewToTarget");
                bool result = CommandBool(":MS#", true);
                if (result) { throw new Exception("Unable to slew:" + result); }  //Need to review other implementation

                Slewing = true;
                MiscResources.IsSlewingToTarget = true;
                
                //Create loop to monitor slewing and return when done
                double resid = 1000; //some number greater than .0001 (~0.5/3600)
                double threshold = 0.5 / 3600; //0.5 second accuracy
                int inc = 3; //3 readings <.0001 to determine at target
                int interval = 50; //100 msec between readings
                var CoordOld = GetTelescopeRaAndDec();  //Get initial readings
                int timeout = 1 * 60 * 1000; //two minute timeout for task
                /*using (CancellationTokenSource cts = new CancellationTokenSource(timeout)) //Use task and cancellation token to prevent infinite loop
                {
                    CancellationToken ct = cts.Token;
                    try
                    {
                        Task task = Task.Run(() =>
                        {
                            while (inc > 0)
                            {
                                utilities.WaitForMilliseconds(interval); //let the mount move a bit
                                var CoordNew = GetTelescopeRaAndDec(); //get telescope coords
                                double RADelt = CoordNew.RightAscension - CoordOld.RightAscension;
                                double DecDelt = CoordNew.Declination - CoordOld.Declination;
                                resid = Math.Sqrt(Math.Pow(RADelt, 2) + Math.Pow(DecDelt, 2));
                                if (resid <= threshold)
                                {
                                    switch (inc)  //We are good, decrement the count
                                    {
                                        case 3:
                                            inc--;
                                            break;
                                        case 2:
                                            inc--;
                                            break;
                                        case 1:
                                            inc--;
                                            break;
                                        case 0:
                                            Slewing = false;
                                            MiscResources.IsSlewingToTarget = false;
                                            return;
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
                                ct.ThrowIfCancellationRequested();
                            }
                        }, ct);
                        await task;
                    }
                    catch (TaskCanceledException)
                    {
                        //timeout received, now what?
                        CommandBlind(":Q#", true); //We think something went wrong, stop the slew
                        Slewing = false;
                        MiscResources.IsSlewingToTarget = false;
                        throw new Exception("Slew Timeout Reached, Motion Cancelled");
                    }*/
                while (inc > 0)
                {
                    utilities.WaitForMilliseconds(interval); //let the mount move a bit
                    var CoordNew = GetTelescopeRaAndDec(); //get telescope coords
                    double RADelt = CoordNew.RightAscension - CoordOld.RightAscension;
                    double DecDelt = CoordNew.Declination - CoordOld.Declination;
                    resid = Math.Sqrt(Math.Pow(RADelt, 2) + Math.Pow(DecDelt, 2));
                    if (resid <= threshold)
                    {
                        switch (inc)  //We are good, decrement the count
                        {
                            case 3:
                                inc--;
                                break;
                            case 2:
                                inc--;
                                break;
                            case 1:
                                inc--;
                                break;
                            case 0:
                                Slewing = false;
                                MiscResources.IsSlewingToTarget = false;
                                return;
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

                    CoordOld = CoordNew;

                }

            }
            catch (Exception ex)
            {
                tl.LogMessage("SlewToTarget", $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Move the telescope to the <see cref="TargetRightAscension" /> and <see cref="TargetDeclination" />  coordinates.
        /// This method must be implemented if <see cref="CanSlewAsync" /> returns True.
        /// It returns immediately, with <see cref="Slewing" /> set to True
        /// </summary>
        public void SlewToTargetAsync()
        {
            tl.LogMessage("SlewToTargetAsync", "Not implemented");
            throw new MethodNotImplementedException("SlewToTargetAsync");
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
                return MiscResources.IsSlewing;
                //tl.LogMessage("Slewing Get", "Not implemented");
                //throw new PropertyNotImplementedException("Slewing", false);
            }
            set
            {
                MiscResources.IsSlewing = value;
            }
        }

        /// <summary>
        /// Matches the scope's local horizontal coordinates to the given local horizontal coordinates.
        /// </summary>
        public void SyncToAltAz(double Azimuth, double Altitude)
        {
            tl.LogMessage("SyncToAltAz", "Not implemented");
            throw new MethodNotImplementedException("SyncToAltAz");
        }

        /// <summary>
        /// Matches the scope's equatorial coordinates to the given equatorial coordinates.
        /// </summary>
        public void SyncToCoordinates(double RightAscension, double Declination)
        {
            tl.LogMessage("SyncToCoordinates", "Setting Coordinates as Target and Syncing");
            try
            {
                CheckConnected("SyncToCoordinates");
                TargetDeclination = Declination;
                TargetRightAscension = RightAscension;
                CommandBlind(":CM#", true);
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
                CommandBlind(":CM#", true);
                tl.LogMessage("SyncToTarget", "Complete");
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
                    if (MiscResources.IsTargetDecSet)
                    {
                        return MiscResources.Target.Declination;
                    }
                    else
                    {
                        throw new Exception("Target not set");
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
                    CheckConnected("Set Target Dec");
                    var targDec = utilities.DegreesToDMS(value, "*", ":");
                    bool result = CommandBool($":Sd+{targDec}#", true);
                    
                    if (!result ) { throw new Exception("Invalid Target Declination:" + targDec); }

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
                catch (Exception ex)
                {
                    tl.LogMessage("Target Declination Set", $"Error: {ex.Message}");
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
                    if (MiscResources.IsTargetRASet)
                    {
                        return MiscResources.Target.RightAscension;
                    }
                    else
                    {
                        throw new Exception("Target not set");
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
                    string targRA = utilities.HoursToHMS(value, ":", ":");
                    bool result = CommandBool(":Sr" + targRA + "#", true);

                    if (!result) { throw new Exception("Invalid Target Right Ascension:" + targRA); }

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
                    var ret = CommandString(":GW#", true);
                    bool tracking = (ret[1] == 'T');
                    tl.LogMessage("Tracking", "Get - " + tracking.ToString());
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
                tl.LogMessage("Tracking Set", "Not implemented");
                throw new PropertyNotImplementedException("Tracking", true);
            }
        }

        /// <summary>
        /// The current tracking rate of the telescope's sidereal drive
        /// </summary>
        public DriveRates TrackingRate
        {
            get
            {
                const DriveRates DEFAULT_DRIVERATE = DriveRates.driveSidereal;
                tl.LogMessage("TrackingRate Get", $"{DEFAULT_DRIVERATE}");
                return DEFAULT_DRIVERATE;
            }
            set
            {
                tl.LogMessage("TrackingRate Set", "Not implemented");
                throw new PropertyNotImplementedException("TrackingRate", true);
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
            //Consider writing helper functions for the conversions...
            get
            {
                DateTime utcDate = DateTime.UtcNow;
                tl.LogMessage("UTCDate", "Get - " + String.Format("MM/dd/yy HH:mm:ss", utcDate));
                return utcDate;
            }
            set
            {
                tl.LogMessage("UTCDate Set", "Not implemented");
                throw new PropertyNotImplementedException("UTCDate", true);
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
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Telescope";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, traceStateDefault));
                comPort = driverProfile.GetValue(driverID, comPortProfileName, string.Empty, comPortDefault);
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Telescope";
                driverProfile.WriteValue(driverID, traceStateProfileName, tl.Enabled.ToString());
                if (!(comPort is null)) driverProfile.WriteValue(driverID, comPortProfileName, comPort.ToString());
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
