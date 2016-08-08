This contains a class to use NAUDIO to access a 333D01 USB digital accelerometer via the wavein audio interface.  
It searches for a 333D01.  If found, it reads the calibration information, reads a block of acceleration time data, 
scales and returns the data calibrated g's.

It also includes a simple example code to display the data read from the sensor.

Each Digiducer contains in the name calibration information traceable back to national standards to convert the raw samples into 
engineering units of acceleration ratio.
