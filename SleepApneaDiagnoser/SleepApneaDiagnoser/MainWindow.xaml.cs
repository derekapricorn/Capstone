﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

using EDF;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;

using MathWorks.MATLAB.NET.Arrays;
using MathWorks.MATLAB.NET.Utility;
using MATLAB_496;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro;
using System.Windows.Forms;
using EEGBandpower;
using PSD_Welch;
using System.Numerics;
using MathNet.Filtering;
using MathNet.Numerics;
using System.Diagnostics;

namespace SleepApneaDiagnoser
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : MetroWindow
  {
    ModelView model;

    /// <summary>
    /// Modified From Sample MahApps.Metro Project
    /// https://github.com/punker76/code-samples/blob/master/MahAppsMetroThemesSample/MahAppsMetroThemesSample/ThemeManagerHelper.cs
    /// </summary>
    public static void UseWindowsThemeColor()
    {
      byte a = ((Color)SystemParameters.WindowGlassBrush.GetValue(SolidColorBrush.ColorProperty)).A;
      byte g = ((Color)SystemParameters.WindowGlassBrush.GetValue(SolidColorBrush.ColorProperty)).G;
      byte r = ((Color)SystemParameters.WindowGlassBrush.GetValue(SolidColorBrush.ColorProperty)).R;
      byte b = ((Color)SystemParameters.WindowGlassBrush.GetValue(SolidColorBrush.ColorProperty)).B;

      // create a runtime accent resource dictionary

      var resourceDictionary = new ResourceDictionary();

      resourceDictionary.Add("HighlightColor", Color.FromArgb(a, r, g, b));
      resourceDictionary.Add("AccentColor", Color.FromArgb(a, r, g, b));
      resourceDictionary.Add("AccentColor2", Color.FromArgb(a, r, g, b));
      resourceDictionary.Add("AccentColor3", Color.FromArgb(a, r, g, b));
      resourceDictionary.Add("AccentColor4", Color.FromArgb(a, r, g, b));

      resourceDictionary.Add("HighlightBrush", new SolidColorBrush((Color)resourceDictionary["HighlightColor"]));
      resourceDictionary.Add("AccentColorBrush", new SolidColorBrush((Color)resourceDictionary["AccentColor"]));
      resourceDictionary.Add("AccentColorBrush2", new SolidColorBrush((Color)resourceDictionary["AccentColor2"]));
      resourceDictionary.Add("AccentColorBrush3", new SolidColorBrush((Color)resourceDictionary["AccentColor3"]));
      resourceDictionary.Add("AccentColorBrush4", new SolidColorBrush((Color)resourceDictionary["AccentColor4"]));
      resourceDictionary.Add("WindowTitleColorBrush", new SolidColorBrush((Color)resourceDictionary["AccentColor"]));

      resourceDictionary.Add("ProgressBrush", new LinearGradientBrush(
          new GradientStopCollection(new[]
          {
                new GradientStop((Color)resourceDictionary["HighlightColor"], 0),
                new GradientStop((Color)resourceDictionary["AccentColor3"], 1)
          }),
          new Point(0.001, 0.5), new Point(1.002, 0.5)));

      resourceDictionary.Add("CheckmarkFill", new SolidColorBrush((Color)resourceDictionary["AccentColor"]));
      resourceDictionary.Add("RightArrowFill", new SolidColorBrush((Color)resourceDictionary["AccentColor"]));

      resourceDictionary.Add("IdealForegroundColor", Colors.White);
      resourceDictionary.Add("IdealForegroundColorBrush", new SolidColorBrush((Color)resourceDictionary["IdealForegroundColor"]));
      resourceDictionary.Add("AccentSelectedColorBrush", new SolidColorBrush((Color)resourceDictionary["IdealForegroundColor"]));

      // DataGrid brushes since latest alpha after 1.1.2
      resourceDictionary.Add("MetroDataGrid.HighlightBrush", new SolidColorBrush((Color)resourceDictionary["AccentColor"]));
      resourceDictionary.Add("MetroDataGrid.HighlightTextBrush", new SolidColorBrush((Color)resourceDictionary["IdealForegroundColor"]));
      resourceDictionary.Add("MetroDataGrid.MouseOverHighlightBrush", new SolidColorBrush((Color)resourceDictionary["AccentColor3"]));
      resourceDictionary.Add("MetroDataGrid.FocusBorderBrush", new SolidColorBrush((Color)resourceDictionary["AccentColor"]));
      resourceDictionary.Add("MetroDataGrid.InactiveSelectionHighlightBrush", new SolidColorBrush((Color)resourceDictionary["AccentColor2"]));
      resourceDictionary.Add("MetroDataGrid.InactiveSelectionHighlightTextBrush", new SolidColorBrush((Color)resourceDictionary["IdealForegroundColor"]));

      // applying theme to MahApps

      var resDictName = string.Format("ApplicationAccent_{0}.xaml", Color.FromArgb(a, r, g, b).ToString().Replace("#", string.Empty));
      var fileName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), resDictName);
      using (var writer = System.Xml.XmlWriter.Create(fileName, new System.Xml.XmlWriterSettings { Indent = true }))
      {
        System.Windows.Markup.XamlWriter.Save(resourceDictionary, writer);
        writer.Close();
      }

      resourceDictionary = new ResourceDictionary() { Source = new Uri(fileName, UriKind.Absolute) };

      var newAccent = new Accent { Name = resDictName, Resources = resourceDictionary };
      ThemeManager.AddAccent(newAccent.Name, newAccent.Resources.Source);

      var application = System.Windows.Application.Current;
      var applicationTheme = ThemeManager.AppThemes.First(x => string.Equals(x.Name, "BaseLight"));
      ThemeManager.ChangeAppStyle(application, newAccent, applicationTheme);
    }

    /// <summary>
    /// Function called to populate recent files list. Called when application is first loaded and if the recent files list changes.
    /// </summary>
    public void LoadRecent()
    {
      List<string> array = model.RecentFiles.ToArray().ToList();

      itemControl_RecentEDF.Items.Clear();
      for (int x = 0; x < array.Count; x++)
        if (!itemControl_RecentEDF.Items.Contains(array[x].Split('\\')[array[x].Split('\\').Length - 1]))
          itemControl_RecentEDF.Items.Add(array[x].Split('\\')[array[x].Split('\\').Length - 1]);
    }

    /// <summary>
    /// Constructor for GUI class.
    /// </summary>
    public MainWindow()
    {
      InitializeComponent();

      model = new ModelView(this);
      this.DataContext = model;
      LoadRecent();

      try
      {
        UseWindowsThemeColor();
      }
      catch
      {
      }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
      model.WriteSettings();
    }

    // Home Tab Events
    private void TextBlock_OpenEDF_Click(object sender, RoutedEventArgs e)
    {
      model.LoadedEDFFile = null;

      Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
      dialog.Filter = "EDF files (*.edf)|*.edf";
      dialog.Title = "Select an EDF file";

      if (dialog.ShowDialog() == true)
      {
        model.LoadEDFFile(dialog.FileName);
      }
    }
    private void TextBlock_Recent_Click(object sender, RoutedEventArgs e)
    {
      model.LoadedEDFFile = null;

      List<string> array = model.RecentFiles.ToArray().ToList();
      List<string> selected = array.Where(temp => temp.Split('\\')[temp.Split('\\').Length - 1] == ((Hyperlink)sender).Inlines.FirstInline.DataContext.ToString()).ToList();

      if (selected.Count == 0)
      {
        this.ShowMessageAsync("Error", "File not Found");
        LoadRecent();
      }
      else
      {
        for (int x = 0; x < selected.Count; x++)
        {
          if (File.Exists(selected[x]))
          {
            model.LoadEDFFile(selected[x]);
            break;
          }
          else
          {
            this.ShowMessageAsync("Error", "File not Found");
            model.RecentFiles_Remove(selected[x]);
          }
        }
      }
    }

    // Preview Tab Events   
    private void listBox_SignalSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      model.SetSelectedSignals(listBox_SignalSelect.SelectedItems);
    }
    private void comboBox_SignalSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (comboBox_SignalSelect.SelectedValue != null)
      {
        EDFSignal edfsignal = model.LoadedEDFFile.Header.Signals.Find(temp => temp.Label.Trim() == comboBox_SignalSelect.SelectedValue.ToString().Trim());
        textBox_SampRecord.Text = ((int)((double)edfsignal.NumberOfSamplesPerDataRecord / (double)model.LoadedEDFFile.Header.DurationOfDataRecordInSeconds)).ToString();
      }
      else
      {
        textBox_SampRecord.Text = "";
      }
    }

    private void toggleButton_UseAbsoluteTime_Checked(object sender, RoutedEventArgs e)
    {
      timePicker_From_Abs.Visibility = Visibility.Visible;
      timePicker_From_Eph.Visibility = Visibility.Hidden;
    }
    private void toggleButton_UseAbsoluteTime_Unchecked(object sender, RoutedEventArgs e)
    {
      timePicker_From_Abs.Visibility = Visibility.Hidden;
      timePicker_From_Eph.Visibility = Visibility.Visible;
    }

    private void button_HideSignals_Click(object sender, RoutedEventArgs e)
    {
      model.HideSignals();
    }
    private void button_AddDerivative_Click(object sender, RoutedEventArgs e)
    {
      model.AddDerivative();
    }
    private void button_RemoveDerivative_Click(object sender, RoutedEventArgs e)
    {
      model.RemoveDerivative();
    }
    private void button_Categories_Click(object sender, RoutedEventArgs e)
    {
      model.ManageCategories();
    }
    private void button_Next_Click(object sender, RoutedEventArgs e)
    {
      model.NextCategory();
    }
    private void button_Prev_Click(object sender, RoutedEventArgs e)
    {
      model.PreviousCategory();
    }

    private void export_button_Click(object sender, RoutedEventArgs e)
    {
      model.ExportSignals();
    }

    // Analysis Tab Events 
    private void button_PerformRespiratoryAnalysis_Click(object sender, RoutedEventArgs e)
    {
      model.PerformRespiratoryAnalysisEDF();
    }
    private void button_PerformEEGAnalysis_Click(object sender, RoutedEventArgs e)
    {
      model.PerformEEGAnalysisEDF();
    }

    // Tool Tab Events
    private void button_PerformCoherenceAnalysis_Click(object sender, RoutedEventArgs e)
    {
      model.PerformCoherenceAnalysisEDF();
    }

    private void button_Load_Respiratory_Click(object sender, RoutedEventArgs e)
    {
      model.PerformRespiratoryAnalysisBinary();
    }

    private void Button_EEG_From_Bin(object sender, RoutedEventArgs e)
    {
      model.PerformEEGAnalysisBinary();
    }

    private void button_Settings_Click(object sender, RoutedEventArgs e)
    {
      model.OpenSettings();
    }
  }



  public class ModelView : INotifyPropertyChanged
  {
    #region Models

    /// <summary>
    /// Model for variables used exclusively in the 'Preview' tab
    /// </summary>
    public class PreviewModel
    {
      /// <summary>
      /// The index of the current category to be displayed in the signal selection list
      /// -1 denotes displaying the 'All' category
      /// </summary>
      public int PreviewCurrentCategory = -1;
      /// <summary>
      /// The list of user selected signals in the signal selection list
      /// </summary>
      public List<string> PreviewSelectedSignals = new List<string>();

      /// <summary>
      /// If true, the user selection for the start time of the plot should be date and time
      /// the user selection for the period of the plot should be in seconds
      /// If false, the user selection for the start time of the plot should be 30s epochs
      /// the user selection for the period of the plot should be in epochs
      /// </summary>
      public bool PreviewUseAbsoluteTime = false;
      /// <summary>
      /// The user selected start time of the plot in Date and Time format
      /// </summary>
      public DateTime PreviewViewStartTime = new DateTime();
      /// <summary>
      /// The user selected start time of the plot in epochs
      /// </summary>
      public int PreviewViewStartRecord = 0;
      /// <summary>
      /// The user selected period of the plot in seconds
      /// </summary>
      public int PreviewViewDuration = 0;
      /// <summary>
      /// The currently displayed preview plot
      /// </summary>
      public PlotModel PreviewSignalPlot = null;
      /// <summary>
      /// If false, the plot is currently being drawn and the tab should be disabled
      /// If true, the plot is done being drawn
      /// </summary>
      public bool PreviewNavigationEnabled = false;
    }

    /// <summary>
    /// Model for variables used exclusively in the 'Respiratory' sub tab of the 'Analysis' tab
    /// </summary>
    public class RespiratoryModel
    {
      /// <summary>
      /// The user selected signal to perform respiratory analysis on
      /// </summary>
      public string RespiratoryEDFSelectedSignal;
      /// <summary>
      /// The user selected start time for the respiratory analysis in 30s epochs
      /// </summary>
      public int RespiratoryEDFStartRecord;
      /// <summary>
      /// The user selected period for the respiratory analysis in 30s epochs
      /// </summary>
      public int RespiratoryEDFDuration;
      /// <summary>
      /// The respiratory analysis plot to be displayed
      /// </summary>
      public PlotModel RespiratorySignalPlot = null;
      /// <summary>
      /// The calculated mean average of the periods of the respiratory signal
      /// </summary>
      public string RespiratoryBreathingPeriodMean;
      /// <summary>
      /// The calculated median average of the periods of the respiratory signal
      /// </summary>
      public string RespiratoryBreathingPeriodMedian;
      /// <summary>
      /// A user selected option for setting the sensitivity of the peak detection of the analysis
      /// Effect where the insets, onsets, and peaks are detected
      /// Any "spike" that is less wide than the user setting in ms will be ignored
      /// </summary>
      public int RespiratoryMinimumPeakWidth = 500;
      /// <summary>
      /// A user selected option for choosing whether the analysis will allow for repeated peaks of
      /// the same polarity
      /// </summary>
      public bool RespiratoryRemoveMultiplePeaks = true;
    }

    /// <summary>
    /// Model for variables used exclusively in the 'EEG' sub tab of the 'Analysis' tab
    /// </summary>
    public class EEGModel
    {
      /// <summary>
      /// The user selected signal to perform eeg analysis on
      /// </summary>
      public string EEGEDFSelectedSignal;
      /// <summary>
      /// The user selected start time for the eeg analysis in 30s epochs
      /// </summary>
      public int EEGEDFStartRecord;
      /// <summary>
      /// The user selected period for the eeg analysis in 30s epochs
      /// </summary>
      public int EEGEDFDuration;
      /// <summary>
      /// The eeg analysis plot to be displayed
      /// </summary>
      public PlotModel PlotAbsPwr = null;
      /// <summary>
      /// Displays the eeg absolute power plot
      /// </summary>
      public PlotModel PlotRelPwr = null;
      /// <summary>
      /// Displays the eeg relative power plot
      /// </summary>
      public PlotModel PlotSpecGram = null;
      /// <summary>
      /// Displays the eeg spectrogram power plot
      /// </summary>
      public PlotModel PlotPSD = null;
      /// <summary>
      /// Displays the eeg spectrogram power plot
      /// </summary>
    }

    /// <summary>
    /// Model for variables used exclusively in the 'Coherence' sub tab of the 'Tool' tab
    /// </summary>
    public class CoherenceModel
    {
      /// <summary>
      /// The first selected signal
      /// </summary>
      public string CoherenceEDFSelectedSignal1;
      /// <summary>
      /// The second selected signal
      /// </summary>
      public string CoherenceEDFSelectedSignal2;
      /// <summary>
      /// The start time in 30s epochs of the signals to perform coherence analysis on
      /// </summary>
      public int CoherenceEDFStartRecord;
      /// <summary>
      /// The duration in 30s epochs of the signals to perform coherence analysis on
      /// </summary>
      public int CoherenceEDFDuration;
      /// <summary>
      /// A time domain plot of the first signal to perform coherence analysis on
      /// </summary>
      public PlotModel CoherenceSignalPlot1 = null;
      /// <summary>
      /// A time domain plot of the second signal to perform coherence analysis on
      /// </summary>
      public PlotModel CoherenceSignalPlot2 = null;
      /// <summary>
      /// The plot of the coherence signal
      /// </summary>
      public PlotModel CoherencePlot = null;
      /// <summary>
      /// If true, the progress ring should be shown
      /// If false, the progress ring should not be shown
      /// </summary>
      public bool CoherenceProgressRingEnabled = false;
    }
    #endregion

    #region Helper Functions 

    /******************************************************* STATIC FUNCTIONS *******************************************************/

    /// <summary>
    /// The definition of epochs in seconds
    /// </summary>
    private static int EPOCH_SEC = 30;
    /// <summary>
    /// Converts an epoch point in time to a DateTime structure
    /// </summary>
    /// <param name="epoch"> The epoch point in time to convert </param>
    /// <param name="file"> 
    /// The EDFFile class used to determine the start 
    /// DateTime corresponding to epoch 0 
    /// </param>
    /// <returns> A DateTime structure corresponding the input epoch point in time </returns>
    private static DateTime EpochtoDateTime(int epoch, EDFFile file)
    {
      // DateTime = StartTime + epoch * EPOCH_SEC
      return file.Header.StartDateTime + new TimeSpan(0, 0, epoch * EPOCH_SEC);
    }
    /// <summary>
    /// Converts an epoch duration into a TimeSpan structure
    /// </summary>
    /// <param name="period"> The epoch duration to convert </param>
    /// <returns> The TimeSpan structure corresponding to the epoch duration </returns>
    private static TimeSpan EpochPeriodtoTimeSpan(int period)
    {
      // TimeSpan = period * EPOCH_SEC
      return new TimeSpan(0, 0, 0, period * EPOCH_SEC);
    }
    /// <summary>
    /// Converts a DateTime structure into an epoch point in time
    /// </summary>
    /// <param name="time"> The DateTime structure to convert </param>
    /// <param name="file"> 
    /// The EDFFile class used to determine the start
    /// DateTime corresponding to epoch 0 
    /// </param>
    /// <returns> The epoch point in time corresponding to the input DateTime </returns>
    private static int DateTimetoEpoch(DateTime time, EDFFile file)
    {
      // epoch = (DateTime - StartTime) / EPOCH_SEC
      return (int)((time - file.Header.StartDateTime).TotalSeconds / (double)EPOCH_SEC);
    }
    /// <summary>
    /// Converts a TimeSpan structure into an epoch duration
    /// </summary>
    /// <param name="period"> The TimeSpan structure to convert </param>
    /// <returns> The epoch duration corresponding to the input TimeSpan </returns>
    private static int TimeSpantoEpochPeriod(TimeSpan period)
    {
      // epoch = TimeSpan / EPOCH_SEC
      return (int)(period.TotalSeconds / (double)EPOCH_SEC);
    }

    /// <summary>
    /// Gets a value at a specified percentile from an array
    /// </summary>
    /// <param name="values_array"> The input array </param>
    /// <param name="percentile"> The percentile of the desired value </param>
    /// <returns> The desired value at the specified percentile </returns>
    private static double? GetPercentileValue(float[] values_array, int percentile)
    {
      // Sort values in ascending order
      List<float> values = values_array.ToList();
      values.Sort();

      // index = percent * length 
      int index = (int)((double)percentile / (double)100 * (double)values.Count);

      // return desired value
      return values[index];
    }
    /// <summary>
    /// Gets a value at a specified percentile from the difference between two arrays
    /// </summary>
    /// <param name="values_array_1"> The input minuend array </param>
    /// <param name="values_array_2"> The input subtrahend array </param>
    /// <param name="percentile"> The percentile of the desired value </param>
    /// <returns> The desired value at the specified percentile </returns>
    private static double? GetPercentileValueDeriv(float[] values_array_1, float[] values_array_2, int percentile)
    {
      // Subtract two input arrays from each other
      List<float> values1 = values_array_1.ToList();
      List<float> values2 = values_array_2.ToList();
      List<float> values = new List<float>();
      for (int x = 0; x < Math.Min(values_array_1.Length, values_array_2.Length); x++)
        values.Add(values_array_1[x] - values_array_2[x]);

      // Call GetPercentileValue on difference
      return GetPercentileValue(values.ToArray(), percentile);
    }

    /// <summary>
    /// Gets the signal samples from one period of time to another
    /// </summary>
    /// <param name="file"> The EDFFile class </param>
    /// <param name="signal_to_retrieve"> The signal to get samples from </param>
    /// <param name="StartTime"> The start time to get samples from </param>
    /// <param name="EndTime"> The end time to get samples from </param>
    /// <returns> A list of the retrieved samples </returns>
    private static List<float> retrieveSignalSampleValuesMod(EDFFile file, EDFSignal signal_to_retrieve, DateTime StartTime, DateTime EndTime)
    {
      int start_sample, start_record;
      int end_sample, end_record;
      #region Find Start and End Points
      // Duration of record in seconds
      double record_duration = file.Header.DurationOfDataRecordInSeconds;
      // Samples per record
      double samples_per_record = signal_to_retrieve.NumberOfSamplesPerDataRecord;
      // The sample period of the signal (Duration of Record)/(Samples per Record)
      double sample_period = record_duration / samples_per_record;
      {
        // Time of start point in seconds
        double total_seconds = (StartTime - file.Header.StartDateTime).TotalSeconds;
        // Time of start point in samples 
        double total_samples = total_seconds / sample_period;

        start_sample = ((int)(total_samples)) % ((int)samples_per_record); // Start Sample in Record
        start_record = (int)((total_samples - start_sample) / samples_per_record); // Start Record
      }
      {
        // Time of end point in seconds
        double total_seconds = (EndTime - file.Header.StartDateTime).TotalSeconds;
        // Time of end point in samples
        double total_samples = total_seconds / sample_period - 1;

        end_sample = ((int)total_samples) % ((int)samples_per_record); // End Sample in Record
        end_record = (((int)total_samples) - end_sample) / ((int)samples_per_record); // End Record
      }
      #endregion
      List<float> signalSampleValues = new List<float>();
      if (file.Header.Signals.Contains(signal_to_retrieve))
      {
        for (int x = start_record; x <= end_record; x++)
        {
          EDFDataRecord dr = file.DataRecords[x];
          foreach (EDFSignal signal in file.Header.Signals)
          {
            if (signal.IndexNumberWithLabel.Equals(signal_to_retrieve.IndexNumberWithLabel))
            {
              int start = x == start_record ? start_sample : 0;
              int end = x == end_record ? end_sample : dr[signal.IndexNumberWithLabel].Count - 1;
              for (int y = start; y <= end; y++)
              {
                signalSampleValues.Add(dr[signal.IndexNumberWithLabel][y]);
              }
            }
          }
        }
      }
      return signalSampleValues;
    }
    /// <summary>
    /// Performs upsampling and downsampling on an array of values
    /// </summary>
    /// <param name="values"> The input array to resample </param>
    /// <param name="ratio"> The ratio between upsampling and downsampling to perform </param>
    /// <returns> The resampled array </returns>
    private static List<float> MATLAB_Resample(float[] values, float ratio)
    {
      // Prepare Input for MATLAB function
      Processing proc = new Processing();
      MWArray[] input = new MWArray[2];
      input[0] = new MWNumericArray(values);
      input[1] = ratio;
      // Call MATLAB function
      return (
                  (double[])(
                      (MWNumericArray)proc.m_resample(1, input[0], input[1])[0]
                  ).ToVector(MWArrayComponent.Real)
                ).ToList().Select(temp => (float)temp).ToList();
    }
    /// <summary>
    /// Performs coherence analysis on 2 lists of values
    /// </summary>
    /// <param name="values1"> First list of values </param>
    /// <param name="values2"> Second list of values </param>
    /// <returns> Index 1 is Y axis, Index 2 is X axis </returns>
    private static LineSeries MATLAB_Coherence(float[] values1, float[] values2)
    {
      // Prepare Input for MATLAB function
      Processing proc = new Processing();
      MWArray[] input = new MWArray[3];
      input[0] = new MWNumericArray(values1.ToArray());
      input[1] = new MWNumericArray(values2.ToArray());
      input[2] = Math.Round(Math.Sqrt(Math.Max(values1.Length, values2.Length)));

      // Call MATLAB function
      MWArray[] output = proc.m_cohere(2, input[0], input[1], input[2]);
      double[] y_values = (double[])((MWNumericArray)output[0]).ToVector(MWArrayComponent.Real);
      double[] x_values = (double[])((MWNumericArray)output[1]).ToVector(MWArrayComponent.Real);

      LineSeries series = new LineSeries();
      for (int x = 0; x < y_values.Length; x++)
        series.Points.Add(new DataPoint(x_values[x], y_values[x]));

      return series;
    }

    /***************************************************** NON-STATIC FUNCTIONS *****************************************************/

    /// <summary>
    /// From a signal, returns a series of X,Y values for use with a PlotModel
    /// Also returns y axis information and the sample_period of the signal
    /// </summary>
    /// <param name="sample_period"> Variable to contain the sample period of the signal </param>
    /// <param name="max_y"> Variable to contain the maximum y axis value </param>
    /// <param name="min_y"> Variable to contain the minimum y axis value </param>
    /// <param name="Signal"> The input signal name </param>
    /// <param name="StartTime">  The input start time to be contained in the series </param>
    /// <param name="EndTime"> The input end time to be contained in the series </param>
    /// <returns> The series of X,Y values to draw on the plot </returns>
    private LineSeries GetSeriesFromSignalName(out float sample_period, out double? max_y, out double? min_y, string Signal, DateTime StartTime, DateTime EndTime)
    {
      // Variable To Return
      LineSeries series = new LineSeries();

      if (LoadedEDFFile.Header.Signals.Find(temp => temp.Label.Trim() == Signal.Trim()) != null) // Normal EDF Signal
      {
        // Get Signal
        EDFSignal edfsignal = LoadedEDFFile.Header.Signals.Find(temp => temp.Label.Trim() == Signal);

        // Determine Array Portion
        sample_period = (float)LoadedEDFFile.Header.DurationOfDataRecordInSeconds / (float)edfsignal.NumberOfSamplesPerDataRecord;

        // Get Array
        List<float> values = retrieveSignalSampleValuesMod(LoadedEDFFile, edfsignal, StartTime, EndTime);

        // Determine Y Axis Bounds
        min_y = GetMinSignalValue(Signal, values);
        max_y = GetMaxSignalValue(Signal, values);

        // Add Points to Series
        for (int y = 0; y < values.Count; y++)
        {
          series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(StartTime + new TimeSpan(0, 0, 0, 0, (int)(sample_period * (float)y * 1000))), values[y]));
        }
      }
      else // Derivative Signal
      {
        // Get Signals
        string[] deriv_info = p_DerivedSignals.Find(temp => temp[0] == Signal);
        EDFSignal edfsignal1 = LoadedEDFFile.Header.Signals.Find(temp => temp.Label.Trim() == deriv_info[1].Trim());
        EDFSignal edfsignal2 = LoadedEDFFile.Header.Signals.Find(temp => temp.Label.Trim() == deriv_info[2].Trim());

        // Get Arrays and Perform Resampling if needed
        List<float> values1;
        List<float> values2;
        if (edfsignal1.NumberOfSamplesPerDataRecord == edfsignal2.NumberOfSamplesPerDataRecord) // No resampling
        {
          values1 = retrieveSignalSampleValuesMod(LoadedEDFFile, edfsignal1, StartTime, EndTime);
          values2 = retrieveSignalSampleValuesMod(LoadedEDFFile, edfsignal2, StartTime, EndTime);
          sample_period = (float)LoadedEDFFile.Header.DurationOfDataRecordInSeconds / (float)edfsignal1.NumberOfSamplesPerDataRecord;
        }
        else if (edfsignal1.NumberOfSamplesPerDataRecord > edfsignal2.NumberOfSamplesPerDataRecord) // Upsample signal 2
        {
          values1 = retrieveSignalSampleValuesMod(LoadedEDFFile, edfsignal1, StartTime, EndTime);
          values2 = retrieveSignalSampleValuesMod(LoadedEDFFile, edfsignal2, StartTime, EndTime);
          values2 = MATLAB_Resample(values2.ToArray(), edfsignal1.NumberOfSamplesPerDataRecord / edfsignal2.NumberOfSamplesPerDataRecord);
          sample_period = (float)LoadedEDFFile.Header.DurationOfDataRecordInSeconds / (float)edfsignal1.NumberOfSamplesPerDataRecord;
        }
        else // Upsample signal 1
        {
          values1 = retrieveSignalSampleValuesMod(LoadedEDFFile, edfsignal1, StartTime, EndTime);
          values2 = retrieveSignalSampleValuesMod(LoadedEDFFile, edfsignal2, StartTime, EndTime);
          values1 = MATLAB_Resample(values1.ToArray(), edfsignal2.NumberOfSamplesPerDataRecord / edfsignal1.NumberOfSamplesPerDataRecord);
          sample_period = (float)LoadedEDFFile.Header.DurationOfDataRecordInSeconds / (float)edfsignal2.NumberOfSamplesPerDataRecord;
        }

        // Get Y Axis Bounds
        min_y = GetMinSignalValue(Signal, values1, values2);
        max_y = GetMaxSignalValue(Signal, values1, values2);
        // Add Points to Series
        for (int y = 0; y < Math.Min(values1.Count, values2.Count); y++)
        {
          series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(StartTime + new TimeSpan(0, 0, 0, 0, (int)(sample_period * (float)y * 1000))), values1[y] - values2[y]));
        }
      }

      return series;
    }

    #endregion

    #region Actions

    /*********************************************************** HOME TAB ***********************************************************/

    // Load EDF File
    /// <summary>
    /// Used to control progress bar shown when edf file is being loaded
    /// </summary>
    private ProgressDialogController controller;
    /// <summary>
    /// Background task that updates the progress bar
    /// </summary>
    private BackgroundWorker bw_progressbar = new BackgroundWorker();
    private void BW_LoadEDFFileUpDateProgress(object sender, DoWorkEventArgs e)
    {
      long process_start = Process.GetCurrentProcess().PagedMemorySize64;
      long file_size = (long) (new FileInfo(e.Argument.ToString()).Length * 2.2);
      long current_progress = 0;

      while (!bw_progressbar.CancellationPending)
      {
        current_progress = Math.Max(current_progress, Process.GetCurrentProcess().PagedMemorySize64 - process_start);
        double progress = Math.Min(99, (current_progress * 100 / (double) file_size));

        controller.SetProgress(progress);
      }
    }
    /// <summary>
    /// Background process for loading edf file
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BW_LoadEDFFile(object sender, DoWorkEventArgs e)
    {
      // Progress Bar should not be cancelable
      controller.SetCancelable(false);
      controller.Maximum = 100;

      // 'Update Progress Bar' Task 
      bw_progressbar = new BackgroundWorker();
      bw_progressbar.WorkerSupportsCancellation = true;
      bw_progressbar.DoWork += BW_LoadEDFFileUpDateProgress;
      bw_progressbar.RunWorkerAsync(e.Argument.ToString());

      // Read EDF File
      EDFFile temp = new EDFFile();
      temp.readFile(e.Argument.ToString());
      LoadedEDFFile = temp;

      // Load Settings Files
      LoadSettings();

      // End 'Update Progress Bar' Task 
      bw_progressbar.CancelAsync();
      while (bw_progressbar.IsBusy)
      { }
    }
    /// <summary>
    /// Function called after background process for loading edf file finishes
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void BW_FinishLoad(object sender, RunWorkerCompletedEventArgs e)
    {
      // Add loaded EDF file to Recent Files list
      RecentFiles_Add(LoadedEDFFileName);

      // Close progress bar and display message
      await controller.CloseAsync();
      await p_window.ShowMessageAsync("Success!", "EDF file loaded");
    }
    /// <summary>
    /// Loads an EDF File into memory
    /// </summary>
    /// <param name="fileNameIn"> Path to the EDF file to load </param>
    public async void LoadEDFFile(string fileNameIn)
    {
      controller = await p_window.ShowProgressAsync("Please wait...", "Loading EDF File: " + fileNameIn);

      LoadedEDFFileName = fileNameIn;
      BackgroundWorker bw = new BackgroundWorker();
      bw.DoWork += BW_LoadEDFFile;
      bw.RunWorkerCompleted += BW_FinishLoad;
      bw.RunWorkerAsync(LoadedEDFFileName);
    }

    /********************************************************* PREVIEW TAB **********************************************************/

    /// <summary>
    /// Background process for drawing preview chart
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BW_CreateChart(object sender, DoWorkEventArgs e)
    {
      // Create temporary plot model class
      PlotModel temp_PreviewSignalPlot = new PlotModel();
      temp_PreviewSignalPlot.Series.Clear();
      temp_PreviewSignalPlot.Axes.Clear();

      if (pm.PreviewSelectedSignals.Count > 0)
      {
        // Create X Axis and add to plot model
        DateTimeAxis xAxis = new DateTimeAxis();
        xAxis.Key = "DateTime";
        xAxis.Minimum = DateTimeAxis.ToDouble(PreviewViewStartTime);
        xAxis.Maximum = DateTimeAxis.ToDouble(PreviewViewEndTime);
        temp_PreviewSignalPlot.Axes.Add(xAxis);

        // In the background create series for every chart to be displayed
        List<BackgroundWorker> bw_array = new List<BackgroundWorker>();
        LineSeries[] series_array = new LineSeries[pm.PreviewSelectedSignals.Count];
        LinearAxis[] axis_array = new LinearAxis[pm.PreviewSelectedSignals.Count];
        for (int x = 0; x < pm.PreviewSelectedSignals.Count; x++)
        {
          BackgroundWorker bw = new BackgroundWorker();
          bw.DoWork += new DoWorkEventHandler(
            delegate (object sender1, DoWorkEventArgs e1)
            {
            // Get Series for each signal
            int y = (int)e1.Argument;
              double? min_y, max_y;
              float sample_period;
              LineSeries series = GetSeriesFromSignalName(out sample_period,
                                                          out max_y,
                                                          out min_y,
                                                          pm.PreviewSelectedSignals[y],
                                                          (PreviewViewStartTime ?? new DateTime()),
                                                          PreviewViewEndTime
                                                          );
              series.YAxisKey = pm.PreviewSelectedSignals[y];
              series.XAxisKey = "DateTime";

            // Create Y Axis for each signal
            LinearAxis yAxis = new LinearAxis();
              yAxis.MajorGridlineStyle = LineStyle.Solid;
              yAxis.MinorGridlineStyle = LineStyle.Dot;
              yAxis.Title = pm.PreviewSelectedSignals[y];
              yAxis.Key = pm.PreviewSelectedSignals[y];
              yAxis.EndPosition = (double)1 - (double)y * ((double)1 / (double)pm.PreviewSelectedSignals.Count);
              yAxis.StartPosition = (double)1 - (double)(y + 1) * ((double)1 / (double)pm.PreviewSelectedSignals.Count);
              yAxis.Maximum = max_y ?? Double.NaN;
              yAxis.Minimum = min_y ?? Double.NaN;
              series_array[y] = series;
              axis_array[y] = yAxis;
            }
          );
          bw.RunWorkerAsync(x);
          bw_array.Add(bw);
        }

        // Wait for all background processes to finish then add all series and y axises to plot model
        bool all_done = false;
        while (!all_done)
        {
          all_done = true;
          for (int y = 0; y < bw_array.Count; y++)
          {
            if (bw_array[y].IsBusy)
              all_done = false;
          }
        }
        for (int y = 0; y < series_array.Length; y++)
        {
          temp_PreviewSignalPlot.Series.Add(series_array[y]);
          temp_PreviewSignalPlot.Axes.Add(axis_array[y]);
        }
      }

      PreviewSignalPlot = temp_PreviewSignalPlot;
    }
    /// <summary>
    /// Draws a chart in the Preview tab 
    /// </summary>
    public void DrawChart()
    {
      PreviewNavigationEnabled = false;

      BackgroundWorker bw = new BackgroundWorker();
      bw.DoWork += BW_CreateChart;
      bw.RunWorkerAsync();
    }

    // Export Previewed/Selected Signals Wizard
    public async void ExportSignals()
    {
      if (pm.PreviewSelectedSignals.Count > 0)
      {
        Dialog_Export_Previewed_Signals dlg = new Dialog_Export_Previewed_Signals(pm.PreviewSelectedSignals);

        controller = await p_window.ShowProgressAsync("Export", "Exporting preview signals to binary...");

        controller.SetCancelable(false);

        if (dlg.ShowDialog() == true)
        {
          FolderBrowserDialog folder_dialog = new FolderBrowserDialog();

          string location;

          if (folder_dialog.ShowDialog() == DialogResult.OK)
          {
            location = folder_dialog.SelectedPath;
          }
          else
          {
            await p_window.ShowMessageAsync("Cancelled", "Action was cancelled.");

            await controller.CloseAsync();

            return;
          }

          ExportSignalModel signals_data = Dialog_Export_Previewed_Signals.signals_to_export;

          BackgroundWorker bw = new BackgroundWorker();
          bw.DoWork += BW_ExportSignals;
          bw.RunWorkerCompleted += BW_FinishExportSignals;

          List<dynamic> arguments = new List<dynamic>();
          arguments.Add(signals_data);
          arguments.Add(location);

          bw.RunWorkerAsync(arguments);
        }
      }
      else
      {
        await controller.CloseAsync();

        await p_window.ShowMessageAsync("Error", "Please select at least one signal from the preview.");
      }
    }
    private async void BW_FinishExportSignals(object sender, RunWorkerCompletedEventArgs e)
    {
      await controller.CloseAsync();

      await p_window.ShowMessageAsync("Export Success", "Previewed signals were exported to Binary");
    }
    private void BW_ExportSignals(object sender, DoWorkEventArgs e)
    {
      ExportSignalModel signals_data = ((List<dynamic>)e.Argument)[0];
      string location = ((List<dynamic>)e.Argument)[1];

      foreach (var signal in pm.PreviewSelectedSignals)
      {
        #region signal_header
        EDFSignal edfsignal = LoadedEDFFile.Header.Signals.Find(temp => temp.Label.Trim() == signal);

        float sample_period = LoadedEDFFile.Header.DurationOfDataRecordInSeconds / (float)edfsignal.NumberOfSamplesPerDataRecord;

        //hdr file contains metadata of the binary file
        FileStream hdr_file = new FileStream(location + "/" + signals_data.Subject_ID + "-" + signal + ".hdr", FileMode.OpenOrCreate);
        hdr_file.SetLength(0); //clear it's contents
        hdr_file.Close(); //flush
        hdr_file = new FileStream(location + "/" + signals_data.Subject_ID + "-" + signal + ".hdr", FileMode.OpenOrCreate); //reload

        StringBuilder sb_hdr = new StringBuilder(); // string builder used for writing into the file

        sb_hdr.AppendLine(edfsignal.Label) // name
            .AppendLine(signals_data.Subject_ID.ToString()) // subject id
            .AppendLine(EpochtoDateTime(signals_data.Epochs_From, LoadedEDFFile).ToString()) // epoch start
            .AppendLine(EpochtoDateTime(signals_data.Epochs_To, LoadedEDFFile).ToString()) // epoch end
            .AppendLine(sample_period.ToString()); // sample_period 

        var bytes_to_write = Encoding.ASCII.GetBytes(sb_hdr.ToString());
        hdr_file.Write(bytes_to_write, 0, bytes_to_write.Length);
        hdr_file.Close();

        var edfSignal = LoadedEDFFile.Header.Signals.Find(s => s.Label.Trim() == signal.Trim());
        var signalValues = LoadedEDFFile.retrieveSignalSampleValues(edfSignal).ToArray();

        FileStream bin_file = new FileStream(location + "/" + signals_data.Subject_ID + "-" + signal + ".bin", FileMode.OpenOrCreate); //the binary file for each signal
        bin_file.SetLength(0); //clear it's contents
        bin_file.Close(); //flush

        #endregion

        #region signal_binary_contents

        bin_file = new FileStream(location + "/" + signals_data.Subject_ID + "-" + signal + ".bin", FileMode.OpenOrCreate); //reload
        BinaryWriter bin_writer = new BinaryWriter(bin_file);

        int start_index = (int)((signals_data.Epochs_From * 30) / LoadedEDFFile.Header.DurationOfDataRecordInSeconds)* edfsignal.NumberOfSamplesPerDataRecord; // from epoch number * 30 seconds per epoch * sample rate = start time
        int end_index = (int)((signals_data.Epochs_To * 30) / LoadedEDFFile.Header.DurationOfDataRecordInSeconds)* edfsignal.NumberOfSamplesPerDataRecord; // to epoch number * 30 seconds per epoch * sample rate = end time

        if (start_index < 0) { start_index = 0; }
        if (end_index > signalValues.Count()) { end_index = signalValues.Count(); }        

        for (int i = start_index; i < end_index; i++)
        {
          bin_writer.Write(signalValues[i]);
        }

        bin_writer.Close();

        #endregion
      }
    }

    /// <summary>
    /// Respiratory Analysis From Binary FIle
    /// </summary>
    public void PerformRespiratoryAnalysisBinary()
    {
      System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();

      dialog.Filter = "|*.bin";

      if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        // select the binary file
        FileStream bin_file = new FileStream(dialog.FileName, FileMode.Open);
        BinaryReader reader = new BinaryReader(bin_file);

        byte[] value =  new byte[4];
        bool didReachEnd = false;
        List<float> signal_values = new List<float>();
        // read the whole binary file and build the signal values
        while (reader.BaseStream.Position != reader.BaseStream.Length)
        {
          try
          {
            value = reader.ReadBytes(4);
            float myFloat = System.BitConverter.ToSingle(value, 0);
            signal_values.Add(myFloat);
          }
          catch (Exception ex)
          {
            didReachEnd = true;
            break;
          }
        }

        // close the binary file
        bin_file.Close();

        // get the file metadata from the header file
        bin_file = new FileStream(dialog.FileName.Remove(dialog.FileName.Length - 4, 4) + ".hdr", FileMode.Open);

        StreamReader file_reader = new StreamReader(bin_file);
        // get the signal name
        string signal_name = file_reader.ReadLine();
        string subject_id = file_reader.ReadLine();
        string date_time_from = file_reader.ReadLine();
        string date_time_to = file_reader.ReadLine();
        string sample_period_s = file_reader.ReadLine();

        float sample_period = float.Parse(sample_period_s);

        double? min_y;
        double? max_y;

        DateTime epochs_from_datetime = DateTime.Parse(date_time_from);
        DateTime epochs_to_datetime = DateTime.Parse(date_time_to);

        // perform all of the respiratory analysis
        RespiratoryAnalysisBinary(signal_name, signal_values, out min_y, out max_y, epochs_from_datetime, epochs_to_datetime, sample_period);
      }
      else
      {
        p_window.ShowMessageAsync("Error", "File could not be opened.");
      }
    }
    private void RespiratoryAnalysisBinary(string Signal, List<float> values, out double? min_y, out double? max_y, DateTime epochs_from, DateTime epochs_to, float sample_period)
    {
      // Variable To Return
      LineSeries series = new LineSeries();

      // Determine Y Axis Bounds
      min_y = GetMinSignalValue(Signal, values);
      max_y = GetMaxSignalValue(Signal, values);

      //  // Add Points to Series
      for (int y = 0; y < values.Count; y++)
      {
        series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(epochs_from + new TimeSpan(0, 0, 0, 0, (int)(sample_period * (float)y * 1000))), values[y]));
      }

      PlotModel temp_SignalPlot = new PlotModel();

      temp_SignalPlot.Series.Clear();
      temp_SignalPlot.Axes.Clear();

      // Calculate Bias
      double bias = 0;
      for (int x = 0; x < series.Points.Count; x++)
      {
        double point_1 = series.Points[x].Y;
        double point_2 = x + 1 < series.Points.Count ? series.Points[x + 1].Y : series.Points[x].Y;
        double average = (point_1 + point_2) / 2;
        bias += average / (double)series.Points.Count;
      }

      // Weighted Running Average (Smoothing) and Normalization
      LineSeries series_norm = new LineSeries();
      int LENGTH = (int)(0.05 / sample_period) * 2;
      LENGTH = Math.Max(1, LENGTH);
      for (int x = 0; x < series.Points.Count; x++)
      {
        double sum = 0;
        double weight_sum = 0;
        for (int y = -LENGTH / 2; y <= LENGTH / 2; y++)
        {
          double weight = (LENGTH / 2 + 1) - Math.Abs(y);
          weight_sum += weight;
          sum += weight * series.Points[Math.Min(series.Points.Count - 1, Math.Max(0, x - y))].Y;
        }
        double average = sum / weight_sum;

        series_norm.Points.Add(new DataPoint(series.Points[x].X, average - bias));
      }

      // Find Peaks and Zero Crossings
      int min_spike_length = (int)((double)((double)RespiratoryMinimumPeakWidth / (double)1000) / (double)sample_period);
      int spike_length = 0;
      int maxima = 0;
      int start = 0;
      bool? positive = null;
      ScatterSeries series_pos_peaks = new ScatterSeries();
      ScatterSeries series_neg_peaks = new ScatterSeries();
      ScatterSeries series_insets = new ScatterSeries();
      ScatterSeries series_onsets = new ScatterSeries();
      for (int x = 0; x < series_norm.Points.Count; x++)
      {
        // If positive spike
        if (positive != false)
        {
          // If end of positive spike
          if (series_norm.Points[x].Y < 0 || x == series_norm.Points.Count - 1)
          {
            // If spike is appropriate length
            if (spike_length > min_spike_length)
            {
              if (
                  // If user does not mind consequent peaks of same sign
                  !RespiratoryRemoveMultiplePeaks ||
                  // If first positive peak
                  series_pos_peaks.Points.Count == 0 ||
                  // If last peak was negative
                  (series_neg_peaks.Points.Count != 0 &&
                  DateTimeAxis.ToDateTime(series_neg_peaks.Points[series_neg_peaks.Points.Count - 1].X) >
                  DateTimeAxis.ToDateTime(series_pos_peaks.Points[series_pos_peaks.Points.Count - 1].X))
                 )
              {
                // Add new positive peak and onset 
                series_pos_peaks.Points.Add(new ScatterPoint(series_norm.Points[maxima].X, series_norm.Points[maxima].Y));
                series_onsets.Points.Add(new ScatterPoint(series_norm.Points[start].X, series_norm.Points[start].Y));
              }
              else
              {
                // If this peak is greater than the previous
                if (series_norm.Points[maxima].Y > series_pos_peaks.Points[series_pos_peaks.Points.Count - 1].Y)
                {
                  // Replace previous spike maxima with latest spike maxima
                  series_pos_peaks.Points.Remove(series_pos_peaks.Points[series_pos_peaks.Points.Count - 1]);
                  series_onsets.Points.Remove(series_onsets.Points[series_onsets.Points.Count - 1]);
                  series_pos_peaks.Points.Add(new ScatterPoint(series_norm.Points[maxima].X, series_norm.Points[maxima].Y));
                  series_onsets.Points.Add(new ScatterPoint(series_norm.Points[start].X, series_norm.Points[start].Y));
                }
              }
            }

            // Initialization for analyzing negative peak
            positive = false;
            spike_length = 1;
            maxima = x;
            start = x;
          }
          // If middle of positive spike
          else
          {
            if (Math.Abs(series_norm.Points[x].Y) > Math.Abs(series_norm.Points[maxima].Y))
              maxima = x;
            spike_length++;
          }
        }
        // If negative spike
        else
        {
          // If end of negative spike
          if (series_norm.Points[x].Y > 0 || x == series_norm.Points.Count - 1)
          {
            // If spike is appropriate length
            if (spike_length > min_spike_length)
            {
              if (
                  // If user does not mind consequent peaks of same sign
                  !RespiratoryRemoveMultiplePeaks ||
                  // If first negative peak
                  series_neg_peaks.Points.Count == 0 ||
                  // If last peak was positive 
                  (series_pos_peaks.Points.Count != 0 &&
                  DateTimeAxis.ToDateTime(series_neg_peaks.Points[series_neg_peaks.Points.Count - 1].X) <
                  DateTimeAxis.ToDateTime(series_pos_peaks.Points[series_pos_peaks.Points.Count - 1].X))
                )
              {
                // Add new negative peak and onset 
                series_neg_peaks.Points.Add(new ScatterPoint(series_norm.Points[maxima].X, series_norm.Points[maxima].Y));
                series_insets.Points.Add(new ScatterPoint(series_norm.Points[start].X, series_norm.Points[start].Y));
              }
              else
              {
                // If this peak is less than the previous
                if (series_norm.Points[maxima].Y < series_neg_peaks.Points[series_neg_peaks.Points.Count - 1].Y)
                {
                  // Replace previous spike maxima with latest spike maxima
                  series_neg_peaks.Points.Remove(series_neg_peaks.Points[series_neg_peaks.Points.Count - 1]);
                  series_insets.Points.Remove(series_insets.Points[series_insets.Points.Count - 1]);
                  series_neg_peaks.Points.Add(new ScatterPoint(series_norm.Points[maxima].X, series_norm.Points[maxima].Y));
                  series_insets.Points.Add(new ScatterPoint(series_norm.Points[start].X, series_norm.Points[start].Y));
                }
              }
            }

            // Initialization for analyzing positive peak
            positive = true;
            spike_length = 1;
            maxima = x;
            start = x;
          }
          // If middle of negative spike
          else
          {
            if (Math.Abs(series_norm.Points[x].Y) > Math.Abs(series_norm.Points[maxima].Y))
              maxima = x;
            spike_length++;
          }
        }
      }

      series_norm.YAxisKey = Signal;
      series_norm.XAxisKey = "DateTime";
      series_onsets.YAxisKey = Signal;
      series_onsets.XAxisKey = "DateTime";
      series_insets.YAxisKey = Signal;
      series_insets.XAxisKey = "DateTime";
      series_pos_peaks.YAxisKey = Signal;
      series_pos_peaks.XAxisKey = "DateTime";
      series_neg_peaks.YAxisKey = Signal;
      series_neg_peaks.XAxisKey = "DateTime";

      DateTimeAxis xAxis = new DateTimeAxis();
      xAxis.Key = "DateTime";
      xAxis.Minimum = DateTimeAxis.ToDouble(epochs_from);
      xAxis.Maximum = DateTimeAxis.ToDouble(epochs_to);
      temp_SignalPlot.Axes.Add(xAxis);

      LinearAxis yAxis = new LinearAxis();
      yAxis.MajorGridlineStyle = LineStyle.Solid;
      yAxis.MinorGridlineStyle = LineStyle.Dot;
      yAxis.Title = Signal;
      yAxis.Key = Signal;
      yAxis.Maximum = (max_y ?? Double.NaN) - bias;
      yAxis.Minimum = (min_y ?? Double.NaN) - bias;

      series_onsets.MarkerFill = OxyColor.FromRgb(255, 0, 0);
      series_insets.MarkerFill = OxyColor.FromRgb(0, 255, 0);
      series_pos_peaks.MarkerFill = OxyColor.FromRgb(0, 0, 255);
      series_neg_peaks.MarkerFill = OxyColor.FromRgb(255, 255, 0);

      temp_SignalPlot.Axes.Add(yAxis);
      temp_SignalPlot.Series.Add(series_norm);
      temp_SignalPlot.Series.Add(series_onsets);
      temp_SignalPlot.Series.Add(series_insets);
      temp_SignalPlot.Series.Add(series_pos_peaks);
      temp_SignalPlot.Series.Add(series_neg_peaks);

      RespiratorySignalPlot = temp_SignalPlot;

      // Find Breathing Rate
      List<double> breathing_periods = new List<double>();
      for (int x = 1; x < series_insets.Points.Count; x++)
        breathing_periods.Add((DateTimeAxis.ToDateTime(series_insets.Points[x].X) - DateTimeAxis.ToDateTime(series_insets.Points[x - 1].X)).TotalSeconds);
      for (int x = 1; x < series_onsets.Points.Count; x++)
        breathing_periods.Add((DateTimeAxis.ToDateTime(series_onsets.Points[x].X) - DateTimeAxis.ToDateTime(series_onsets.Points[x - 1].X)).TotalSeconds);
      for (int x = 1; x < series_pos_peaks.Points.Count; x++)
        breathing_periods.Add((DateTimeAxis.ToDateTime(series_pos_peaks.Points[x].X) - DateTimeAxis.ToDateTime(series_pos_peaks.Points[x - 1].X)).TotalSeconds);
      for (int x = 1; x < series_neg_peaks.Points.Count; x++)
        breathing_periods.Add((DateTimeAxis.ToDateTime(series_neg_peaks.Points[x].X) - DateTimeAxis.ToDateTime(series_neg_peaks.Points[x - 1].X)).TotalSeconds);

      breathing_periods.Sort();

      if (breathing_periods.Count > 0)
      {
        RespiratoryBreathingPeriodMean = (breathing_periods.Average()).ToString("0.## sec/breath");
        RespiratoryBreathingPeriodMedian = (breathing_periods[breathing_periods.Count / 2 - 1]).ToString("0.## sec/breath");
      }
    }

    /************************************************** RESPIRATORY ANALYSIS TAB ****************************************************/

    // Respiratory Analysis From EDF File
    /// <summary>
    /// Background process for performing respiratory analysis
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BW_RespiratoryAnalysisEDF(object sender, DoWorkEventArgs e)
    {
      PlotModel temp_SignalPlot = new PlotModel();

      temp_SignalPlot.Series.Clear();
      temp_SignalPlot.Axes.Clear();

      double? max_y, min_y;
      float sample_period;
      LineSeries series = GetSeriesFromSignalName(out sample_period,
                                                  out max_y,
                                                  out min_y,
                                                  RespiratoryEDFSelectedSignal,
                                                  EpochtoDateTime(RespiratoryEDFStartRecord ?? 0, LoadedEDFFile),
                                                  EpochtoDateTime(RespiratoryEDFStartRecord ?? 0, LoadedEDFFile) + EpochPeriodtoTimeSpan(RespiratoryEDFDuration ?? 0)
                                                  );

      // Plot Insets of Respiration Expiration

      // Calculate Bias
      double bias = 0;
      for (int x = 0; x < series.Points.Count; x++)
      {
        double point_1 = series.Points[x].Y;
        double point_2 = x + 1 < series.Points.Count ? series.Points[x + 1].Y : series.Points[x].Y;
        double average = (point_1 + point_2) / 2;
        bias += average / (double)series.Points.Count;
      }

      // Weighted Running Average (Smoothing) and Normalization
      LineSeries series_norm = new LineSeries();
      int LENGTH = (int)(0.05 / sample_period) * 2;
      LENGTH = Math.Max(1, LENGTH);
      for (int x = 0; x < series.Points.Count; x++)
      {
        double sum = 0;
        double weight_sum = 0;
        for (int y = -LENGTH / 2; y <= LENGTH / 2; y++)
        {
          double weight = (LENGTH / 2 + 1) - Math.Abs(y);
          weight_sum += weight;
          sum += weight * series.Points[Math.Min(series.Points.Count - 1, Math.Max(0, x - y))].Y;
        }
        double average = sum / weight_sum;

        series_norm.Points.Add(new DataPoint(series.Points[x].X, average - bias));
      }

      // Find Peaks and Zero Crossings
      int min_spike_length = (int)((double)((double)RespiratoryMinimumPeakWidth / (double)1000) / (double)sample_period);
      int spike_length = 0;
      int maxima = 0;
      int start = 0;
      bool? positive = null;
      ScatterSeries series_pos_peaks = new ScatterSeries();
      ScatterSeries series_neg_peaks = new ScatterSeries();
      ScatterSeries series_insets = new ScatterSeries();
      ScatterSeries series_onsets = new ScatterSeries();
      for (int x = 0; x < series_norm.Points.Count; x++)
      {
        // If positive spike
        if (positive != false) 
        {
          // If end of positive spike
          if (series_norm.Points[x].Y < 0 || x == series_norm.Points.Count - 1) 
          {
            // If spike is appropriate length
            if (spike_length > min_spike_length) 
            {
              if (
                  // If user does not mind consequent peaks of same sign
                  !RespiratoryRemoveMultiplePeaks || 
                  // If first positive peak
                  series_pos_peaks.Points.Count == 0 ||
                  // If last peak was negative
                  (series_neg_peaks.Points.Count != 0 && 
                  DateTimeAxis.ToDateTime(series_neg_peaks.Points[series_neg_peaks.Points.Count - 1].X) > 
                  DateTimeAxis.ToDateTime(series_pos_peaks.Points[series_pos_peaks.Points.Count - 1].X))
                 ) 
              {
                // Add new positive peak and onset 
                series_pos_peaks.Points.Add(new ScatterPoint(series_norm.Points[maxima].X, series_norm.Points[maxima].Y));
                series_onsets.Points.Add(new ScatterPoint(series_norm.Points[start].X, series_norm.Points[start].Y));
              }
              else
              {
                // If this peak is greater than the previous
                if (series_norm.Points[maxima].Y > series_pos_peaks.Points[series_pos_peaks.Points.Count - 1].Y) 
                {
                  // Replace previous spike maxima with latest spike maxima
                  series_pos_peaks.Points.Remove(series_pos_peaks.Points[series_pos_peaks.Points.Count - 1]);
                  series_onsets.Points.Remove(series_onsets.Points[series_onsets.Points.Count - 1]);
                  series_pos_peaks.Points.Add(new ScatterPoint(series_norm.Points[maxima].X, series_norm.Points[maxima].Y));
                  series_onsets.Points.Add(new ScatterPoint(series_norm.Points[start].X, series_norm.Points[start].Y));
                }
              }
            }

            // Initialization for analyzing negative peak
            positive = false;
            spike_length = 1;
            maxima = x;
            start = x;
          }
          // If middle of positive spike
          else
          {
            if (Math.Abs(series_norm.Points[x].Y) > Math.Abs(series_norm.Points[maxima].Y))
              maxima = x;
            spike_length++;
          }
        }
        // If negative spike
        else
        {
          // If end of negative spike
          if (series_norm.Points[x].Y > 0 || x == series_norm.Points.Count - 1)
          {
            // If spike is appropriate length
            if (spike_length > min_spike_length)
            {
              if (
                  // If user does not mind consequent peaks of same sign
                  !RespiratoryRemoveMultiplePeaks ||
                  // If first negative peak
                  series_neg_peaks.Points.Count == 0 ||
                  // If last peak was positive 
                  (series_pos_peaks.Points.Count != 0 && 
                  DateTimeAxis.ToDateTime(series_neg_peaks.Points[series_neg_peaks.Points.Count - 1].X) < 
                  DateTimeAxis.ToDateTime(series_pos_peaks.Points[series_pos_peaks.Points.Count - 1].X))
                ) 
              {
                // Add new negative peak and onset 
                series_neg_peaks.Points.Add(new ScatterPoint(series_norm.Points[maxima].X, series_norm.Points[maxima].Y));
                series_insets.Points.Add(new ScatterPoint(series_norm.Points[start].X, series_norm.Points[start].Y));
              }
              else
              {
                // If this peak is less than the previous
                if (series_norm.Points[maxima].Y < series_neg_peaks.Points[series_neg_peaks.Points.Count - 1].Y) 
                {
                  // Replace previous spike maxima with latest spike maxima
                  series_neg_peaks.Points.Remove(series_neg_peaks.Points[series_neg_peaks.Points.Count - 1]);
                  series_insets.Points.Remove(series_insets.Points[series_insets.Points.Count - 1]);
                  series_neg_peaks.Points.Add(new ScatterPoint(series_norm.Points[maxima].X, series_norm.Points[maxima].Y));
                  series_insets.Points.Add(new ScatterPoint(series_norm.Points[start].X, series_norm.Points[start].Y));
                }
              }
            }

            // Initialization for analyzing positive peak
            positive = true;
            spike_length = 1;
            maxima = x;
            start = x;
          }
          // If middle of negative spike
          else
          {
            if (Math.Abs(series_norm.Points[x].Y) > Math.Abs(series_norm.Points[maxima].Y))
              maxima = x;
            spike_length++;
          }
        }
      }

      series_norm.YAxisKey = RespiratoryEDFSelectedSignal;
      series_norm.XAxisKey = "DateTime";
      series_onsets.YAxisKey = RespiratoryEDFSelectedSignal;
      series_onsets.XAxisKey = "DateTime";
      series_insets.YAxisKey = RespiratoryEDFSelectedSignal;
      series_insets.XAxisKey = "DateTime";
      series_pos_peaks.YAxisKey = RespiratoryEDFSelectedSignal;
      series_pos_peaks.XAxisKey = "DateTime";
      series_neg_peaks.YAxisKey = RespiratoryEDFSelectedSignal;
      series_neg_peaks.XAxisKey = "DateTime";

      DateTimeAxis xAxis = new DateTimeAxis();
      xAxis.Key = "DateTime";
      xAxis.Minimum = DateTimeAxis.ToDouble(EpochtoDateTime(RespiratoryEDFStartRecord ?? 0, LoadedEDFFile));
      xAxis.Maximum = DateTimeAxis.ToDouble(EpochtoDateTime(RespiratoryEDFStartRecord ?? 0, LoadedEDFFile) + EpochPeriodtoTimeSpan(RespiratoryEDFDuration ?? 0));
      temp_SignalPlot.Axes.Add(xAxis);

      LinearAxis yAxis = new LinearAxis();
      yAxis.MajorGridlineStyle = LineStyle.Solid;
      yAxis.MinorGridlineStyle = LineStyle.Dot;
      yAxis.Title = RespiratoryEDFSelectedSignal;
      yAxis.Key = RespiratoryEDFSelectedSignal;
      yAxis.Maximum = (max_y ?? Double.NaN) - bias;
      yAxis.Minimum = (min_y ?? Double.NaN) - bias;

      series_onsets.MarkerFill = OxyColor.FromRgb(255, 0, 0);
      series_insets.MarkerFill = OxyColor.FromRgb(0, 255, 0);
      series_pos_peaks.MarkerFill = OxyColor.FromRgb(0, 0, 255);
      series_neg_peaks.MarkerFill = OxyColor.FromRgb(255, 255, 0);

      temp_SignalPlot.Axes.Add(yAxis);
      temp_SignalPlot.Series.Add(series_norm);
      temp_SignalPlot.Series.Add(series_onsets);
      temp_SignalPlot.Series.Add(series_insets);
      temp_SignalPlot.Series.Add(series_pos_peaks);
      temp_SignalPlot.Series.Add(series_neg_peaks);

      RespiratorySignalPlot = temp_SignalPlot;

      // Find Breathing Rate
      List<double> breathing_periods = new List<double>();
      for (int x = 1; x < series_insets.Points.Count; x++)
        breathing_periods.Add((DateTimeAxis.ToDateTime(series_insets.Points[x].X) - DateTimeAxis.ToDateTime(series_insets.Points[x - 1].X)).TotalSeconds);
      for (int x = 1; x < series_onsets.Points.Count; x++)
        breathing_periods.Add((DateTimeAxis.ToDateTime(series_onsets.Points[x].X) - DateTimeAxis.ToDateTime(series_onsets.Points[x - 1].X)).TotalSeconds);
      for (int x = 1; x < series_pos_peaks.Points.Count; x++)
        breathing_periods.Add((DateTimeAxis.ToDateTime(series_pos_peaks.Points[x].X) - DateTimeAxis.ToDateTime(series_pos_peaks.Points[x - 1].X)).TotalSeconds);
      for (int x = 1; x < series_neg_peaks.Points.Count; x++)
        breathing_periods.Add((DateTimeAxis.ToDateTime(series_neg_peaks.Points[x].X) - DateTimeAxis.ToDateTime(series_neg_peaks.Points[x - 1].X)).TotalSeconds);

      breathing_periods.Sort();

      if (breathing_periods.Count > 0)
      {
        RespiratoryBreathingPeriodMean = (breathing_periods.Average()).ToString("0.## sec/breath");
        RespiratoryBreathingPeriodMedian = (breathing_periods[breathing_periods.Count / 2 - 1]).ToString("0.## sec/breath");
      }

    }
    /// <summary>
    /// Peforms respiratory analysis 
    /// </summary>
    public void PerformRespiratoryAnalysisEDF()
    {
      BackgroundWorker bw = new BackgroundWorker();
      bw.DoWork += BW_RespiratoryAnalysisEDF;
      bw.RunWorkerAsync();
    }

    /****************************************************** EEG ANALYSIS TAB ********************************************************/

    //EEG Analysis From Binary File
    public void PerformEEGAnalysisBinary()
    {
      System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();

      dialog.Filter = "|*.bin";

      if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        // select the binary file
        FileStream bin_file = new FileStream(dialog.FileName, FileMode.Open);
        BinaryReader reader = new BinaryReader(bin_file);

        byte[] value = new byte[4];
        bool didReachEnd = false;
        List<float> signal_values = new List<float>();
        // read the whole binary file and build the signal values
        while (reader.BaseStream.Position != reader.BaseStream.Length)
        {
          try
          {
            value = reader.ReadBytes(4);
            float myFloat = System.BitConverter.ToSingle(value, 0);
            signal_values.Add(myFloat);
          }
          catch (Exception ex)
          {
            didReachEnd = true;
            break;
          }
        }

        // close the binary file
        bin_file.Close();

        // get the file metadata from the header file
        bin_file = new FileStream(dialog.FileName.Remove(dialog.FileName.Length - 4, 4) + ".hdr", FileMode.Open);

        StreamReader file_reader = new StreamReader(bin_file);
        // get the signal name
        string signal_name = file_reader.ReadLine();
        string subject_id = file_reader.ReadLine();
        string date_time_from = file_reader.ReadLine();
        string date_time_to = file_reader.ReadLine();
        string sample_period_s = file_reader.ReadLine();

        float sample_period = float.Parse(sample_period_s);

        double? min_y;
        double? max_y;

        DateTime epochs_from_datetime = DateTime.Parse(date_time_from);
        DateTime epochs_to_datetime = DateTime.Parse(date_time_to);

        // perform all of the respiratory analysis
        BW_EEGAnalysisBin(signal_name, signal_values, out min_y, out max_y, epochs_from_datetime, epochs_to_datetime, sample_period);
      }
      else
      {
        p_window.ShowMessageAsync("Error", "File could not be opened.");
      }
    }

    private void BW_EEGAnalysisBin(string Signal, List<float> values, out double? min_y, out double? max_y, DateTime epochs_from, DateTime epochs_to, float sample_period)
    {
      // Variable To Return
      LineSeries series = new LineSeries();

      // Determine Y Axis Bounds
      min_y = GetMinSignalValue(Signal, values);
      max_y = GetMaxSignalValue(Signal, values);

      //  // Add Points to Series
      for (int y = 0; y < values.Count; y++)
      {
        series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(epochs_from + new TimeSpan(0, 0, 0, 0, (int)(sample_period * (float)y * 1000))), values[y]));
      }

      if (series.Points.Count == 0)//select length to be more than From (on GUI)
      {
        //need to type error message for User
        return;
      }

      const int freqbands = 7;
      MWNumericArray[] freqRange = new MWNumericArray[freqbands];
      freqRange[0] = new MWNumericArray(1, 2, new double[] { 0.1, 3 });//delta band
      freqRange[1] = new MWNumericArray(1, 2, new double[] { 4, 7 });//theta band
      freqRange[2] = new MWNumericArray(1, 2, new double[] { 8, 12 });//alpha band
      freqRange[3] = new MWNumericArray(1, 2, new double[] { 13, 17 });//beta1 band
      freqRange[4] = new MWNumericArray(1, 2, new double[] { 18, 30 });//beta2 band
      freqRange[5] = new MWNumericArray(1, 2, new double[] { 31, 40 });//gamma1 band
      freqRange[6] = new MWNumericArray(1, 2, new double[] { 41, 50 });//gamma2 band

      double[] signal = new double[series.Points.Count];//select length to be more than From (on GUI)
      for (int i = 0; i < series.Points.Count; i++)
      {
        signal[i] = series.Points[i].Y;
      }

      MWNumericArray mlabArraySignal = new MWNumericArray(signal);
      EEGPower pwr = new EEGPower();
      double totalPower = 0.0;
      MWNumericArray[] absPower = new MWNumericArray[freqbands];
      MWNumericArray sampleFreq = new MWNumericArray(1 / sample_period);
      ColumnItem[] absPlotbandItems = new ColumnItem[freqbands];
      for (int i = 0; i < freqRange.Length; i++)
      {
        absPower[i] = (MWNumericArray)pwr.eeg_bandpower(mlabArraySignal, sampleFreq, freqRange[i]);
        totalPower += (double)absPower[i];
        absPlotbandItems[i] = new ColumnItem { Value = (double)absPower[i] };//bars for abs pwr plot
      }

      ColumnItem[] relPlotbandItems = new ColumnItem[freqbands];
      double[] relPower = new double[freqbands];
      for (int i = 0; i < relPower.Length; i++)
      {
        relPower[i] = ((double)absPower[i]) / totalPower;
        relPlotbandItems[i] = new ColumnItem { Value = relPower[i] };//bars for rel pwr plot
      }

      //order of bands MUST match the order of bands in freqRange array (see above)
      String[] freqBandName = new String[] { "delta", "theta", "alpha", "beta1", "beta2", "gamma1", "gamma2" };


      //Plotting absolute power graph      
      PlotModel tempAbsPwr = new PlotModel()
      {
        Title = "Absolute Power",
        LegendPlacement = LegendPlacement.Outside,
        LegendPosition = LegendPosition.BottomCenter,
        LegendOrientation = LegendOrientation.Horizontal,
        LegendBorderThickness = 0
      };

      ColumnSeries absPlotbars = new ColumnSeries
      {
        //Title = "Abs_Pwr",
        StrokeColor = OxyColors.Black,
        StrokeThickness = 1,
        FillColor = OxyColors.Blue//changes color of bars
      };
      absPlotbars.Items.AddRange(absPlotbandItems);

      CategoryAxis absbandLabels = new CategoryAxis { Position = AxisPosition.Bottom };

      absbandLabels.Labels.AddRange(freqBandName);

      LinearAxis absvoltYAxis = new LinearAxis { Position = AxisPosition.Left, MinimumPadding = 0, MaximumPadding = 0.06, AbsoluteMinimum = 0 };
      tempAbsPwr.Series.Add(absPlotbars);
      tempAbsPwr.Axes.Add(absbandLabels);
      tempAbsPwr.Axes.Add(absvoltYAxis);

      PlotAbsPwr = tempAbsPwr;
      /**********************End of Absolute Power Plotting***********************/

      //Plotting relative power graph      
      PlotModel tempRelPwr = new PlotModel()
      {
        Title = "Relative Power",
        LegendPlacement = LegendPlacement.Outside,
        LegendPosition = LegendPosition.BottomCenter,
        LegendOrientation = LegendOrientation.Horizontal,
        LegendBorderThickness = 0
      };
      ColumnSeries relPlotbars = new ColumnSeries
      {
        //Title = "Rel_Pwr",
        StrokeColor = OxyColors.Black,
        StrokeThickness = 1,
        FillColor = OxyColors.Red//changes color of bars
      };
      relPlotbars.Items.AddRange(relPlotbandItems);

      CategoryAxis relbandLabels = new CategoryAxis { Position = AxisPosition.Bottom };

      relbandLabels.Labels.AddRange(freqBandName);

      LinearAxis relvoltYAxis = new LinearAxis { Position = AxisPosition.Left, MinimumPadding = 0, MaximumPadding = 0.06, AbsoluteMinimum = 0 };
      tempRelPwr.Series.Add(relPlotbars);
      tempRelPwr.Axes.Add(relbandLabels);
      tempRelPwr.Axes.Add(relvoltYAxis);

      PlotRelPwr = tempRelPwr;

      //todo: create a heatmap (from oxyplot) for spectrogram
      //todo: create a graph (from oxyplot) for power spectral density
      return;//for debugging only
    }

    //EEG Analysis From EDF File
    private void BW_EEGAnalysisEDF(object sender, DoWorkEventArgs e)
    {      
      double? max_y, min_y;
      float sample_period;
      LineSeries series = GetSeriesFromSignalName(out sample_period,
                                                  out max_y,
                                                  out min_y,
                                                  EEGEDFSelectedSignal,
                                                  EpochtoDateTime(EEGEDFStartRecord ?? 0, LoadedEDFFile),
                                                  EpochtoDateTime(EEGEDFStartRecord ?? 0, LoadedEDFFile) + EpochPeriodtoTimeSpan(EEGEDFDuration ?? 0)
                                                  );

      if(series.Points.Count == 0)//select length to be more than From (on GUI)
      {
        //need to type error message for User
        return;
      }
      
      const int freqbands = 7;
      MWNumericArray[] freqRange = new MWNumericArray[freqbands];
      freqRange[0] = new MWNumericArray(1, 2, new double[] { 0.1, 3 });//delta band
      freqRange[1] = new MWNumericArray(1, 2, new double[] { 4, 7 });//theta band
      freqRange[2] = new MWNumericArray(1, 2, new double[] { 8, 12 });//alpha band
      freqRange[3] = new MWNumericArray(1, 2, new double[] { 13, 17 });//beta1 band
      freqRange[4] = new MWNumericArray(1, 2, new double[] { 18, 30 });//beta2 band
      freqRange[5] = new MWNumericArray(1, 2, new double[] { 31, 40 });//gamma1 band
      freqRange[6] = new MWNumericArray(1, 2, new double[] { 41, 50 });//gamma2 band

      double[] signal = new double[series.Points.Count];//select length to be more than From (on GUI)
      for (int i = 0; i < series.Points.Count; i++)
      {
        signal[i] = series.Points[i].Y;
      }

      /********************Computing Absolute, Relative & TotalPower*************************/
      MWNumericArray mlabArraySignal = new MWNumericArray(signal);      
      EEGPower pwr = new EEGPower();
      double totalPower = 0.0;
      MWNumericArray[] absPower = new MWNumericArray[freqbands];
      MWNumericArray sampleFreq = new MWNumericArray(1/sample_period);
      ColumnItem[] absPlotbandItems = new ColumnItem[freqbands];
      for (int i = 0; i < freqRange.Length; i++)
      {
        absPower[i] = (MWNumericArray)pwr.eeg_bandpower(mlabArraySignal, sampleFreq, freqRange[i]);
        totalPower += (double)absPower[i];
        absPlotbandItems[i] = new ColumnItem { Value = 10 * Math.Log10((double)absPower[i]) };//bars for abs pwr plot
      }

      ColumnItem[] relPlotbandItems = new ColumnItem[freqbands];
      double[] relPower = new double[freqbands];
      for (int i = 0; i < relPower.Length; i++)
      {
        relPower[i] = 100 * ((double)absPower[i]) / totalPower;
        relPlotbandItems[i] = new ColumnItem { Value = relPower[i] };//bars for rel pwr plot
      }

      /*************Computing Power spectral Density (line 841 - PSG_viewer_v7.m)****************/
      EEG_PSD computePSD = new EEG_PSD();
      MWArray[] mLabResult = null;

      mLabResult = computePSD.eeg_psd(2, mlabArraySignal, sampleFreq);
      MWNumericArray tempPsdValues = (MWNumericArray)mLabResult[0];
      MWNumericArray tempFrqValues = (MWNumericArray)mLabResult[1];

      double[] psdValues = new double[tempPsdValues.NumberOfElements];
      double[] frqValues = new double[tempFrqValues.NumberOfElements];
      for (int i = 0; i < tempPsdValues.NumberOfElements - 1; i++)
      {
        psdValues[i] = (double)tempPsdValues[i + 1];
        frqValues[i] = (double)tempFrqValues[i + 1];
      }

      /*****************************Plotting absolute power graph***************************/
      //order of bands MUST match the order of bands in freqRange array (see above)
      String[] freqBandName = new String[] { "delta", "theta", "alpha", "beta1", "beta2", "gamma1", "gamma2" };


      PlotModel tempAbsPwr = new PlotModel()
      {
        Title = "Absolute Power",
        LegendPlacement = LegendPlacement.Outside,
        LegendPosition = LegendPosition.BottomCenter,
        LegendOrientation = LegendOrientation.Horizontal,
        LegendBorderThickness = 0
      };
      
      ColumnSeries absPlotbars = new ColumnSeries
      {
        //Title = "Abs_Pwr",
        StrokeColor = OxyColors.Black,
        StrokeThickness = 1,
        FillColor = OxyColors.Blue//changes color of bars
      };      
      absPlotbars.Items.AddRange(absPlotbandItems);            

      CategoryAxis absbandLabels = new CategoryAxis { Position = AxisPosition.Bottom };
            
      absbandLabels.Labels.AddRange(freqBandName);      
      
      LinearAxis absYAxis = new LinearAxis { Position = AxisPosition.Left, Title="Power (db)", MinimumPadding = 0, MaximumPadding = 0.06, AbsoluteMinimum = 0};
      tempAbsPwr.Series.Add(absPlotbars);
      tempAbsPwr.Axes.Add(absbandLabels);
      tempAbsPwr.Axes.Add(absYAxis);

      PlotAbsPwr = tempAbsPwr;
      

      /*************************************Plotting relative power graph****************************/
      PlotModel tempRelPwr = new PlotModel()
      {
        Title = "Relative Power",
        LegendPlacement = LegendPlacement.Outside,
        LegendPosition = LegendPosition.BottomCenter,
        LegendOrientation = LegendOrientation.Horizontal,
        LegendBorderThickness = 0
      };
      ColumnSeries relPlotbars = new ColumnSeries
      {
        //Title = "Rel_Pwr",
        StrokeColor = OxyColors.Black,
        StrokeThickness = 1,
        FillColor = OxyColors.Red//changes color of bars
      };      
      relPlotbars.Items.AddRange(relPlotbandItems);
      
      CategoryAxis relbandLabels = new CategoryAxis { Position = AxisPosition.Bottom };
      
      relbandLabels.Labels.AddRange(freqBandName);
      
      LinearAxis relYAxis = new LinearAxis { Position = AxisPosition.Left, Title = "Power (%)", MinimumPadding = 0, MaximumPadding = 0.06, AbsoluteMinimum = 0 };
      tempRelPwr.Series.Add(relPlotbars);
      tempRelPwr.Axes.Add(relbandLabels);
      tempRelPwr.Axes.Add(relYAxis);

      PlotRelPwr = tempRelPwr;

      /********************Plotting a heatmap for spectrogram (line 820 - PSG_viewer_v7.m)*********************/
      /*PlotModel tempSpectGram = new PlotModel()
      {
        Title = "Spectrogram",         
      };
      LinearColorAxis specLegend = new LinearColorAxis() { Position = AxisPosition.Right, Palette = OxyPalettes.Jet(500), HighColor = OxyColors.Red, LowColor = OxyColors.Green };
      LinearAxis specYAxis = new LinearAxis() { Position = AxisPosition.Left, Title = "Frequency (Hz)" };
      LinearAxis specXAxis = new LinearAxis() { Position = AxisPosition.Bottom, Title = "Time (s)" };     
      
      tempSpectGram.Axes.Add(specLegend);
      tempSpectGram.Axes.Add(specXAxis);
      tempSpectGram.Axes.Add(specYAxis);

      //double minTime, maxTime, minFreq, maxFreq;
      //HeatMapSeries specGram = new HeatMapSeries() { //X0 = minTime, X1 = maxTime, Y0 = minFreq, Y1 = maxFreq, Data = psdValues };
      tempSpectGram.Series.Add(specGram);*/

      //PlotSpecGram = tempSpectGram;

      /***************************Plotting Power Spectral Density ****************************/
      PlotModel tempPSD = new PlotModel()
      {
        Title = "Power Spectral Density"       
      };
      LineSeries psdSeries = new LineSeries() { Color = OxyColors.Green};
      for(int i = 0; i < psdValues.Length; i++)
      {
        psdSeries.Points.Add(new DataPoint(frqValues[i], 10 * Math.Log10(psdValues[i])));
      }
      tempPSD.Series.Add(psdSeries);
      tempPSD.Axes.Add(new LinearAxis() { Position = AxisPosition.Left, Title = "Power (dB)" });
      tempPSD.Axes.Add(new LinearAxis() { Position = AxisPosition.Bottom, Title = "Frequency (Hz)" });

      PlotPSD = tempPSD;

      return;//for debugging only
    }
    private void BW_FinishEEGAnalysisEDF(object sender, RunWorkerCompletedEventArgs e)
    {

    }
    public void PerformEEGAnalysisEDF()
    {
      BackgroundWorker bw = new BackgroundWorker();
      bw.DoWork += BW_EEGAnalysisEDF;
      bw.RunWorkerCompleted += BW_FinishEEGAnalysisEDF;
      bw.RunWorkerAsync();
    }

    /**************************************************** COHERENCE ANALYSIS TAB ****************************************************/

    /// <summary>
    /// Background process for performing coherence analysis
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BW_CoherenceAnalysisEDF(object sender, DoWorkEventArgs e)
    {
      #region Plot Series 1 in Time Domain 

      // Get Series 1
      double? max_y_1, min_y_1;
      float sample_period_1;
      LineSeries series_1 = GetSeriesFromSignalName(out sample_period_1,
                                                  out max_y_1,
                                                  out min_y_1,
                                                  CoherenceEDFSelectedSignal1,
                                                  EpochtoDateTime(CoherenceEDFStartRecord ?? 0, LoadedEDFFile),
                                                  EpochtoDateTime(CoherenceEDFStartRecord ?? 0, LoadedEDFFile) + EpochPeriodtoTimeSpan(CoherenceEDFDuration ?? 0)
                                                  );

      // Plot Series 1
      {
        PlotModel temp_SignalPlot = new PlotModel();

        DateTimeAxis xAxis = new DateTimeAxis();
        xAxis.Key = "DateTime";
        xAxis.Minimum = DateTimeAxis.ToDouble(EpochtoDateTime(CoherenceEDFStartRecord ?? 0, LoadedEDFFile));
        xAxis.Maximum = DateTimeAxis.ToDouble(EpochtoDateTime(CoherenceEDFStartRecord ?? 0, LoadedEDFFile) + EpochPeriodtoTimeSpan(CoherenceEDFDuration ?? 0));
        temp_SignalPlot.Axes.Add(xAxis);

        LinearAxis yAxis = new LinearAxis();
        yAxis.MajorGridlineStyle = LineStyle.Solid;
        yAxis.MinorGridlineStyle = LineStyle.Dot;
        yAxis.Title = CoherenceEDFSelectedSignal1 + " (Time)";
        yAxis.Key = CoherenceEDFSelectedSignal1 + " (Time)";
        yAxis.Maximum = (max_y_1 ?? Double.NaN);
        yAxis.Minimum = (min_y_1 ?? Double.NaN);
        temp_SignalPlot.Axes.Add(yAxis);

        series_1.YAxisKey = CoherenceEDFSelectedSignal1 + " (Time)";
        series_1.XAxisKey = "DateTime";
        temp_SignalPlot.Series.Add(series_1);

        CoherenceSignalPlot1 = temp_SignalPlot;
      }

      #endregion

      #region Plot Series 2 in Time Domain 

      // Get Series 2
      double? max_y_2, min_y_2;
      float sample_period_2;
      LineSeries series_2 = GetSeriesFromSignalName(out sample_period_2,
                                                  out max_y_2,
                                                  out min_y_2,
                                                  CoherenceEDFSelectedSignal2,
                                                  EpochtoDateTime(CoherenceEDFStartRecord ?? 0, LoadedEDFFile),
                                                  EpochtoDateTime(CoherenceEDFStartRecord ?? 0, LoadedEDFFile) + EpochPeriodtoTimeSpan(CoherenceEDFDuration ?? 0)
                                                  );

      // Plot Series 2
      {
        PlotModel temp_SignalPlot = new PlotModel();

        DateTimeAxis xAxis = new DateTimeAxis();
        xAxis.Key = "DateTime";
        xAxis.Minimum = DateTimeAxis.ToDouble(EpochtoDateTime(CoherenceEDFStartRecord ?? 0, LoadedEDFFile));
        xAxis.Maximum = DateTimeAxis.ToDouble(EpochtoDateTime(CoherenceEDFStartRecord ?? 0, LoadedEDFFile) + EpochPeriodtoTimeSpan(CoherenceEDFDuration ?? 0));
        temp_SignalPlot.Axes.Add(xAxis);

        LinearAxis yAxis = new LinearAxis();
        yAxis.MajorGridlineStyle = LineStyle.Solid;
        yAxis.MinorGridlineStyle = LineStyle.Dot;
        yAxis.Title = CoherenceEDFSelectedSignal2 + " (Time)";
        yAxis.Key = CoherenceEDFSelectedSignal2 + " (Time)";
        yAxis.Maximum = (max_y_2 ?? Double.NaN);
        yAxis.Minimum = (min_y_2 ?? Double.NaN);
        temp_SignalPlot.Axes.Add(yAxis);

        series_2.YAxisKey = CoherenceEDFSelectedSignal2 + " (Time)";
        series_2.XAxisKey = "DateTime";
        temp_SignalPlot.Series.Add(series_2);

        CoherenceSignalPlot2 = temp_SignalPlot;
      }

      #endregion

      #region Plot Coherence 

      // Calculate Coherence
      LineSeries coh = new LineSeries();
      {
        List<float> values1;
        List<float> values2;

        if (sample_period_1 == sample_period_2)
        {
          values1 = series_1.Points.Select(temp => (float)temp.Y).ToList();
          values2 = series_2.Points.Select(temp => (float)temp.Y).ToList();
        }
        else
        {
          if (sample_period_1 < sample_period_2) // Upsample signal 2
          {
            values1 = series_1.Points.Select(temp => (float)temp.Y).ToList();
            values2 = series_2.Points.Select(temp => (float)temp.Y).ToList();
            values2 = MATLAB_Resample(values2.ToArray(), sample_period_2 / sample_period_1);
          }
          else // Upsample signal 1
          {
            values1 = series_1.Points.Select(temp => (float)temp.Y).ToList();
            values2 = series_2.Points.Select(temp => (float)temp.Y).ToList();
            values1 = MATLAB_Resample(values1.ToArray(), sample_period_1 / sample_period_2);
          }
        }

        coh = MATLAB_Coherence(values1.ToArray(), values2.ToArray());
        coh.YAxisKey = "Coherence";
      }

      // Plot Coherence
      {
        PlotModel temp_plot = new PlotModel();
        temp_plot.Series.Add(coh);

        LinearAxis yAxis = new LinearAxis();
        yAxis.MajorGridlineStyle = LineStyle.Solid;
        yAxis.MinorGridlineStyle = LineStyle.Dot;
        yAxis.Title = "Coherence";
        yAxis.Key = "Coherence";
        yAxis.Maximum = 1.25;
        yAxis.Minimum = 0;
        temp_plot.Axes.Add(yAxis);

        CoherencePlot = temp_plot;
      }

      #endregion
    }
    /// <summary>
    /// Performs coherence analysis between two signals
    /// </summary>
    public void PerformCoherenceAnalysisEDF()
    {
      CoherenceProgressRingEnabled = true; 

      BackgroundWorker bw = new BackgroundWorker();
      bw.DoWork += BW_CoherenceAnalysisEDF;
      bw.RunWorkerAsync();
    }

    /*********************************************************************************************************************************/

    #endregion

    #region Settings

    /*********************************************************************************************************************************/

    // Flyout
    private bool p_FlyoutOpen = false;
    public bool FlyoutOpen
    {
      get
      {
        return p_FlyoutOpen;
      }
      set
      {
        p_FlyoutOpen = value;
        OnPropertyChanged(nameof(FlyoutOpen));
      }
    }
    public void OpenSettings()
    {
      FlyoutOpen = !FlyoutOpen;
    }

    // Recent File List and Functions. 
    public ReadOnlyCollection<string> RecentFiles
    {
      get
      {
        string[] value = null;

        if (File.Exists("recent.txt"))
        {
          StreamReader sr = new StreamReader("recent.txt");
          string[] text = sr.ReadToEnd().Split('\n');
          List<string> values = new List<string>();
          for (int x = 0; x < text.Length; x++)
            if (File.Exists(text[x].Trim()))
              values.Add(text[x].Trim());
          sr.Close();

          value = values.ToArray();
        }
        else
        {
          value = new string[0];
        }

        return Array.AsReadOnly(value);
      }
    }
    public void RecentFiles_Add(string path)
    {
      List<string> array = RecentFiles.ToArray().ToList();
      array.Insert(0, path);
      array = array.Distinct().ToList();

      StreamWriter sw = new StreamWriter("recent.txt");
      for (int x = 0; x < array.Count; x++)
      {
        sw.WriteLine(array[x]);
      }
      sw.Close();

      p_window.LoadRecent();
    }
    public void RecentFiles_Remove(string path)
    {
      List<string> array = RecentFiles.ToArray().ToList();
      array.Remove(path);
      array = array.Distinct().ToList();

      StreamWriter sw = new StreamWriter("recent.txt");
      for (int x = 0; x < array.Count; x++)
      {
        sw.WriteLine(array[x]);
      }
      sw.Close();

      p_window.LoadRecent();
    }

    // Preview Category Management
    private List<string> p_SignalCategories = new List<string>();
    private List<List<string>> p_SignalCategoryContents = new List<List<string>>();
    private void LoadCategoriesFile()
    {
      p_SignalCategories.Clear();
      p_SignalCategoryContents.Clear();

      if (File.Exists("signal_categories.txt"))
      {
        StreamReader sr = new StreamReader("signal_categories.txt");
        string[] text = sr.ReadToEnd().Replace("\r\n", "\n").Split('\n');

        for (int x = 0; x < text.Length; x++)
        {
          string line = text[x];

          string category = line.Split(',')[0].Trim();
          List<string> category_signals = new List<string>();

          for (int y = 0; y < line.Split(',').Length; y++)
          {
            if (EDFAllSignals.Contains(line.Split(',')[y].Trim()) || p_DerivedSignals.Find(temp => temp[0].Trim() == line.Split(',')[y].Trim()) != null)
            {
              category_signals.Add(line.Split(',')[y]);
            }
          }

          if (category_signals.Count > 0)
          {
            p_SignalCategories.Add((p_SignalCategories.Count + 1) + ". " + category);
            p_SignalCategoryContents.Add(category_signals);
          }
        }

        sr.Close();
      }
    }
    private void WriteToCategoriesFile()
    {
      List<string> temp_SignalCategories = new List<string>();
      List<List<string>> temp_SignalCategoriesContents = new List<List<string>>();

      if (File.Exists("signal_categories.txt"))
      {
        StreamReader sr = new StreamReader("signal_categories.txt");
        string[] text = sr.ReadToEnd().Replace("\r\n", "\n").Split('\n');

        for (int x = 0; x < text.Length; x++)
        {
          string line = text[x];

          string category = line.Split(',')[0].Trim();
          List<string> category_signals = new List<string>();

          for (int y = 1; y < line.Split(',').Length; y++)
          {
            category_signals.Add(line.Split(',')[y]);
          }

          if (!p_SignalCategories.Contains(category))
          {
            for (int y = 0; y < AllSignals.Count; y++)
            {
              if (category_signals.Contains(AllSignals[y]))
                category_signals.Remove(AllSignals[y]);
            }
          }

          temp_SignalCategories.Add(category);
          temp_SignalCategoriesContents.Add(category_signals);
        }

        sr.Close();
      }

      for (int x = 0; x < p_SignalCategories.Count; x++)
      {
        if (temp_SignalCategories.Contains(p_SignalCategories[x].Substring(p_SignalCategories[x].IndexOf('.') + 2).Trim()))
        {
          int u = temp_SignalCategories.IndexOf(p_SignalCategories[x].Substring(p_SignalCategories[x].IndexOf('.') + 2).Trim());
          temp_SignalCategoriesContents[u].AddRange(p_SignalCategoryContents[x].ToArray());
          temp_SignalCategoriesContents[u] = temp_SignalCategoriesContents[u].Distinct().ToList();
        }
        else
        {
          temp_SignalCategories.Add(p_SignalCategories[x].Substring(p_SignalCategories[x].IndexOf('.') + 2).Trim());
          temp_SignalCategoriesContents.Add(p_SignalCategoryContents[x]);
        }
      }

      StreamWriter sw = new StreamWriter("signal_categories.txt");
      for (int x = 0; x < temp_SignalCategories.Count; x++)
      {
        string line = temp_SignalCategories[x].Trim();
        if (line.Trim() != "")
        {
          for (int y = 0; y < temp_SignalCategoriesContents[x].Count; y++)
            line += "," + temp_SignalCategoriesContents[x][y].Trim();

          sw.WriteLine(line);
        }
      }
      sw.Close();
    }
    public void ManageCategories()
    {
      Dialog_Manage_Categories dlg = new Dialog_Manage_Categories(p_window,
                                                                  this,
                                                                  p_SignalCategories.ToArray(),
                                                                  p_SignalCategoryContents.Select(temp => temp.ToArray()).ToArray(),
                                                                  LoadedEDFFile.Header.Signals.Select(temp => temp.Label.ToString().Trim()).ToArray(),
                                                                  p_DerivedSignals.Select(temp => temp[0].Trim()).ToArray());
      p_window.ShowMetroDialogAsync(dlg);
    }
    public void ManageCategoriesOutput(string[] categories, List<List<string>> categories_signals)
    {
      PreviewCurrentCategory = -1;
      p_SignalCategories = categories.ToList();
      p_SignalCategoryContents = categories_signals;
    }

    public void NextCategory()
    {
      if (PreviewCurrentCategory == p_SignalCategories.Count - 1)
        PreviewCurrentCategory = -1;
      else
        PreviewCurrentCategory++;
    }
    public void PreviousCategory()
    {
      if (PreviewCurrentCategory == -1)
        PreviewCurrentCategory = p_SignalCategories.Count - 1;
      else
        PreviewCurrentCategory--;
    }

    // Preview Derivative Management
    private List<string[]> p_DerivedSignals = new List<string[]>();
    private void LoadCommonDerivativesFile()
    {
      p_DerivedSignals.Clear();
      if (File.Exists("common_derivatives.txt"))
      {
        List<string> text = new StreamReader("common_derivatives.txt").ReadToEnd().Replace("\r\n", "\n").Split('\n').ToList();
        for (int x = 0; x < text.Count; x++)
        {
          string[] new_entry = text[x].Split(',');

          if (new_entry.Length == 3)
          {
            if (LoadedEDFFile.Header.Signals.Find(temp => temp.Label.Trim() == new_entry[1].Trim()) != null) // Signals Exist
            {
              if (LoadedEDFFile.Header.Signals.Find(temp => temp.Label.Trim() == new_entry[2].Trim()) != null) // Signals Exist
              {
                if (LoadedEDFFile.Header.Signals.Find(temp => temp.Label.Trim() == new_entry[0].Trim()) == null) // Unique Name
                {
                  if (p_DerivedSignals.Where(temp => temp[0].Trim() == new_entry[0].Trim()).ToList().Count == 0) // Unique Name
                  {
                    p_DerivedSignals.Add(new_entry);
                  }
                }
              }
            }
          }
        }
      }
    }
    private void AddToCommonDerivativesFile(string name, string signal1, string signal2)
    {
      StreamWriter sw = new StreamWriter("common_derivatives.txt", true);
      sw.WriteLine(name + "," + signal1 + "," + signal2);
      sw.Close();
    }
    private void RemoveFromCommonDerivativesFile(List<string[]> signals)
    {
      if (File.Exists("common_derivatives.txt"))
      {
        StreamReader sr = new StreamReader("common_derivatives.txt");
        List<string> text = sr.ReadToEnd().Split('\n').ToList();
        sr.Close();
        for (int x = 0; x < text.Count; x++)
        {
          for (int y = 0; y < signals.Count; y++)
          {
            if (text[x].Split(',').Length != 3 || text[x].Split(',')[0].Trim() == signals[y][0].Trim() && text[x].Split(',')[1].Trim() == signals[y][1].Trim() && text[x].Split(',')[2].Trim() == signals[y][2].Trim())
            {
              text.Remove(text[x]);
              x--;
            }
          }
        }

        StreamWriter sw = new StreamWriter("common_derivatives.txt");
        for (int x = 0; x < text.Count; x++)
        {
          sw.WriteLine(text[x].Trim());
        }
        sw.Close();
      }
    }
    public void AddDerivative()
    {
      Dialog_Add_Derivative dlg = new Dialog_Add_Derivative(p_window,
                                                            this,
                                                            LoadedEDFFile.Header.Signals.Select(temp => temp.Label.Trim()).ToArray(),
                                                            p_DerivedSignals.Select(temp => temp[0].Trim()).ToArray());
      p_window.ShowMetroDialogAsync(dlg);
    }
    public void AddDerivativeOutput(string name, string signal1, string signal2)
    {
      p_DerivedSignals.Add(new string[] { name, signal1, signal2 });
      AddToCommonDerivativesFile(name, signal1, signal2);

      OnPropertyChanged(nameof(PreviewSignals));
      OnPropertyChanged(nameof(AllNonHiddenSignals));
    }
    public void RemoveDerivative()
    {
      Dialog_Remove_Derivative dlg = new Dialog_Remove_Derivative(p_window,
                                                                  this,
                                                                  p_DerivedSignals.ToArray());
      p_window.ShowMetroDialogAsync(dlg);
    }
    public void RemoveDerivativeOutput(string[] RemovedSignals)
    {
      for (int x = 0; x < RemovedSignals.Length; x++)
      {
        List<string[]> RemovedDerivatives = p_DerivedSignals.FindAll(temp => temp[0].Trim() == RemovedSignals[x].Trim()).ToList();
        p_DerivedSignals.RemoveAll(temp => temp[0].Trim() == RemovedSignals[x].Trim());
        RemoveFromCommonDerivativesFile(RemovedDerivatives);

        if (pm.PreviewSelectedSignals.Contains(RemovedSignals[x].Trim()))
        {
          pm.PreviewSelectedSignals.Remove(RemovedSignals[x].Trim());
        }

        // Remove Potentially Saved Min/Max Values
        p_SignalsMaxValues.RemoveAll(temp => temp[0].Trim() == RemovedSignals[x].Trim());
        p_SignalsMinValues.RemoveAll(temp => temp[0].Trim() == RemovedSignals[x].Trim());
      }

      OnPropertyChanged(nameof(PreviewSignals));
      OnPropertyChanged(nameof(AllNonHiddenSignals));
    }

    // Hidden Signal Management
    private List<string> p_HiddenSignals = new List<string>();
    private void LoadHiddenSignalsFile()
    {
      p_HiddenSignals.Clear();
      if (File.Exists("hiddensignals.txt"))
      {
        StreamReader sr = new StreamReader("hiddensignals.txt");
        p_HiddenSignals = sr.ReadToEnd().Replace("\r\n", "\n").Split('\n').ToList();
        p_HiddenSignals = p_HiddenSignals.Select(temp => temp.Trim()).Where(temp => temp != "").ToList();
        sr.Close();
      }
    }
    private void WriteToHiddenSignalsFile()
    {
      StreamWriter sw = new StreamWriter("hiddensignals.txt");
      for (int x = 0; x < p_HiddenSignals.Count; x++)
      {
        sw.WriteLine(p_HiddenSignals[x]);
      }
      sw.Close();
    }
    public void HideSignals()
    {
      bool[] input = new bool[EDFAllSignals.Count];
      for (int x = 0; x < EDFAllSignals.Count; x++)
      {
        if (p_HiddenSignals.Contains(EDFAllSignals[x]))
          input[x] = true;
        else
          input[x] = false;
      }

      Dialog_Hide_Signals dlg = new Dialog_Hide_Signals(p_window,
                                                        this,
                                                        EDFAllSignals.ToArray(),
                                                        input);
      p_window.ShowMetroDialogAsync(dlg);
    }
    public void HideSignalsOutput(bool[] hide_signals_new)
    {
      for (int x = 0; x < hide_signals_new.Length; x++)
      {
        if (hide_signals_new[x])
        {
          if (!p_HiddenSignals.Contains(EDFAllSignals[x]))
          {
            p_HiddenSignals.Add(EDFAllSignals[x]);
          }
        }
        else
        {
          if (p_HiddenSignals.Contains(EDFAllSignals[x]))
          {
            p_HiddenSignals.Remove(EDFAllSignals[x]);
          }
        }
      }

      OnPropertyChanged(nameof(PreviewSignals));
      OnPropertyChanged(nameof(AllNonHiddenSignals));

      WriteToHiddenSignalsFile();
    }

    private List<string[]> p_SignalsMinValues = new List<string[]>();
    private List<string[]> p_SignalsMaxValues = new List<string[]>();
    private double? GetMaxSignalValue(string Signal, List<float> values)
    {
      string[] find = p_SignalsMaxValues.Find(temp => temp[0].Trim() == Signal.Trim());

      if (find != null)
        return Double.Parse(find[1]);
      else
      {
        double? value = null;
        value = GetPercentileValue(values.ToArray(), 99);
        SetMaxSignalValue(Signal, value ?? 0);
        return value;
      }
    }
    private double? GetMinSignalValue(string Signal, List<float> values)
    {
      string[] find = p_SignalsMinValues.Find(temp => temp[0].Trim() == Signal.Trim());

      if (find != null)
        return Double.Parse(find[1]);
      else
      {
        double? value = null;
        value = GetPercentileValue(values.ToArray(), 1);
        SetMinSignalValue(Signal, value ?? 0);
        return value;
      }
    }
    private double? GetMaxSignalValue(string Signal, List<float> values1, List<float> values2)
    {
      string[] find = p_SignalsMaxValues.Find(temp => temp[0].Trim() == Signal.Trim());

      if (find != null)
        return Double.Parse(find[1]);
      else
      {
        double? value = null;
        value = GetPercentileValueDeriv(values1.ToArray(), values2.ToArray(), 99);
        SetMaxSignalValue(Signal, value ?? 0);
        return value;
      }
    }
    private double? GetMinSignalValue(string Signal, List<float> values1, List<float> values2)
    {
      string[] find = p_SignalsMinValues.Find(temp => temp[0].Trim() == Signal.Trim());

      if (find != null)
        return Double.Parse(find[1]);
      else
      {
        double? value = null;
        value = GetPercentileValueDeriv(values1.ToArray(), values2.ToArray(), 1);
        SetMinSignalValue(Signal, value ?? 0);
        return value;
      }
    }

    private void SetMaxSignalValue(string Signal, double Value)
    {
      string[] find = p_SignalsMaxValues.Find(temp => temp[0].Trim() == Signal.Trim());

      if (find != null)
      {
        p_SignalsMaxValues.Remove(find);
      }

      p_SignalsMaxValues.Add(new string[] { Signal.Trim(), Value.ToString() });
    }
    private void SetMinSignalValue(string Signal, double Value)
    {
      string[] find = p_SignalsMinValues.Find(temp => temp[0].Trim() == Signal.Trim());

      if (find != null)
      {
        p_SignalsMinValues.Remove(find);
      }

      p_SignalsMinValues.Add(new string[] { Signal.Trim(), Value.ToString() });
    }

    public void WriteSettings()
    {
      WriteToHiddenSignalsFile();
      WriteToCategoriesFile();
    }
    public void LoadSettings()
    {
      LoadHiddenSignalsFile();
      LoadCommonDerivativesFile();
      LoadCategoriesFile();
      OnPropertyChanged(nameof(PreviewSignals));
      OnPropertyChanged(nameof(AllNonHiddenSignals));
    }

    /*********************************************************************************************************************************/

    #endregion

    #region Members

    /*********************************************************************************************************************************/

    /// <summary>
    /// The Window
    /// </summary>
    private MainWindow p_window;
    /// <summary>
    /// The Loaded EDF File
    /// </summary>
    private EDFFile p_LoadedEDFFile;
    /// <summary>
    /// The Loaded EDF Filename
    /// </summary>
    private string p_LoadedEDFFileName = null;

    /// <summary>
    /// Preview Model
    /// </summary>
    private PreviewModel pm = new PreviewModel();

    /// <summary>
    /// Respiratory Model
    /// </summary>
    private RespiratoryModel rm = new RespiratoryModel();

    /// <summary>
    /// EEG Model
    /// </summary>
    private EEGModel eegm = new EEGModel();

    /// <summary>
    /// Coherence Model
    /// </summary>
    private CoherenceModel cm = new CoherenceModel();

    /*********************************************************************************************************************************/

    #endregion

    #region Properties 

    /*********************************************************************************************************************************/

    // Update Actions
    private void LoadedEDFFile_Changed()
    {
      PreviewCurrentCategory = -1;

      // Preview Time Picker
      if (p_LoadedEDFFile == null)
      {
        PreviewUseAbsoluteTime = false;
        PreviewViewStartTime = null;
        PreviewViewStartRecord = null;
        PreviewViewDuration = null;
        LoadedEDFFileName = null;

        RespiratoryBreathingPeriodMean = "";
        RespiratoryBreathingPeriodMedian = "";
        RespiratorySignalPlot = null;
        RespiratoryEDFSelectedSignal = null;
        RespiratoryEDFDuration = null;
        RespiratoryEDFStartRecord = null;

        CoherenceEDFSelectedSignal1 = null;
        CoherenceEDFSelectedSignal2 = null;
        CoherenceSignalPlot1 = null;
        CoherenceSignalPlot2 = null;
        CoherencePlot = null;
        CoherenceEDFDuration = null;
        CoherenceEDFStartRecord = null;
      }
      else
      {
        PreviewUseAbsoluteTime = false;
        PreviewViewStartTime = LoadedEDFFile.Header.StartDateTime;
        PreviewViewStartRecord = 0;
        PreviewViewDuration = 5;

        RespiratoryBreathingPeriodMean = "";
        RespiratoryBreathingPeriodMedian = "";
        RespiratoryEDFSelectedSignal = null;
        RespiratorySignalPlot = null;
        RespiratoryEDFDuration = 1;
        RespiratoryEDFStartRecord = 0;

        CoherenceEDFSelectedSignal1 = null;
        CoherenceEDFSelectedSignal2 = null;
        CoherenceSignalPlot1 = null;
        CoherenceSignalPlot2 = null;
        CoherencePlot = null;
        CoherenceEDFDuration = 1;
        CoherenceEDFStartRecord = 0;
      }
      OnPropertyChanged(nameof(PreviewNavigationEnabled));

      // Header
      OnPropertyChanged(nameof(EDFStartTime));
      OnPropertyChanged(nameof(EDFEndTime));
      OnPropertyChanged(nameof(EDFPatientName));
      OnPropertyChanged(nameof(EDFPatientSex));
      OnPropertyChanged(nameof(EDFPatientCode));
      OnPropertyChanged(nameof(EDFPatientBirthDate));
      OnPropertyChanged(nameof(EDFRecordEquipment));
      OnPropertyChanged(nameof(EDFRecordCode));
      OnPropertyChanged(nameof(EDFRecordTechnician));
      OnPropertyChanged(nameof(EDFAllSignals));

      // Misc
      OnPropertyChanged(nameof(IsEDFLoaded));

      // Coherence
      OnPropertyChanged(nameof(CoherenceEDFNavigationEnabled));
    }
    private void PreviewCurrentCategory_Changed()
    {
      OnPropertyChanged(nameof(PreviewCurrentCategoryName));
      OnPropertyChanged(nameof(PreviewSignals));
    }
    private void PreviewUseAbsoluteTime_Changed()
    {
      OnPropertyChanged(nameof(PreviewViewDuration));

      OnPropertyChanged(nameof(PreviewViewStartTimeMax));
      OnPropertyChanged(nameof(PreviewViewStartTimeMin));
      OnPropertyChanged(nameof(PreviewViewStartRecordMax));
      OnPropertyChanged(nameof(PreviewViewStartRecordMin));
      OnPropertyChanged(nameof(PreviewViewDurationMax));
      OnPropertyChanged(nameof(PreviewViewDurationMin));

      DrawChart();
    }
    private void PreviewView_Changed()
    {
      OnPropertyChanged(nameof(PreviewViewStartRecord));
      OnPropertyChanged(nameof(PreviewViewStartTime));
      OnPropertyChanged(nameof(PreviewViewDuration));

      OnPropertyChanged(nameof(PreviewViewStartTimeMax));
      OnPropertyChanged(nameof(PreviewViewStartTimeMin));
      OnPropertyChanged(nameof(PreviewViewStartRecordMax));
      OnPropertyChanged(nameof(PreviewViewStartRecordMin));
      OnPropertyChanged(nameof(PreviewViewDurationMax));
      OnPropertyChanged(nameof(PreviewViewDurationMin));

      DrawChart();
    }
    private void PreviewSignalPlot_Changed()
    {
      PreviewNavigationEnabled = true;
    }
    private void CoherencePlot_Changed()
    {
      CoherenceProgressRingEnabled = false;
    }

    /*********************************************************** GENERAL ************************************************************/

    // Loaded EDF Structure and File Name
    public EDFFile LoadedEDFFile
    {
      get
      {
        return p_LoadedEDFFile;
      }
      set
      {
        p_LoadedEDFFile = value;
        LoadedEDFFile_Changed();
      }
    }
    public string LoadedEDFFileName
    {
      get
      {
        return p_LoadedEDFFileName ?? "No File Loaded";
      }
      set
      {
        p_LoadedEDFFileName = value;
        OnPropertyChanged(nameof(LoadedEDFFileName));
      }
    }
    public bool IsEDFLoaded
    {
      get
      {
        return LoadedEDFFile != null;
      }
    }

    /********************************************************* PREVIEW TAB **********************************************************/

    // EDF Header
    public string EDFStartTime
    {
      get
      {
        if (IsEDFLoaded)
          return LoadedEDFFile.Header.StartDateTime.ToString();
        else
          return null;
      }
    }
    public string EDFEndTime
    {
      get
      {
        if (IsEDFLoaded)
        {
          DateTime EndTime = LoadedEDFFile.Header.StartDateTime 
                             + new TimeSpan(
                               (long)(TimeSpan.TicksPerSecond * LoadedEDFFile.Header.DurationOfDataRecordInSeconds * LoadedEDFFile.Header.NumberOfDataRecords)
                               );
          return EndTime.ToString();
        }
        else
          return "";
      }
    }
    public string EDFPatientName
    {
      get
      {
        if (IsEDFLoaded)
          return LoadedEDFFile.Header.PatientIdentification.PatientName;
        else
          return "";
      }
    }
    public string EDFPatientSex
    {
      get
      {
        if (IsEDFLoaded)
          return LoadedEDFFile.Header.PatientIdentification.PatientSex;
        else
          return "";
      }
    }
    public string EDFPatientCode
    {
      get
      {
        if (IsEDFLoaded)
          return LoadedEDFFile.Header.PatientIdentification.PatientCode;
        else
          return "";
      }
    }
    public string EDFPatientBirthDate
    {
      get
      {
        if (IsEDFLoaded)
          return LoadedEDFFile.Header.PatientIdentification.PatientBirthDate.ToString();
        else
          return "";
      }
    }
    public string EDFRecordEquipment
    {
      get
      {
        if (IsEDFLoaded)
          return LoadedEDFFile.Header.RecordingIdentification.RecordingEquipment;
        else
          return "";
      }
    }
    public string EDFRecordCode
    {
      get
      {
        if (IsEDFLoaded)
          return LoadedEDFFile.Header.RecordingIdentification.RecordingCode;
        else
          return "";
      }
    }
    public string EDFRecordTechnician
    {
      get
      {
        if (IsEDFLoaded)
          return LoadedEDFFile.Header.RecordingIdentification.RecordingTechnician;
        else
          return "";
      }
    }
    public ReadOnlyCollection<string> AllSignals
    {
      get
      {
        if (IsEDFLoaded)
        {
          List<string> output = new List<string>();
          output.AddRange(LoadedEDFFile.Header.Signals.Select(temp => temp.Label.ToString().Trim()).ToArray());
          output.AddRange(p_DerivedSignals.Select(temp => temp[0].Trim()).ToArray());
          return Array.AsReadOnly(output.ToArray());
        }
        else
        {
          return Array.AsReadOnly(new string[0]);
        }
      }
    }
    public ReadOnlyCollection<string> EDFAllSignals
    {
      get
      {
        if (IsEDFLoaded)
          return Array.AsReadOnly(LoadedEDFFile.Header.Signals.Select(temp => temp.Label.ToString().Trim()).ToArray());
        else
          return Array.AsReadOnly(new string[0]);
      }
    }
    public ReadOnlyCollection<string> AllNonHiddenSignals
    {
      get
      {
        if (IsEDFLoaded)
        {
          List<string> output = new List<string>();
          output.AddRange(LoadedEDFFile.Header.Signals.Select(temp => temp.Label.ToString().Trim()).Where(temp => !p_HiddenSignals.Contains(temp)).ToArray());
          output.AddRange(p_DerivedSignals.Select(temp => temp[0].Trim()).ToArray());
          return Array.AsReadOnly(output.ToArray());
        }
        else
        {
          return Array.AsReadOnly(new string[0]);
        }
      }
    }

    // Preview Signal Selection
    public int PreviewCurrentCategory
    {
      get
      {
        return pm.PreviewCurrentCategory;
      }
      set
      {
        pm.PreviewCurrentCategory = value;
        OnPropertyChanged(nameof(PreviewCurrentCategory));
        PreviewCurrentCategory_Changed();
      }
    }
    public string PreviewCurrentCategoryName
    {
      get
      {
        if (PreviewCurrentCategory == -1)
          return "All";
        else
          return p_SignalCategories[PreviewCurrentCategory];
      }
    }
    public ReadOnlyCollection<string> PreviewSignals
    {
      get
      {
        if (IsEDFLoaded)
        {
          if (PreviewCurrentCategory != -1)
            return Array.AsReadOnly(p_SignalCategoryContents[PreviewCurrentCategory].Where(temp => !p_HiddenSignals.Contains(temp)).ToArray());
          else
          {
            List<string> output = new List<string>();
            output.AddRange(LoadedEDFFile.Header.Signals.Select(temp => temp.Label.ToString().Trim()).Where(temp => !p_HiddenSignals.Contains(temp)).ToArray());
            output.AddRange(p_DerivedSignals.Select(temp => temp[0].Trim()).ToArray());
            return Array.AsReadOnly(output.ToArray());
          }
        }
        else
        {
          return Array.AsReadOnly(new string[0]);
        }
      }
    }
    public void SetSelectedSignals(System.Collections.IList SelectedItems)
    {
      pm.PreviewSelectedSignals.Clear();
      for (int x = 0; x < SelectedItems.Count; x++)
        pm.PreviewSelectedSignals.Add(SelectedItems[x].ToString());

      DrawChart();
    }

    // Preview Plot Range
    public bool PreviewUseAbsoluteTime
    {
      get
      {
        return pm.PreviewUseAbsoluteTime;
      }
      set
      {
        pm.PreviewUseAbsoluteTime = value;
        OnPropertyChanged(nameof(PreviewUseAbsoluteTime));
        PreviewUseAbsoluteTime_Changed();
      }
    }
    public DateTime? PreviewViewStartTime
    {
      get
      {
        if (IsEDFLoaded)
        {
          if (PreviewUseAbsoluteTime)
            return pm.PreviewViewStartTime;
          else
            return EpochtoDateTime(pm.PreviewViewStartRecord, LoadedEDFFile);
        }
        else
        {
          return null;
        }
      }
      set
      {
        if (PreviewUseAbsoluteTime && IsEDFLoaded)
        {
          pm.PreviewViewStartTime = value ?? new DateTime();
          pm.PreviewViewStartRecord = DateTimetoEpoch(pm.PreviewViewStartTime, LoadedEDFFile);
          PreviewView_Changed();
        }
      }
    }
    public int? PreviewViewStartRecord
    {
      get
      {
        if (IsEDFLoaded)
        {
          if (PreviewUseAbsoluteTime)
            return DateTimetoEpoch(PreviewViewStartTime ?? new DateTime(), LoadedEDFFile);
          else
            return pm.PreviewViewStartRecord;
        }
        else
        {
          return null;
        }
      }
      set
      {
        if (!PreviewUseAbsoluteTime && IsEDFLoaded)
        {
          pm.PreviewViewStartRecord = value ?? 0;
          pm.PreviewViewStartTime = EpochtoDateTime(pm.PreviewViewStartRecord, LoadedEDFFile);
          PreviewView_Changed();
        }
      }
    }
    public int? PreviewViewDuration
    {
      get
      {
        if (IsEDFLoaded)
        {
          if (PreviewUseAbsoluteTime)
            return pm.PreviewViewDuration;
          else
            return TimeSpantoEpochPeriod(new TimeSpan(0, 0, pm.PreviewViewDuration));
        }
        else
        {
          return null;
        }
      }
      set
      {
        if (IsEDFLoaded)
        {
          if (PreviewUseAbsoluteTime)
            pm.PreviewViewDuration = value ?? 0;
          else
            pm.PreviewViewDuration = (int)EpochPeriodtoTimeSpan((value ?? 0)).TotalSeconds;
        }

        PreviewView_Changed();
      }
    }
    public DateTime PreviewViewEndTime
    {
      get
      {
        if (IsEDFLoaded)
        {
          if (PreviewUseAbsoluteTime)
            return (PreviewViewStartTime ?? new DateTime()) + new TimeSpan(0, 0, 0, PreviewViewDuration ?? 0);
          else
            return (PreviewViewStartTime ?? new DateTime()) + EpochPeriodtoTimeSpan(PreviewViewDuration ?? 0);
        }
        else
        {
          return new DateTime();
        }
      }
    }

    public DateTime PreviewViewStartTimeMax
    {
      get
      {
        if (LoadedEDFFile != null)
        {
          DateTime EndTime = DateTime.Parse(EDFEndTime); // EDF End Time
          TimeSpan duration = new TimeSpan(TimeSpan.TicksPerSecond * pm.PreviewViewDuration); // User Selected Duration 
          return EndTime - duration; 
        }
        else
          return new DateTime();
      }
    }
    public DateTime PreviewViewStartTimeMin
    {
      get
      {
        if (LoadedEDFFile != null)
          return LoadedEDFFile.Header.StartDateTime; // Start Time
        else
          return new DateTime();
      }
    }
    public int PreviewViewStartRecordMax
    {
      get
      {
        if (LoadedEDFFile != null)
          return DateTimetoEpoch(PreviewViewStartTimeMax, LoadedEDFFile); // PreviewViewStartTimeMax to Record
        else
          return 0;
      }
    }
    public int PreviewViewStartRecordMin
    {
      get
      {
        return 0; // Record 0
      }
    }
    public int PreviewViewDurationMax
    {
      get
      {
        if (LoadedEDFFile != null) // File Loaded
        {
          DateTime EndTime = DateTime.Parse(EDFEndTime); // EDF End Time
          TimeSpan duration = EndTime - (PreviewViewStartTime ?? new DateTime()); // Theoretical Limit Duration
          TimeSpan limit = new TimeSpan(TimeSpan.TicksPerHour * 2); // Practical Limit Duration

          if (pm.PreviewUseAbsoluteTime)
            return Math.Min(
                (int)limit.TotalSeconds,
                (int)duration.TotalSeconds
                );
          else
            return Math.Min(
                TimeSpantoEpochPeriod(limit),
                TimeSpantoEpochPeriod(duration)
                );
        }
        else // No File Loaded
          return 0;
      }
    }
    public int PreviewViewDurationMin
    {
      get
      {
        if (LoadedEDFFile != null) // File Loaded
          return 1;
        else // No File Loaded
          return 0;
      }
    }
    public bool PreviewNavigationEnabled
    {
      get
      {
        if (!IsEDFLoaded)
          return false;
        else
          return pm.PreviewNavigationEnabled;
      }
      set
      {
        pm.PreviewNavigationEnabled = value;
        OnPropertyChanged(nameof(PreviewNavigationEnabled));
        OnPropertyChanged(nameof(PreviewProgressRingEnabled));
      }
    }
    public bool PreviewProgressRingEnabled
    {
      get
      {
        if (!IsEDFLoaded)
          return false;
        else
          return !pm.PreviewNavigationEnabled;
      }
    }

    // Preview Plot
    public PlotModel PreviewSignalPlot
    {
      get
      {
        return pm.PreviewSignalPlot;
      }
      set
      {
        pm.PreviewSignalPlot = value;
        OnPropertyChanged(nameof(PreviewSignalPlot));
        PreviewSignalPlot_Changed();
      }
    }

    /************************************************** RESPIRATORY ANALYSIS TAB ****************************************************/

    // Respiratory Analysis
    public string RespiratoryEDFSelectedSignal
    {
      get
      {
        return rm.RespiratoryEDFSelectedSignal;
      }
      set
      {
        rm.RespiratoryEDFSelectedSignal = value;
        OnPropertyChanged(nameof(RespiratoryEDFSelectedSignal));
      }
    }
    public int? RespiratoryEDFStartRecord
    {
      get
      {
        return rm.RespiratoryEDFStartRecord;
      }
      set
      {
        rm.RespiratoryEDFStartRecord = value ?? 0;
        OnPropertyChanged(nameof(RespiratoryEDFStartRecord));
      }
    }
    public int? RespiratoryEDFDuration
    {
      get
      {
        return rm.RespiratoryEDFDuration;
      }
      set
      {
        rm.RespiratoryEDFDuration = value ?? 0;
        OnPropertyChanged(nameof(RespiratoryEDFDuration));
      }
    }
    public PlotModel RespiratorySignalPlot
    {
      get
      {
        return rm.RespiratorySignalPlot;
      }
      set
      {
        rm.RespiratorySignalPlot = value;
        OnPropertyChanged(nameof(RespiratorySignalPlot));
        p_window.Dispatcher.Invoke(new Action(() => { p_window.TextBlock_RespPendingChanges.Visibility = Visibility.Hidden; }));
      }
    }
    public string RespiratoryBreathingPeriodMean
    {
      get
      {
        return rm.RespiratoryBreathingPeriodMean;
      }
      set
      {
        rm.RespiratoryBreathingPeriodMean = value;
        OnPropertyChanged(nameof(RespiratoryBreathingPeriodMean));
      }
    }
    public string RespiratoryBreathingPeriodMedian
    {
      get
      {
        return rm.RespiratoryBreathingPeriodMedian;
      }
      set
      {
        rm.RespiratoryBreathingPeriodMedian = value;
        OnPropertyChanged(nameof(RespiratoryBreathingPeriodMedian));
      }
    }
    public int RespiratoryMinimumPeakWidth
    {
      get
      {
        return rm.RespiratoryMinimumPeakWidth;
      }
      set
      {
        rm.RespiratoryMinimumPeakWidth = value;
        OnPropertyChanged(nameof(RespiratoryMinimumPeakWidth));
        p_window.Dispatcher.Invoke(new Action(() => { p_window.TextBlock_RespPendingChanges.Visibility = Visibility.Visible; }));
      }
    }
    public bool RespiratoryRemoveMultiplePeaks
    {
      get
      {
        return rm.RespiratoryRemoveMultiplePeaks;
      }
      set
      {
        rm.RespiratoryRemoveMultiplePeaks = value;
        OnPropertyChanged(nameof(RespiratoryRemoveMultiplePeaks));
        p_window.Dispatcher.Invoke(new Action(() => { p_window.TextBlock_RespPendingChanges.Visibility = Visibility.Visible; }));
        if (value == true)
          p_window.Dispatcher.Invoke(new Action(() => { p_window.TextBlock_RespAllowMultiplePeaks.Text = "No"; }));
        else
          p_window.Dispatcher.Invoke(new Action(() => { p_window.TextBlock_RespAllowMultiplePeaks.Text = "Yes"; }));

      }
    }

    /****************************************************** EEG ANALYSIS TAB ********************************************************/

    //EEG Anaylsis
    public string EEGEDFSelectedSignal
    {
      get
      {
        return eegm.EEGEDFSelectedSignal;
      }
      set
      {
        eegm.EEGEDFSelectedSignal = value;
        OnPropertyChanged(nameof(EEGEDFSelectedSignal));
      }
    }
    public int? EEGEDFStartRecord
    {
      get
      {
        return eegm.EEGEDFStartRecord;
      }
      set
      {
        eegm.EEGEDFStartRecord = value ?? 0;
        OnPropertyChanged(nameof(EEGEDFStartRecord));
      }
    }
    public int? EEGEDFDuration
    {
      get
      {
        return eegm.EEGEDFDuration;
      }
      set
      {
        eegm.EEGEDFDuration = value ?? 0;
        OnPropertyChanged(nameof(EEGEDFDuration));
      }
    }
    public PlotModel PlotAbsPwr
    {
      get
      {
        return eegm.PlotAbsPwr;
      }
      set
      {
        eegm.PlotAbsPwr = value;
        OnPropertyChanged(nameof(PlotAbsPwr));
        p_window.Dispatcher.Invoke(new Action(() => { p_window.TextBlock_RespPendingChanges.Visibility = Visibility.Hidden; }));
      }
    }
    public PlotModel PlotRelPwr
    {
      get
      {
        return eegm.PlotRelPwr;
      }
      set
      {
        eegm.PlotRelPwr = value;
        OnPropertyChanged(nameof(PlotRelPwr));
        p_window.Dispatcher.Invoke(new Action(() => { p_window.TextBlock_RespPendingChanges.Visibility = Visibility.Hidden; }));
      }
    }

    public PlotModel PlotSpecGram
    {
      get
      {
        return eegm.PlotSpecGram;
      }
      set
      {
        eegm.PlotSpecGram = value;
        OnPropertyChanged(nameof(PlotSpecGram));
        p_window.Dispatcher.Invoke(new Action(() => { p_window.TextBlock_RespPendingChanges.Visibility = Visibility.Hidden; }));
      }
    }

    public PlotModel PlotPSD
    {
      get
      {
        return eegm.PlotPSD;
      }
      set
      {
        eegm.PlotPSD = value;
        OnPropertyChanged(nameof(PlotPSD));
        p_window.Dispatcher.Invoke(new Action(() => { p_window.TextBlock_RespPendingChanges.Visibility = Visibility.Hidden; }));
      }
    }

    /**************************************************** COHERENCE ANALYSIS TAB ****************************************************/

    public string CoherenceEDFSelectedSignal1
    {
      get
      {
        return cm.CoherenceEDFSelectedSignal1;
      }
      set
      {
        cm.CoherenceEDFSelectedSignal1 = value;
        OnPropertyChanged(nameof(CoherenceEDFSelectedSignal1));
      }
    }
    public string CoherenceEDFSelectedSignal2
    {
      get
      {
        return cm.CoherenceEDFSelectedSignal2;
      }
      set
      {
        cm.CoherenceEDFSelectedSignal2 = value;
        OnPropertyChanged(nameof(CoherenceEDFSelectedSignal2));
      }
    }
    public int? CoherenceEDFStartRecord
    {
      get
      {
        return cm.CoherenceEDFStartRecord;
      }
      set
      {
        cm.CoherenceEDFStartRecord = value ?? 0;
        OnPropertyChanged(nameof(CoherenceEDFStartRecord));
      }
    }
    public int? CoherenceEDFDuration
    {
      get
      {
        return cm.CoherenceEDFDuration;
      }
      set
      {
        cm.CoherenceEDFDuration = value ?? 0;
        OnPropertyChanged(nameof(CoherenceEDFDuration));
      }
    }
    public PlotModel CoherenceSignalPlot1
    {
      get
      {
        return cm.CoherenceSignalPlot1;
      }
      set
      {
        cm.CoherenceSignalPlot1 = value;
        OnPropertyChanged(nameof(CoherenceSignalPlot1));
      }
    }
    public PlotModel CoherenceSignalPlot2
    {
      get
      {
        return cm.CoherenceSignalPlot2;
      }
      set
      {
        cm.CoherenceSignalPlot2 = value;
        OnPropertyChanged(nameof(CoherenceSignalPlot2));
      }
    }
    public PlotModel CoherencePlot
    {
      get
      {
        return cm.CoherencePlot;
      }
      set
      {
        cm.CoherencePlot = value;
        OnPropertyChanged(nameof(CoherencePlot));
        CoherencePlot_Changed();
      }
    }
    public bool CoherenceProgressRingEnabled
    {
      get
      {
        return cm.CoherenceProgressRingEnabled;
      }
      set
      {
        cm.CoherenceProgressRingEnabled = value;
        OnPropertyChanged(nameof(CoherenceProgressRingEnabled));
        OnPropertyChanged(nameof(CoherenceEDFNavigationEnabled));
      }
    }
    public bool CoherenceEDFNavigationEnabled
    {
      get
      {
        if (!IsEDFLoaded)
          return false;
        else
          return !CoherenceProgressRingEnabled;
      }
    }

    /*********************************************************************************************************************************/

    #endregion

    #region etc

    // INotify Interface
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string propertyName)
    {
      PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    public ModelView(MainWindow i_window)
    {
      p_window = i_window;

      #region Preload MATLAB functions into memory
      {
        BackgroundWorker bw = new BackgroundWorker();
        bw.DoWork += new DoWorkEventHandler(
          delegate (object sender1, DoWorkEventArgs e1)
          {
            MATLAB_Coherence(new float[] { 1, 1, 1, 1, 1, 1, 1, 1 }, new float[] { 1, 1, 1, 1, 1, 1, 1, 1 });
          }
          );
        bw.RunWorkerAsync();
      }
      {
        BackgroundWorker bw = new BackgroundWorker();
        bw.DoWork += new DoWorkEventHandler(
          delegate (object sender1, DoWorkEventArgs e1)
          {
            MATLAB_Resample(new float[] { 1, 1, 1, 1, 1, 1, 1, 1 }, 2);
          }
          );
        bw.RunWorkerAsync();
      }
      #endregion 
    }
  }
}
