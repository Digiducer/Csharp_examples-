//==============================================================================
//
// Title:		DigiDataCap
// Purpose:		This class allows users to extract information and data from a digiducer
//
// Created on:	7/27/2016 at 12:54 PM by jwells
//
//
//	Free to use to support interfacing to the Digiducer
//
// Programmed in Visual Studio 2013
//
//==============================================================================

//==============================================================================
// Include files
using System;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.Windows.Forms;

namespace MyDigiRecorder
{
    class DigiDataCapture
    {
        public int bitsPerSample = 24; // Digiducer outputs 24-bit data
        public int SampleRate = 48000; // samples per second that the digiducer acquires
        WaveIn waveSource = null; // initialize Naudio wave input object used for recording 
        Single[] BuffA; // global buffer for channel A used to pass data from the DataAvailable event handler to the acquire function
        Single[] BuffB; // global buffer for channel B used to pass data from the DataAvailable event handler to the acquire function
        double GinSI = 9.80665; // value of gravity in SI units
        Single gscalefactorA; // scale factor for obtaining G values on channel A
        Single gscalefactorB; // scale factor for obtaining G values on channel B

        // Passes serial number through serialNum, calibration date through CalDate, and data buffers for each channel through ChanA and ChanB, for the first Digiducer detected
        // Data buffers scaled to G's
        // Returns
        //		-2				Unknown format with default values
        //		-1				Invalid string - may not be from a 333D01 sensor
        //		0				Older format with default values
        //		1				Valid calibration with all information present
        public int acquire(ref string serialNum, ref string CalDate, ref Single[] ChanA, ref Single[] ChanB)
        {
            
            // initialize local variables
            

            int errCode = 0; // sets default error code of 0 to indicate the device connected is a 333D. If 333D0 or MB63 is detected, code will be 1. If no digiducer is detected, the code will be -1. If Digiducer connected, but with wrong version number, code will be -2.
            int waveInDevices = WaveIn.DeviceCount; // number of recording devices connected to computer
            int sensitivityA = 0; // sensitivity later read from device name
            int sensitivityB = 0; // sensitivity later read from device name
            string fullDigiName = " "; // full device name used for extracing information about the digiducer
            int firstDigiNumber = -1; // device number for first Digiducer found by Naudio


            BuffA = ChanA; // gives BuffA the same size as ChanA 
            BuffB = ChanB; // gives BuffB the same size as ChanB

            // following code is performed if the enumeration process recognized that a Digiducer is plugged in 
            if (findFirstDigi(ref fullDigiName, ref firstDigiNumber)) // finds the first Digiducer connected using Naudio enumeration, returns the truncated name of the Digiducer and the device number
            {
                errCode = parseDigiName(fullDigiName, ref serialNum, ref CalDate, ref sensitivityA, ref sensitivityB); // parses the device name to get the serial number, calibration date, channel sensitivity, and an error code
                
                gscalefactorA = (Single)(1 / ((Single)(sensitivityA) * GinSI * 256));	// conversion of float to G's, the 256 is used to adjust for the extra byte added on to the value in the DataAvailable event handler
                gscalefactorB = (Single)(1 / ((Single)(sensitivityB) * GinSI * 256));	// conversion of float to G's, the 256 is used to adjust for the extra byte added on to the value in the DataAvailable event handler

                //WaveIn Setup
                waveSource = new WaveIn(); // initializes Naudio wave input
                waveSource.DeviceNumber = firstDigiNumber; // sets which device will be recorded
                waveSource.NumberOfBuffers = 1; // tells recording function to only record one buffer instead of the default 2 or 3
                waveSource.BufferMilliseconds = 1100 * ChanA.Length / SampleRate; // sets the size of buffer that must be filled before going to DataAvailable event, multplied by 1100 to convert to milliseconds and make it 10% longer, to ensure all of the samples are acquired
                waveSource.WaveFormat = new WaveFormat(SampleRate, bitsPerSample, 2); // sample rate: 48kHz, bits/sample: 24, channels: 2

                waveSource.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable); // enables use of filled buffer event handler
                waveSource.RecordingStopped += new EventHandler<NAudio.Wave.StoppedEventArgs>(waveSource_RecordingStopped); // enables use of stopped recording event handler

                waveSource.StartRecording(); // begins recording digiducer outputs and filling buffer

                // Waiting cycle
                for (int i = 0; i < 100; i++)
                {
                    Thread.Sleep(100); // lets recording proceed
                    Application.DoEvents(); // executes queued events
                }

                if (BuffA != null)
                {
                    ChanA = BuffA; // sets the input buffer A equal to the global buffer A to be returned
                    ChanB = BuffB; // sets the input buffer B equal to the global buffer B to be returned
                }
            }
            else errCode = -1; // sets error code to indicate no device connected
            return errCode;
        }


        //Full Recoding Buffer Event Handler
        void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            //Naudio returns 2-channel data interleaved. The data is stored in the Buffer member of the waveIn object as bytes.
            //To convert the bytes into 2-channel, 24-bit data, 3 bytes are pulled from waveIn.Buffer and converted to int for cahnnel A and then repeated for channel B
            int rawBuffIndx = 0; // indexes through the returned buffer
            byte[] bites = new byte[4]; // temporarily stores each set of bytes that are concatonated into an int. Bites[0] is always 0x00
            int temp; // used to store value of bytes converted to int
            for (int i = 0; i < BuffA.Length; i++)
            {
                bites[1] = e.Buffer[rawBuffIndx++];
                bites[2] = e.Buffer[rawBuffIndx++];
                bites[3] = e.Buffer[rawBuffIndx++]; // stores a byte of 0's with the 3 bytes (24 bits) to  create a 32 bit value, the extra 0's scale the value up by 2^8, this is adjusted for in the gscalefactor calculations
                temp = BitConverter.ToInt32(bites, 0); // converts the concatonetd bytes into and int value
                BuffA[i] = (Single)temp * gscalefactorA; // converts data types and scales the value based on the channel sensitivity
                bites[1] = e.Buffer[rawBuffIndx++];
                bites[2] = e.Buffer[rawBuffIndx++];
                bites[3] = e.Buffer[rawBuffIndx++]; // stores a byte of 0's with the 3 bytes (24 bits) to  create a 32 bit value, the extra 0's scale the value up by 2^8, this is adjusted for in the gscalefactor calculations
                temp = BitConverter.ToInt32(bites, 0); // converts the byte array into an int value
                BuffB[i] = (Single)temp * gscalefactorB; // converts data types and scales the value based on the channel sensitivity
            }
        }

        //Recording Ended Event Handler
        void waveSource_RecordingStopped(object sender, NAudio.Wave.StoppedEventArgs e)
        {
            if (waveSource != null)
            {
                waveSource.Dispose(); // clear the waveIn object
                waveSource = null;
            }
        }

        //Find the truncated name and device number of the first Digiducer found
        //Passes the first Digiducer's device name through fullDigiName, and device number through firstDevNumber
        // Returns
        //		true				Digiducer was found
        //		false				No Digiducer found
        public bool findFirstDigi(ref string fullDigiName, ref int firstDevNumber)
        {
            MMDeviceEnumerator enumerator = new MMDeviceEnumerator(); // new enumerator object
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active); // enumerates recording devices
            string[] devNames = new string[devices.Count]; // temporary array used to store and return the full name of all connected devices
            int numNames = 0; // index used to step through temporary array
            int partialNameIndx; // IndexOf() will return the index at which partialDigiName is found within connectedDevices[i]. If it is not found, -1 is returned 
            bool devFound = false;

            foreach (var device in devices)
            {
                devNames[numNames++] = device.FriendlyName; // store device name and increment index
            }

            int waveIndevs = WaveIn.DeviceCount; // gets number of devices connected
            firstDevNumber = -1;  // this value will change if a Digiducer is connected

            //Naudio enumeration process used to set the correct device number, MMDeviceEnumerator has a different enumeration order than naudio so this is process is used to 
            for (int waveInDevice = 0; waveInDevice < waveIndevs; waveInDevice++)
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice); // enumerates recording devices with naudio method
                if (deviceInfo.ProductName.Substring(12, 4) == "333D" || deviceInfo.ProductName.Substring(12, 4) == "MB63")
                {
                    firstDevNumber = waveInDevice; // sets device number variable so waveIn object records from correct device
                    devFound = true; // signifies that the same digiducer was found as before, which allows later code to run

                    // Compares the truncated name to all of the device names to find the correct full name. No two Digiducers will have the same truncated name
                    for (int i = 0; i < devNames.Length; i++)
                    {
                        partialNameIndx = devNames[i].IndexOf(deviceInfo.ProductName); // IndexOf() will return the index at which partialDigiName is found within connectedDevices[i]. If it is not found, -1 is returned 
                        if (partialNameIndx >= 0) // any number >= implies the names match
                        {
                            fullDigiName = devNames[i]; // store matching name as the correct full Digiducer name
                            break; // leaves comparison once the first match is found
                        }
                    }
                    devFound = true;
                    break; // leaves for loop after digiducer is found that matches previous digiducer
                }
            }
            return devFound;
        }

        //Parse the full name of the first Digiducer found and extracts device parameters
        //Passes the first Digiducer's serial number through serNum, calibration date through caldate, and channel sensitivities through sensA and sensB
        // Returns
        //		-2				Unknown version number
        //		0				Digiducer is a 333D
        //		1				Digiducer is a 333D0 or MB63
        public int parseDigiName(string digiName, ref string serNum, ref string caldate, ref int sensA, ref int sensB)
        {
            // nominal digiducer values, used for 333D devices
            serNum = "Nominal";
            sensA = 33000;
            sensB = 65000;
            caldate = "Unknown";

            int[] startIndex = { digiName.IndexOf("333D"), digiName.IndexOf("MB63") }; // creates array of starting locations, only one will not be -1, the value IndexOf() returns when no match found
            int ErrorCode = 0;
            if (digiName.Substring(startIndex.Max() + 7, 1) != "1") return -2;

            if (digiName.Substring(startIndex.Max(), 5) == "333D0" || digiName.Substring(startIndex.Max(), 4) == "MB63") // handles case for 333D01 and 333D02
            {
                serNum = digiName.Substring(startIndex.Max() + 9, 5); // parses device name for the first sensitivity
                sensA = Int32.Parse(digiName.Substring(startIndex.Max() + 14, 5)); // parses device name for the first sensitivity
                sensB = Int32.Parse(digiName.Substring(startIndex.Max() + 19, 5)); // parses device name for the second snestivity
                caldate = digiName.Substring(startIndex.Max() + 24, 2) + "-" + digiName.Substring(startIndex.Max() + 26, 2) + "-" + digiName.Substring(startIndex.Max() + 28, 2); // parses device name for the first sensitivity
                ErrorCode = 1; // indicatates that a 333D0 or MB63 was found
            }
            return ErrorCode;
        }
    }
}