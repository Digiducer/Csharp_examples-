//==============================================================================
//
// Title:		DigiDataGrapher
// Purpose:		Give an example of how to acquire and use data from a Digiducer
//              using DigiDataCap
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
using System.Windows.Forms;
using System.Collections;
//using DigiDataCapture;

namespace MyDigiRecorder
{
    public partial class Form1 : Form
    {
        public Form1()
        {

            InitializeComponent();
            chart1.Series.Clear();
            chart1.Series.Add("Series 1");
            chart1.Series.Add("Series 2");
            chart1.Series[0].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line; //Sets first series of chart data to line graph
            chart1.Series[1].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line; //Sets second series of chart data to line graph
        }

        private void StartBtn_Click(object sender, EventArgs e)
        {
             StartBtn.Enabled = false;
            int errorCode = 0; //Used to determine if a Digiducer was found. -1: no Digiducer found, 0: 333D found, 1: 333D0 or MB63 found
            int NumberSamples = 48000 * 5; //Determined by (samples per second) * (second to record)
            string serialNumber = " "; //Initialized here, set by dataCap.acquire. Digiducer Serial Number
            string calDate = " "; //Initialized here, set by dataCap.acquire. Date of last calibration
            Single[] ChanABuffer = new Single[NumberSamples]; //Initialized here, set by dataCap.acquire. Array of values from channel A
            Single[] ChanBBuffer = new Single[NumberSamples]; //Initialized here, set by dataCap.acquire. Array of values from channel B
            DigiDataCapture dataCap = new DigiDataCapture(); //Initialize a new digiducer data capturing Object. The reference to this class must be added to your project

            errorCode = dataCap.acquire(ref serialNumber, ref calDate, ref ChanABuffer, ref ChanBBuffer); //Finds first digiducer plugged into PC and fills ChanABuff and ChanBBuff with recorded data

            foreach (var series in chart1.Series)
            {
                series.Points.Clear(); //Clear each set of data on plot
            }
            if (errorCode == 1 || errorCode == 0 || errorCode == -2)
            {
                MessageBox.Show("SN: " + serialNumber);
                MessageBox.Show("Calibration Date: " + calDate);

                for (int j = 0; j < NumberSamples; j++)
                {
                    chart1.Series[0].Points.AddXY(j, ChanABuffer[j]); //Plots data to line graph
                    chart1.Series[1].Points.AddXY(j, ChanBBuffer[j]); //Plots data to line graph
                }
            }
        }
    }
}

