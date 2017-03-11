﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.IO;
using EDF;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Axes;
using Excel = Microsoft.Office.Interop.Excel;

namespace SleepApneaDiagnoser
{
  /// <summary>
  /// Factory containing business logic used exclusively in the 'Respiratory' sub tab of the 'Analysis' tab
  /// </summary>
  public class RespiratoryFactory
  {
    #region Static Functions 
    public static double GetVarianceCoefficient(double[] values)
    {
      double mean = values.Average();
      double variance = 0;
      for (int x = 0; x < values.Length; x++)
      {
        variance += Math.Abs(values[x] - mean);
      }
      variance /= values.Length;
      return variance / Math.Abs(mean);
    }
    public static LineSeries RemoveBiasFromSignal(LineSeries series, double bias)
    {
      // Normalization
      LineSeries series_norm = new LineSeries();
      for (int x = 0; x < series.Points.Count; x++)
      {
        series_norm.Points.Add(new DataPoint(series.Points[x].X, series.Points[x].Y - bias));
      }

      return series_norm;
    }
    public static Tuple<ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries> GetPeaksAndOnsets(LineSeries series, bool RemoveMultiplePeaks, int min_spike_length)
    {
      int spike_length = 0;
      int maxima = 0;
      int start = 0;
      bool? positive = null;
      ScatterSeries series_pos_peaks = new ScatterSeries();
      ScatterSeries series_neg_peaks = new ScatterSeries();
      ScatterSeries series_insets = new ScatterSeries();
      ScatterSeries series_onsets = new ScatterSeries();
      for (int x = 0; x < series.Points.Count; x++)
      {
        // If positive spike
        if (positive != false)
        {
          // If end of positive spike
          if (series.Points[x].Y < 0 || x == series.Points.Count - 1)
          {
            // If spike is appropriate length
            if (spike_length > min_spike_length)
            {
              if (
                  // If user does not mind consequent peaks of same sign
                  !RemoveMultiplePeaks ||
                  // If first positive peak
                  series_pos_peaks.Points.Count == 0 ||
                  // If last peak was negative
                  (series_neg_peaks.Points.Count != 0 &&
                  DateTimeAxis.ToDateTime(series_neg_peaks.Points[series_neg_peaks.Points.Count - 1].X) >
                  DateTimeAxis.ToDateTime(series_pos_peaks.Points[series_pos_peaks.Points.Count - 1].X))
                 )
              {
                // Add new positive peak and onset 
                series_pos_peaks.Points.Add(new ScatterPoint(series.Points[maxima].X, series.Points[maxima].Y));
                series_onsets.Points.Add(new ScatterPoint(series.Points[start].X, series.Points[start].Y));
              }
              else
              {
                // If this peak is greater than the previous
                if (series.Points[maxima].Y > series_pos_peaks.Points[series_pos_peaks.Points.Count - 1].Y)
                {
                  // Replace previous spike maxima with latest spike maxima
                  series_pos_peaks.Points.Remove(series_pos_peaks.Points[series_pos_peaks.Points.Count - 1]);
                  series_onsets.Points.Remove(series_onsets.Points[series_onsets.Points.Count - 1]);
                  series_pos_peaks.Points.Add(new ScatterPoint(series.Points[maxima].X, series.Points[maxima].Y));
                  series_onsets.Points.Add(new ScatterPoint(series.Points[start].X, series.Points[start].Y));
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
            if (Math.Abs(series.Points[x].Y) > Math.Abs(series.Points[maxima].Y))
              maxima = x;
            spike_length++;
          }
        }
        // If negative spike
        else
        {
          // If end of negative spike
          if (series.Points[x].Y > 0 || x == series.Points.Count - 1)
          {
            // If spike is appropriate length
            if (spike_length > min_spike_length)
            {
              if (
                  // If user does not mind consequent peaks of same sign
                  !RemoveMultiplePeaks ||
                  // If first negative peak
                  series_neg_peaks.Points.Count == 0 ||
                  // If last peak was positive 
                  (series_pos_peaks.Points.Count != 0 &&
                  DateTimeAxis.ToDateTime(series_neg_peaks.Points[series_neg_peaks.Points.Count - 1].X) <
                  DateTimeAxis.ToDateTime(series_pos_peaks.Points[series_pos_peaks.Points.Count - 1].X))
                )
              {
                // Add new negative peak and onset 
                series_neg_peaks.Points.Add(new ScatterPoint(series.Points[maxima].X, series.Points[maxima].Y));
                series_insets.Points.Add(new ScatterPoint(series.Points[start].X, series.Points[start].Y));
              }
              else
              {
                // If this peak is less than the previous
                if (series.Points[maxima].Y < series_neg_peaks.Points[series_neg_peaks.Points.Count - 1].Y)
                {
                  // Replace previous spike maxima with latest spike maxima
                  series_neg_peaks.Points.Remove(series_neg_peaks.Points[series_neg_peaks.Points.Count - 1]);
                  series_insets.Points.Remove(series_insets.Points[series_insets.Points.Count - 1]);
                  series_neg_peaks.Points.Add(new ScatterPoint(series.Points[maxima].X, series.Points[maxima].Y));
                  series_insets.Points.Add(new ScatterPoint(series.Points[start].X, series.Points[start].Y));
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
            if (Math.Abs(series.Points[x].Y) > Math.Abs(series.Points[maxima].Y))
              maxima = x;
            spike_length++;
          }
        }
      }

      return new Tuple<ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries>(series_insets, series_onsets, series_neg_peaks, series_pos_peaks);
    }
    public static Tuple<LineSeries, ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries, DateTimeAxis, LinearAxis> GetRespiratoryAnalysisPlot(string SignalName, List<float> yValues, float sample_period, float bias, bool RemoveMultiplePeaks, float MinimumPeakWidth, DateTime ViewStartTime, DateTime ViewEndTime)
    {
      // Variable To Return
      LineSeries series = new LineSeries();

      //  // Add Points to Series
      for (int y = 0; y < yValues.Count; y++)
      {
        series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(ViewStartTime + new TimeSpan(0, 0, 0, 0, (int)(sample_period * (float)y * 1000))), yValues[y]));
      }

      LineSeries series_norm = RemoveBiasFromSignal(series, bias);

      // Find Peaks and Zero Crossings
      int min_spike_length = (int)((double)((double)MinimumPeakWidth / (double)1000) / (double)sample_period);
      Tuple<ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries> output = GetPeaksAndOnsets(series_norm, RemoveMultiplePeaks, min_spike_length);
      ScatterSeries series_insets = output.Item1;
      ScatterSeries series_onsets = output.Item2;
      ScatterSeries series_neg_peaks = output.Item3;
      ScatterSeries series_pos_peaks = output.Item4;

      // Modify Series colors
      series_onsets.MarkerFill = OxyColor.FromRgb(255, 0, 0);
      series_insets.MarkerFill = OxyColor.FromRgb(0, 255, 0);
      series_pos_peaks.MarkerFill = OxyColor.FromRgb(0, 0, 255);
      series_neg_peaks.MarkerFill = OxyColor.FromRgb(255, 255, 0);

      // Bind to Axes
      series_norm.YAxisKey = SignalName;
      series_norm.XAxisKey = "DateTime";
      series_onsets.YAxisKey = SignalName;
      series_onsets.XAxisKey = "DateTime";
      series_insets.YAxisKey = SignalName;
      series_insets.XAxisKey = "DateTime";
      series_pos_peaks.YAxisKey = SignalName;
      series_pos_peaks.XAxisKey = "DateTime";
      series_neg_peaks.YAxisKey = SignalName;
      series_neg_peaks.XAxisKey = "DateTime";

      // Configure Axes
      DateTimeAxis xAxis = new DateTimeAxis();
      xAxis.Key = "DateTime";
      xAxis.Minimum = DateTimeAxis.ToDouble(ViewStartTime);
      xAxis.Maximum = DateTimeAxis.ToDouble(ViewEndTime);

      LinearAxis yAxis = new LinearAxis();
      yAxis.MajorGridlineStyle = LineStyle.Solid;
      yAxis.MinorGridlineStyle = LineStyle.Dot;
      yAxis.Title = SignalName;
      yAxis.Key = SignalName;

      return new Tuple<LineSeries, ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries, DateTimeAxis, LinearAxis>(series_norm, series_insets, series_onsets, series_neg_peaks, series_pos_peaks, xAxis, yAxis);
    }
    public static void SaveRespiratoryAnalysisToExcel(string fileName, string SignalName, List<string[]> signalProperties, DateTime StartTime, PlotModel plot)
    {
      List<DataPoint> series = ((LineSeries)plot.Series[0]).Points;
      List<ScatterPoint> insets = ((ScatterSeries)plot.Series[1]).Points;
      List<ScatterPoint> onsets = ((ScatterSeries)plot.Series[2]).Points;
      List<ScatterPoint> negpeaks = ((ScatterSeries)plot.Series[3]).Points;
      List<ScatterPoint> pospeaks = ((ScatterSeries)plot.Series[4]).Points;
      object[,] signal_points = new object[series.Count + 1, 7];
      #region Get Points

      int count_in = 0, count_on = 0, count_pos = 0, count_neg = 0;
      signal_points[0, 0] = "Epoch";
      signal_points[0, 1] = "Date Time";
      signal_points[0, 2] = "Value";
      signal_points[0, 3] = "Inspiration";
      signal_points[0, 4] = "Exspiration";
      signal_points[0, 5] = "Neg. Peaks";
      signal_points[0, 6] = "Pos. Peaks";

      for (int x = 1; x < series.Count + 1; x++)
      {
        signal_points[x, 0] = Utils.DateTimetoEpoch(DateTimeAxis.ToDateTime(series[x - 1].X), StartTime);
        signal_points[x, 1] = DateTimeAxis.ToDateTime(series[x - 1].X).ToString("MM/dd/yyyy hh:mm:ss.fff tt");
        signal_points[x, 2] = series[x - 1].Y;

        if (count_in < insets.Count && insets[count_in].X == series[x - 1].X)
        {
          signal_points[x, 3] = series[x - 1].Y;
          count_in++;
        }
        if (count_on < onsets.Count && onsets[count_on].X == series[x - 1].X)
        {
          signal_points[x, 4] = series[x - 1].Y;
          count_on++;
        }
        if (count_neg < negpeaks.Count && negpeaks[count_neg].X == series[x - 1].X)
        {
          signal_points[x, 5] = series[x - 1].Y;
          count_neg++;
        }
        if (count_pos < pospeaks.Count && pospeaks[count_pos].X == series[x - 1].X)
        {
          signal_points[x, 6] = series[x - 1].Y;
          count_pos++;
        }
      }

      #endregion 

      Excel.Application app = new Excel.Application();
      
      Excel.Workbook wb = app.Workbooks.Add(System.Reflection.Missing.Value);
      Excel.Worksheet ws2 = (Excel.Worksheet)wb.Sheets.Add();
      Excel.Worksheet ws1 = (Excel.Worksheet)wb.Sheets.Add();
      
      #region Sheet 1

      ws1.Name = "Analysis";

      ws1.Cells[1, 2].Value = "Signal";
      ws1.Cells[1, 3].Value = SignalName;
      ws1.Cells[1, 2].Font.Bold = true;

      ws1.Cells[3, 2].Value = "Property";
      ws1.Cells[3, 3].Value = "Mean";
      ws1.Cells[3, 4].Value = "% Variance";

      for (int x = 0; x < signalProperties.Count; x++)
      {
        ws1.Cells[4 + x, 2].Value = signalProperties[x][0];
        ws1.Cells[4 + x, 3].Value = signalProperties[x][1];
        ws1.Cells[4 + x, 4].Value = signalProperties[x][2];
      }

      ws1.ListObjects.Add(Excel.XlListObjectSourceType.xlSrcRange, ws1.Range[ws1.Cells[3, 2], ws1.Cells[3 + signalProperties.Count, 4]], System.Reflection.Missing.Value, Excel.XlYesNoGuess.xlGuess, System.Reflection.Missing.Value).Name = "SignalProperties";
      ws1.ListObjects["SignalProperties"].TableStyle = "TableStyleLight9";
      ws1.Columns["A:F"].ColumnWidth = 20;
      ws1.Columns["C:D"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

      #endregion

      #region Sheet 2

      ws2.Name = "SignalValues";

      Excel.Range range = ws2.Range[ws2.Cells[3, 2], ws2.Cells[2 + signal_points.Length / 7, 8]];
      range.Value = signal_points;
      ws2.ListObjects.Add(Excel.XlListObjectSourceType.xlSrcRange, range, System.Reflection.Missing.Value, Excel.XlYesNoGuess.xlGuess, System.Reflection.Missing.Value).Name = "SignalValues";
      ws2.ListObjects["SignalValues"].TableStyle = "TableStyleLight9";
      ws2.Columns["A:I"].ColumnWidth = 20;
      ws2.Columns["E:H"].Hidden = true;
      ws2.Columns["B:H"].HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;

      Excel.Range range2 = ws2.Range[ws2.Cells[4, 2], ws2.Cells[2 + signal_points.Length / 7, 8]];
      range2.FormatConditions.Add(Excel.XlFormatConditionType.xlExpression, System.Reflection.Missing.Value, "=NOT(ISBLANK($E4))", System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value);
      range2.FormatConditions.Add(Excel.XlFormatConditionType.xlExpression, System.Reflection.Missing.Value, "=NOT(ISBLANK($F4))", System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value);
      range2.FormatConditions.Add(Excel.XlFormatConditionType.xlExpression, System.Reflection.Missing.Value, "=NOT(ISBLANK($G4))", System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value);
      range2.FormatConditions.Add(Excel.XlFormatConditionType.xlExpression, System.Reflection.Missing.Value, "=NOT(ISBLANK($H4))", System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value, System.Reflection.Missing.Value);
      range2.FormatConditions[1].Interior.Color = 5296274;
      range2.FormatConditions[2].Interior.Color = 255;
      range2.FormatConditions[3].Interior.Color = 65535;
      range2.FormatConditions[4].Interior.Color = 15773696;

      var excel_chart = ((Excel.ChartObject)((Excel.ChartObjects)ws2.ChartObjects()).Add(500, 100, 900, 500)).Chart;
      excel_chart.SetSourceData(range.Columns["B:G"]);
      excel_chart.ChartType = Microsoft.Office.Interop.Excel.XlChartType.xlXYScatterLines;
      excel_chart.ChartWizard(Source: range.Columns["B:G"], Title: SignalName, CategoryTitle: "Time", ValueTitle: SignalName);
      excel_chart.PlotVisibleOnly = false;
      ((Excel.Series)excel_chart.SeriesCollection(1)).ChartType = Excel.XlChartType.xlXYScatterLinesNoMarkers;
      ((Excel.Series)excel_chart.SeriesCollection(2)).MarkerStyle = Excel.XlMarkerStyle.xlMarkerStyleSquare;
      ((Excel.Series)excel_chart.SeriesCollection(3)).MarkerStyle = Excel.XlMarkerStyle.xlMarkerStyleSquare;
      ((Excel.Series)excel_chart.SeriesCollection(4)).MarkerStyle = Excel.XlMarkerStyle.xlMarkerStyleSquare;
      ((Excel.Series)excel_chart.SeriesCollection(5)).MarkerStyle = Excel.XlMarkerStyle.xlMarkerStyleSquare;
      ((Excel.Series)excel_chart.SeriesCollection(2)).Format.Fill.ForeColor.RGB = 5296274;
      ((Excel.Series)excel_chart.SeriesCollection(3)).Format.Fill.ForeColor.RGB = 255;
      ((Excel.Series)excel_chart.SeriesCollection(4)).Format.Fill.ForeColor.RGB = 65535;
      ((Excel.Series)excel_chart.SeriesCollection(5)).Format.Fill.ForeColor.RGB = 15773696;
      ((Excel.Series)excel_chart.SeriesCollection(2)).Format.Line.ForeColor.RGB = 5296274;
      ((Excel.Series)excel_chart.SeriesCollection(3)).Format.Line.ForeColor.RGB = 255;
      ((Excel.Series)excel_chart.SeriesCollection(4)).Format.Line.ForeColor.RGB = 65535;
      ((Excel.Series)excel_chart.SeriesCollection(5)).Format.Line.ForeColor.RGB = 15773696;

      #endregion

      #region Save and Close
      
      wb.SaveAs(fileName);

      wb.Close(true);
      app.Quit();

      System.Runtime.InteropServices.Marshal.ReleaseComObject(range);
      System.Runtime.InteropServices.Marshal.ReleaseComObject(ws2);
      System.Runtime.InteropServices.Marshal.ReleaseComObject(wb);
      System.Runtime.InteropServices.Marshal.ReleaseComObject(app);

      #endregion
    }
    public static Tuple<double, double> GetRespiratorySignalBreathingPeriod(ScatterSeries[] series)
    {
      // Find Breathing Rates
      List<double> breathing_periods = new List<double>();
      for (int x = 0; x < series.Length; x++)
      {
        for (int y = 1; y < series[x].Points.Count; y++)
          breathing_periods.Add((DateTimeAxis.ToDateTime(series[x].Points[y].X) - DateTimeAxis.ToDateTime(series[x].Points[y - 1].X)).TotalSeconds);
      }

      if (breathing_periods.Count != 0) // Non-Zero Breathing Rates
      {
        // Calculate Mean 
        double mean = breathing_periods.Average();

        // Calculate Variance
        double coeff_variance = GetVarianceCoefficient(breathing_periods.ToArray());

        return new Tuple<double, double>(mean, coeff_variance);
      }
      else
      {
        return new Tuple<double, double>(0, 0);
      }
    }
    public static Tuple<double, double> GetRespiratorySignalBreathingHalfPeriod(ScatterSeries series_1, ScatterSeries series_2)
    {
      if (series_1.Points.Count > 0 && series_2.Points.Count > 0)
      {
        int index_1 = 0;
        int index_2;
        if (DateTimeAxis.ToDateTime(series_1.Points[0].X) < DateTimeAxis.ToDateTime(series_2.Points[0].X))
          index_2 = 0;
        else
          index_2 = 1;

        List<double> half_periods = new List<double>();
        while (index_2 < series_2.Points.Count && index_1 < series_1.Points.Count)
        {
          half_periods.Add((DateTimeAxis.ToDateTime(series_2.Points[index_2].X) - DateTimeAxis.ToDateTime(series_1.Points[index_1].X)).TotalSeconds);

          index_1++;
          index_2++;
        }

        // Calculate Mean 
        double mean = half_periods.Average();

        // Calculate Variance
        double coeff_variance = GetVarianceCoefficient(half_periods.ToArray());

        return new Tuple<double, double>(mean, coeff_variance);
      }
      else
      {
        return new Tuple<double, double>(0, 0);
      }
    }
    public static Tuple<double, double> GetRespiratorySignalPeakHeight(ScatterSeries series_peaks)
    {
      List<double> peak_heights = series_peaks.Points.Select(temp => temp.Y).ToList();
      if (peak_heights.Count != 0)
      {
        // Calculate Mean 
        double mean = peak_heights.Average();

        // Calculate Variance
        double coeff_variance = GetVarianceCoefficient(peak_heights.ToArray());

        return new Tuple<double, double>(mean, coeff_variance);
      }
      else
      {
        return new Tuple<double, double>(0, 0);
      }
    }
    public static Tuple<double, double> GetRespiratorySignalFlowVolume(LineSeries series, ScatterSeries series_1, ScatterSeries series_2, float sample_period)
    {
      if (series_1.Points.Count > 0 && series_2.Points.Count > 0)
      {
        int index_1 = 0;
        int index_2;
        if (DateTimeAxis.ToDateTime(series_1.Points[0].X) < DateTimeAxis.ToDateTime(series_2.Points[0].X))
          index_2 = 0;
        else
          index_2 = 1;

        List<double> integral_sums = new List<double>();
        while (index_2 < series_2.Points.Count && index_1 < series_1.Points.Count)
        {
          DateTime EndTime = DateTimeAxis.ToDateTime(series_2.Points[index_2].X);
          DateTime StartTime = DateTimeAxis.ToDateTime(series_1.Points[index_1].X);

          double integral_sum = 0;
          int start_index = series.Points.IndexOf(series.Points.Find(temp => temp.X == DateTimeAxis.ToDouble(StartTime)));
          int end_index = series.Points.IndexOf(series.Points.Find(temp => temp.X == DateTimeAxis.ToDouble(EndTime)));
          for (int x = start_index; x <= end_index; x++)
          {
            integral_sum += series.Points[x].Y * sample_period;
          }
          integral_sums.Add(integral_sum);

          index_1++;
          index_2++;
        }

        // Calculate Mean 
        double mean = integral_sums.Average();

        // Calculate Variance
        double coeff_variance = GetVarianceCoefficient(integral_sums.ToArray());

        return new Tuple<double, double>(mean, coeff_variance);
      }
      else
      {
        return new Tuple<double, double>(0, 0);
      }
    }
    public static double[] GetRespAnalysisInfo(PlotModel model, DateTime start, float sample_period)
    {
      DateTime end = start + Utils.EpochPeriodtoTimeSpan(1);
      
      List<Series> series = new List<Series>();
      series.Add(new LineSeries());
      for (int y = 0; y < ((LineSeries)model.Series[0]).Points.Count; y++)
      {
        if (((LineSeries)model.Series[0]).Points[y].X >= DateTimeAxis.ToDouble(start))
        {
          if (((LineSeries)model.Series[0]).Points[y].X <= DateTimeAxis.ToDouble(end))
          {
            ((LineSeries)series[0]).Points.Add(((LineSeries)model.Series[0]).Points[y]);
          }
        }
      }
      for (int x = 1; x < model.Series.Count; x++)
      {
        series.Add(new ScatterSeries());
        for (int y = 0; y < ((ScatterSeries)model.Series[x]).Points.Count; y++)
        {
          if (((ScatterSeries)model.Series[x]).Points[y].X >= DateTimeAxis.ToDouble(start))
          {
            if (((ScatterSeries)model.Series[x]).Points[y].X <= DateTimeAxis.ToDouble(end))
            {
              ((ScatterSeries)series[x]).Points.Add(((ScatterSeries)model.Series[x]).Points[y]);
            }
          }
        }
      }
      

      double[] output = new double[14];

      Tuple<double, double> breathing_periods = RespiratoryFactory.GetRespiratorySignalBreathingPeriod(new ScatterSeries[] { (ScatterSeries)series[1], (ScatterSeries)series[2] });
      output[0] = breathing_periods.Item1;
      output[1] = breathing_periods.Item2;

      Tuple<double, double> neg_peaks = RespiratoryFactory.GetRespiratorySignalPeakHeight((ScatterSeries)series[3]);
      output[2] = neg_peaks.Item1;
      output[3] = neg_peaks.Item2;

      Tuple<double, double> pos_peaks = RespiratoryFactory.GetRespiratorySignalPeakHeight((ScatterSeries)series[4]);
      output[4] = pos_peaks.Item1;
      output[5] = pos_peaks.Item2;

      Tuple<double, double> inspir_periods = RespiratoryFactory.GetRespiratorySignalBreathingHalfPeriod((ScatterSeries)series[2], (ScatterSeries)series[1]);
      output[6] = inspir_periods.Item1;
      output[7] = inspir_periods.Item2;

      Tuple<double, double> exspir_periods = RespiratoryFactory.GetRespiratorySignalBreathingHalfPeriod((ScatterSeries)series[1], (ScatterSeries)series[2]);
      output[8] = exspir_periods.Item1;
      output[9] = exspir_periods.Item2;

      Tuple<double, double> inspir_volume = RespiratoryFactory.GetRespiratorySignalFlowVolume((LineSeries)series[0], (ScatterSeries)series[2], (ScatterSeries)series[1], sample_period);
      output[10] = inspir_volume.Item1;
      output[11] = inspir_volume.Item2;

      Tuple<double, double> exspir_volume = RespiratoryFactory.GetRespiratorySignalFlowVolume((LineSeries)series[0], (ScatterSeries)series[1], (ScatterSeries)series[2], sample_period);
      output[12] = exspir_volume.Item1;
      output[13] = exspir_volume.Item2;

      return output;
    }
    #endregion
  }

  /// <summary>
  /// Model for variables used exclusively in the 'Respiratory' sub tab of the 'Analysis' tab
  /// </summary>
  public class RespiratoryModel
  {
    #region Members

    // EDF Signal Selection

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

    // Binary Signal Selection

    /// <summary>
    /// The user selected start time for the respiratory analysis in 30s epochs
    /// </summary>
    internal int? RespiratoryBinaryStart;
    /// <summary>
    /// The user selected period for the respiratory analysis in 30s epochs
    /// </summary>
    internal int? RespiratoryBinaryDuration;

    // Output Plot

    /// <summary>
    /// The respiratory analysis plot to be displayed
    /// </summary>
    public PlotModel RespiratorySignalPlot = null;

    // Output Analysis
    /// <summary>
    /// Epoch to provide detailed analysis on
    /// </summary>
    public string RespiratoryAnalysisSelectedEpoch;
    
    /// <summary>
    /// The calculated mean average of the periods of the respiratory signal
    /// </summary>
    public string RespiratoryBreathingPeriodMean;
    /// <summary>
    /// The calculated coefficient of variance of the periods of the respiratory signal
    /// </summary>
    public string RespiratoryBreathingPeriodCoeffVar;

    /// <summary>
    /// The calculated mean average of the inspiration periods of the respiratory signal
    /// </summary>
    public string RespiratoryInspirationPeriodMean;
    /// <summary>
    /// The calculated coefficient of variance of the inspiration periods of the respiratory signal
    /// </summary>
    public string RespiratoryInspirationPeriodCoeffVar;

    /// <summary>
    /// The calculated mean average of the exspiration periods of the respiratory signal
    /// </summary>
    public string RespiratoryExspirationPeriodMean;
    /// <summary>
    /// The calculated coefficient of variance of the exspiration periods of the respiratory signal
    /// </summary>
    public string RespiratoryExspirationPeriodCoeffVar;

    /// <summary>
    /// The calculated mean average of the positive peaks of the respiratory signal
    /// </summary>
    public string RespiratoryPositivePeakMean;
    /// <summary>
    /// The calculated coefficient of variance of the positive peaks of the respiratory signal
    /// </summary>
    public string RespiratoryPositivePeakCoeffVar;

    /// <summary>
    /// The calculated mean average of the negative peaks of the respiratory signal
    /// </summary>
    public string RespiratoryNegativePeakMean;
    /// <summary>
    /// The calculated coefficient of variance of the negative peaks of the respiratory signal
    /// </summary>
    public string RespiratoryNegativePeakCoeffVar;

    /// <summary>
    /// The calculated mean average of the signal integral of the positive peaks of the respiratory signal
    /// </summary>
    public string RespiratoryExpirationVolumeMean;
    /// <summary>
    /// The calculated coefficient of variance of the signal integral of the positive peaks of the respiratory signal
    /// </summary>
    public string RespiratoryExpirationVolumeCoeffVar;

    /// <summary>
    /// The calculated mean average of the signal integral of the negative peaks of the respiratory signal
    /// </summary>
    public string RespiratoryInspirationVolumeMean;
    /// <summary>
    /// The calculated coefficient of variance of the signal integral of the negative peaks of the respiratory signal
    /// </summary>
    public string RespiratoryInspirationVolumeCoeffVar;

    // Settings and Options

    /// <summary>
    /// A user selected option for setting the sensitivity of the peak detection of the analysis
    /// Effect where the insets, onsets, and peaks are detected
    /// Any "spike" that is less wide than the user setting in ms will be ignored
    /// </summary>
    public int RespiratoryMinimumPeakWidth = 500;
    /// <summary>
    /// If true, use a constant axis
    /// If false, auto adjust to plot
    /// </summary>
    public bool RespiratoryUseConstantAxis = false;
    /// <summary>
    /// If true, the analysis was performed on a binary file
    /// If false, the analysis was performed on an EDF file
    /// </summary>
    public bool IsAnalysisFromBinary = false;

    // Freeze UI when Performing Analysis 

    /// <summary>
    /// True if the program is performing analysis and a progress ring should be shown
    /// </summary>
    public bool RespiratoryProgressRingEnabled = false;
    internal PlotModel RespiratoryPropertiesSignalPlot;

    #endregion
  }

  /// <summary>
  /// ModelView containing UI logic used exclusively in the 'Respiratory' sub tab of the 'Analysis' tab
  /// </summary>
  public class RespiratoryModelView : INotifyPropertyChanged
  {
    #region Shared Properties and Functions

    private SettingsModelView svm;
    private SettingsModel sm
    {
      get
      {
        return svm.sm;
      }
    }

    // Property Changed Listener
    private void Exterior_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
      switch (e.PropertyName)
      {
        case nameof(IsEDFLoaded):
          if (!IsEDFLoaded)
          {
            RespiratoryBreathingPeriodMean = "";
            RespiratoryBreathingPeriodCoeffVar = "";
            RespiratorySignalPlot = null;
            RespiratoryEDFSelectedSignal = null;
            RespiratoryEDFDuration = null;
            RespiratoryEDFStartRecord = null;
          }
          else
          {
            RespiratoryBreathingPeriodMean = "";
            RespiratoryBreathingPeriodCoeffVar = "";
            RespiratoryEDFSelectedSignal = null;
            RespiratorySignalPlot = null;
            RespiratoryEDFStartRecord = 1;
            RespiratoryEDFDuration = 1;
          }
          RespiratoryEDFView_Changed();
          OnPropertyChanged(nameof(RespiratoryEDFNavigationEnabled));
          OnPropertyChanged(nameof(IsEDFLoaded));
          break;
        default:
          OnPropertyChanged(e.PropertyName);
          break;
      }
    }
    
    // Shared Properties
    public EDFFile LoadedEDFFile
    {
      get
      {
        return svm.LoadedEDFFile;
      }
    }
    public DateTime EDFStartTime
    {
      get
      {
        if (IsEDFLoaded)
          return LoadedEDFFile.Header.StartDateTime;
        else
          return new DateTime();
      }
    }
    public DateTime EDFEndTime
    {
      get
      {
        if (IsEDFLoaded)
        {
          DateTime EndTime = LoadedEDFFile.Header.StartDateTime
                             + new TimeSpan(
                               (long)(TimeSpan.TicksPerSecond * LoadedEDFFile.Header.DurationOfDataRecordInSeconds * LoadedEDFFile.Header.NumberOfDataRecords)
                               );
          return EndTime;
        }
        else
          return new DateTime();
      }
    }

    public bool IsEDFLoaded
    {
      get
      {
        return svm.IsEDFLoaded;
      }
    }
    public ReadOnlyCollection<string> AllNonHiddenSignals
    {
      get
      {
        return svm.AllNonHiddenSignals;
      }
    }

    public bool UseDarkTheme
    {
      get
      {
        return sm.UseDarkTheme;
      }
    }

    // Shared Functions
    public LineSeries GetSeriesFromSignalName(out float sample_period, string Signal, DateTime StartTime, DateTime EndTime)
    {
      return svm.GetSeriesFromSignalName(out sample_period, Signal, StartTime, EndTime);
    }
    
    #endregion
    
    /// <summary>
    /// Respiratory Model
    /// </summary>
    private RespiratoryModel rm = new RespiratoryModel();
    
    #region Properties

    // Property Changed Functions
    private void RepiratoryPlot_Changed()
    {
      RespiratoryProgressRingEnabled = false;
    }
    private void RespiratoryEDFView_Changed()
    {
      OnPropertyChanged(nameof(RespiratoryEDFStartRecord));
      OnPropertyChanged(nameof(RespiratoryEDFStartTime));
      OnPropertyChanged(nameof(RespiratoryEDFDuration));

      OnPropertyChanged(nameof(RespiratoryEDFStartRecordMax));
      OnPropertyChanged(nameof(RespiratoryEDFStartRecordMin));
      OnPropertyChanged(nameof(RespiratoryEDFDurationMax));
      OnPropertyChanged(nameof(RespiratoryEDFDurationMin));
    }
    private void RespiratoryBinaryView_Changed()
    {
      OnPropertyChanged(nameof(RespiratoryBinaryStart));
      OnPropertyChanged(nameof(RespiratoryBinaryDuration));

      OnPropertyChanged(nameof(RespiratoryBinaryStartRecordMax));
      OnPropertyChanged(nameof(RespiratoryBinaryDurationMax));
    }
    private void RespiratoryAnalysisSelectedEpoch_Changed()
    {
      UpdateRespAnalysisInfo();
    }

    // Settings and Options
    public bool SettingsRespiratoryVisible
    {
      get
      {
        return sm.SettingsRespiratoryVisible;
      }
      set
      {
        sm.SettingsRespiratoryVisible = value;
        OnPropertyChanged(nameof(SettingsRespiratoryVisible));
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
      }
    }
    public bool RespiratoryUseConstantAxis
    {
      get
      {
        return rm.RespiratoryUseConstantAxis;
      }
      set
      {
        rm.RespiratoryUseConstantAxis = value;
        OnPropertyChanged(nameof(RespiratoryUseConstantAxis));
        PerformRespiratoryAnalysisEDF();
      }
    }

    // EDF Signal Selection
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
        PerformRespiratoryAnalysisEDF();
      }
    }
    public int? RespiratoryEDFStartRecord
    {
      get
      {
        if (IsEDFLoaded)
          return rm.RespiratoryEDFStartRecord;
        else
          return null;
      }
      set
      {
        if (IsEDFLoaded && rm.RespiratoryEDFStartRecord != (value ?? 1))
        {
          rm.RespiratoryEDFStartRecord = value ?? 1;
          OnPropertyChanged(nameof(RespiratoryEDFStartRecord));
          RespiratoryEDFView_Changed();
          PerformRespiratoryAnalysisEDF();
        }
      }
    }
    public int? RespiratoryEDFDuration
    {
      get
      {
        if (IsEDFLoaded)
          return rm.RespiratoryEDFDuration;
        else
          return null;
      }
      set
      {
        if (IsEDFLoaded && rm.RespiratoryEDFDuration != (value ?? 1))
        {
          rm.RespiratoryEDFDuration = value ?? 1;
          OnPropertyChanged(nameof(RespiratoryEDFDuration));
          RespiratoryEDFView_Changed();
          PerformRespiratoryAnalysisEDF();
        }
      }
    }

    // Binary Signal Selection
    public int? RespiratoryBinaryStart
    {
      get
      {
        return rm.RespiratoryBinaryStart;
      }
      set
      {
        if (IsRespBinLoaded && rm.RespiratoryBinaryStart != (value ?? 1))
        {
          rm.RespiratoryBinaryStart = value ?? 1;
          OnPropertyChanged(nameof(RespiratoryBinaryStart));
          RespiratoryBinaryView_Changed();
          PerformRespiratoryAnalysisBinary();
        }
      }
    }
    public int? RespiratoryBinaryDuration
    {
      get
      {
        return rm.RespiratoryBinaryDuration;
      }
      set
      {
        if (IsRespBinLoaded && rm.RespiratoryBinaryDuration != (value ?? 1))
        {
          rm.RespiratoryBinaryDuration = value ?? 1;
          OnPropertyChanged(nameof(RespiratoryBinaryDuration));
          RespiratoryBinaryView_Changed();
          PerformRespiratoryAnalysisBinary();
        }
      }
    }

    // Output Plot
    public bool IsAnalysisFromBinary
    {
      get
      {
        return rm.IsAnalysisFromBinary;
      }
      set
      {
        rm.IsAnalysisFromBinary = value;
      }
    }
    public bool RespiratorySignalPlotExists
    {
      get
      {
        return RespiratorySignalPlot != null;
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
        Utils.ApplyThemeToPlot(value, UseDarkTheme);
        rm.RespiratorySignalPlot = value;
        OnPropertyChanged(nameof(RespiratorySignalPlot));
        OnPropertyChanged(nameof(RespiratorySignalPlotExists));
        RepiratoryPlot_Changed();
      }
    }

    // Output Analysis
    public string[] RespiratoryAnalyzedEpochs
    {
      get
      {
        if (!RespiratorySignalPlotExists)
          return null;
        else
        {
          if (IsAnalysisFromBinary)
          {
            List<string> return_value = new List<string>();
            for (int x = 1; x <= resp_bin_max_epoch; x++)
              return_value.Add(x.ToString());
            return return_value.ToArray();
          }
          else
          {
            List<string> return_value = new List<string>();
            for (int x = 1; x <= RespiratoryEDFStartRecordMax; x++)
              return_value.Add(x.ToString());
            return return_value.ToArray();
          }
        }
      }
    }
    public string RespiratoryAnalysisSelectedEpoch
    {
      get
      {
        return rm.RespiratoryAnalysisSelectedEpoch;
      }
      set
      {
        rm.RespiratoryAnalysisSelectedEpoch = value;
        OnPropertyChanged(nameof(RespiratoryAnalysisSelectedEpoch));
        RespiratoryAnalysisSelectedEpoch_Changed();
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
    public string RespiratoryBreathingPeriodCoeffVar
    {
      get
      {
        return rm.RespiratoryBreathingPeriodCoeffVar;
      }
      set
      {
        rm.RespiratoryBreathingPeriodCoeffVar = value;
        OnPropertyChanged(nameof(RespiratoryBreathingPeriodCoeffVar));
      }
    }
    public string RespiratoryInspirationPeriodMean
    {
      get
      {
        return rm.RespiratoryInspirationPeriodMean;
      }
      set
      {
        rm.RespiratoryInspirationPeriodMean = value;
        OnPropertyChanged(nameof(RespiratoryInspirationPeriodMean));
      }
    }
    public string RespiratoryInspirationPeriodCoeffVar
    {
      get
      {
        return rm.RespiratoryInspirationPeriodCoeffVar;
      }
      set
      {
        rm.RespiratoryInspirationPeriodCoeffVar = value;
        OnPropertyChanged(nameof(RespiratoryInspirationPeriodCoeffVar));
      }
    }
    public string RespiratoryExspirationPeriodMean
    {
      get
      {
        return rm.RespiratoryExspirationPeriodMean;
      }
      set
      {
        rm.RespiratoryExspirationPeriodMean = value;
        OnPropertyChanged(nameof(RespiratoryExspirationPeriodMean));
      }
    }
    public string RespiratoryExspirationPeriodCoeffVar
    {
      get
      {
        return rm.RespiratoryExspirationPeriodCoeffVar;
      }
      set
      {
        rm.RespiratoryExspirationPeriodCoeffVar = value;
        OnPropertyChanged(nameof(RespiratoryExspirationPeriodCoeffVar));
      }
    }
    public string RespiratoryPositivePeakMean
    {
      get
      {
        return rm.RespiratoryPositivePeakMean;
      }
      set
      {
        rm.RespiratoryPositivePeakMean = value;
        OnPropertyChanged(nameof(RespiratoryPositivePeakMean));
      }
    }
    public string RespiratoryPositivePeakCoeffVar
    {
      get
      {
        return rm.RespiratoryPositivePeakCoeffVar;
      }
      set
      {
        rm.RespiratoryPositivePeakCoeffVar = value;
        OnPropertyChanged(nameof(RespiratoryPositivePeakCoeffVar));
      }
    }
    public string RespiratoryNegativePeakMean
    {
      get
      {
        return rm.RespiratoryNegativePeakMean;
      }
      set
      {
        rm.RespiratoryNegativePeakMean = value;
        OnPropertyChanged(nameof(RespiratoryNegativePeakMean));
      }
    }
    public string RespiratoryNegativePeakCoeffVar
    {
      get
      {
        return rm.RespiratoryNegativePeakCoeffVar;
      }
      set
      {
        rm.RespiratoryNegativePeakCoeffVar = value;
        OnPropertyChanged(nameof(RespiratoryNegativePeakCoeffVar));
      }
    }
    public string RespiratoryExpirationVolumeMean
    {
      get
      {
        return rm.RespiratoryExpirationVolumeMean;
      }
      set
      {
        rm.RespiratoryExpirationVolumeMean = value;
        OnPropertyChanged(nameof(RespiratoryExpirationVolumeMean));
      }
    }
    public string RespiratoryExpirationVolumeCoeffVar
    {
      get
      {
        return rm.RespiratoryExpirationVolumeCoeffVar;
      }
      set
      {
        rm.RespiratoryExpirationVolumeCoeffVar = value;
        OnPropertyChanged(nameof(RespiratoryExpirationVolumeCoeffVar));
      }
    }
    public string RespiratoryInspirationVolumeMean
    {
      get
      {
        return rm.RespiratoryInspirationVolumeMean;
      }
      set
      {
        rm.RespiratoryInspirationVolumeMean = value;
        OnPropertyChanged(nameof(RespiratoryInspirationVolumeMean));
      }
    }
    public string RespiratoryInspirationVolumeCoeffVar
    {
      get
      {
        return rm.RespiratoryInspirationVolumeCoeffVar;
      }
      set
      {
        rm.RespiratoryInspirationVolumeCoeffVar = value;
        OnPropertyChanged(nameof(RespiratoryInspirationVolumeCoeffVar));
      }
    }
    public PlotModel RespiratoryPropertiesSignalPlot
    {
      get
      {
        return rm.RespiratoryPropertiesSignalPlot;
      }
      set
      {
        Utils.ApplyThemeToPlot(value, UseDarkTheme);
        rm.RespiratoryPropertiesSignalPlot = value;
        OnPropertyChanged(nameof(RespiratoryPropertiesSignalPlot));
      }
    }

    // Bounds on the EDF Signal Selection
    public DateTime RespiratoryEDFStartTime
    {
      get
      {
        if (LoadedEDFFile != null)
          return Utils.EpochtoDateTime(RespiratoryEDFStartRecord ?? 1, LoadedEDFFile);
        else
          return new DateTime();
      }
    }
    public DateTime RespiratoryEDFStartTimeMax
    {
      get
      {
        if (IsEDFLoaded)
        {
          DateTime EndTime = EDFEndTime; // EDF End Time
          TimeSpan duration = Utils.EpochPeriodtoTimeSpan(RespiratoryEDFDuration ?? 1); // User Selected Duration 
          return EndTime - duration;
        }
        else
          return new DateTime();
      }
    }
    public DateTime RespiratoryEDFStartTimeMin
    {
      get
      {
        if (LoadedEDFFile != null)
          return LoadedEDFFile.Header.StartDateTime; // Start Time
        else
          return new DateTime();
      }
    }
    public int RespiratoryEDFStartRecordMax
    {
      get
      {
        if (LoadedEDFFile != null)
          return Utils.DateTimetoEpoch(RespiratoryEDFStartTimeMax, LoadedEDFFile); // RespiratoryViewStartTimeMax to Record
        else
          return 0;
      }
    }
    public int RespiratoryEDFStartRecordMin
    {
      get
      {
        if (LoadedEDFFile != null)
          return Utils.DateTimetoEpoch(RespiratoryEDFStartTimeMin, LoadedEDFFile); // RespiratoryViewStartTimeMax to Record
        else
          return 0;
      }
    }
    public int RespiratoryEDFDurationMax
    {
      get
      {
        if (IsEDFLoaded) // File Loaded
        {
          DateTime EndTime = EDFEndTime; // EDF End Time
          TimeSpan duration = EndTime - (RespiratoryEDFStartTime); // Theoretical Limit Duration
          TimeSpan limit = new TimeSpan(TimeSpan.TicksPerHour * 2); // Practical Limit Duration

          return Math.Min(
              Utils.TimeSpantoEpochPeriod(limit),
              Utils.TimeSpantoEpochPeriod(duration)
              );
        }
        else // No File Loaded
          return 0;
      }
    }
    public int RespiratoryEDFDurationMin
    {
      get
      {
        if (LoadedEDFFile != null) // File Loaded
          return 1;
        else // No File Loaded
          return 0;
      }
    }

    // Bounds on the Binary Signal Selection
    public int RespiratoryBinaryStartRecordMax
    {
      get
      {
        return 1 + resp_bin_max_epoch - RespiratoryBinaryDuration ?? 1;
      }
    }
    public int RespiratoryBinaryDurationMax
    {
      get
      {
        return 1 + resp_bin_max_epoch - RespiratoryBinaryStart ?? 1;
      }
    }
    public int RespiratoryBinaryMaxEpochs
    {
      get
      {
        return resp_bin_max_epoch;
      }
    }

    // Freeze UI when Performing Analysis 
    public bool RespiratoryProgressRingEnabled
    {
      get
      {
        return rm.RespiratoryProgressRingEnabled;
      }
      set
      {
        rm.RespiratoryProgressRingEnabled = value;
        OnPropertyChanged(nameof(RespiratoryProgressRingEnabled));
        OnPropertyChanged(nameof(RespiratoryEDFNavigationEnabled));
      }
    }
    public bool RespiratoryEDFNavigationEnabled
    {
      get
      {
        if (!IsEDFLoaded)
          return false;
        else
          return !RespiratoryProgressRingEnabled;
      }
    }

    // Used For Importing From Binary For Respiratory Signals
    public bool IsRespBinLoaded
    {
      get
      {
        return resp_bin_loaded;
      }
      set
      {
        resp_bin_loaded = value;
        OnPropertyChanged(nameof(IsRespBinLoaded));
      }
    }

    public bool resp_bin_loaded = false;
    private string resp_bin_sample_frequency_s;
    private string resp_bin_date_time_length;
    private string resp_bin_date_time_from;
    private string resp_bin_subject_id;
    private string resp_bin_signal_name;
    private float resp_bin_sample_period;
    private List<float> resp_signal_values;
    private int resp_bin_max_epoch;

    #endregion

    #region Actions

    // Exporting Respiratory Analysis to Excel 
    /// <summary>
    /// Background process for exporting respiratory analysis
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public void BW_ExportAnalysis_DoWork(object sender, DoWorkEventArgs e)
    {
      string SignalName = IsAnalysisFromBinary ? resp_bin_signal_name : RespiratoryEDFSelectedSignal;
      DateTime StartTime = IsAnalysisFromBinary ? DateTime.Parse(resp_bin_date_time_from) : EDFStartTime;
      List<string[]> properties = new List<string[]>();
      properties.Add(new string[] { "Breathing Period", RespiratoryBreathingPeriodMean, RespiratoryBreathingPeriodCoeffVar });
      properties.Add(new string[] { "Inspiration Period", RespiratoryInspirationPeriodMean, RespiratoryInspirationPeriodCoeffVar });
      properties.Add(new string[] { "Exspiration Period", RespiratoryExspirationPeriodMean, RespiratoryExspirationPeriodCoeffVar });
      properties.Add(new string[] { "Positive Peak", RespiratoryPositivePeakMean, RespiratoryPositivePeakCoeffVar });
      properties.Add(new string[] { "Negative Peak", RespiratoryNegativePeakMean, RespiratoryNegativePeakCoeffVar });
      properties.Add(new string[] { "Inspiration Volume", RespiratoryInspirationVolumeMean, RespiratoryInspirationVolumeCoeffVar });
      properties.Add(new string[] { "Exspiration Volume", RespiratoryExpirationVolumeMean, RespiratoryExpirationVolumeCoeffVar });

      RespiratoryFactory.SaveRespiratoryAnalysisToExcel(e.Argument.ToString(), SignalName, properties, StartTime, RespiratorySignalPlot);
    }
    /// <summary>
    /// Called when exporting respiratory analysis finishes
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BW__ExportAnalysis_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
    {
      RespiratoryProgressRingEnabled = false;
    }
    /// <summary>
    /// Exports Respiratory Calculation to Excel file
    /// </summary>
    /// <param name="fileName"> The filename of the excel file to be created </param>
    /// <returns></returns>
    public void ExportRespiratoryCalculations(string fileName)
    {
      RespiratoryProgressRingEnabled = true;

      BackgroundWorker bw = new BackgroundWorker();
      bw.DoWork += BW_ExportAnalysis_DoWork;
      bw.RunWorkerCompleted += BW__ExportAnalysis_RunWorkerCompleted;
      bw.RunWorkerAsync(fileName);
    }
    
    // Respiratory Analysis From Binary File
    /// <summary>
    /// Loads a binary file's contents into memory 
    /// </summary>
    public void LoadRespiratoryAnalysisBinary()
    {
      IsRespBinLoaded = true;
      System.Windows.Forms.OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();

      dialog.Filter = "|*.bin";

      if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
      {
        // select the binary file
        FileStream bin_file = new FileStream(dialog.FileName, FileMode.Open);
        BinaryReader reader = new BinaryReader(bin_file);

        byte[] value = new byte[4];
        bool didReachEnd = false;
        this.resp_signal_values = new List<float>();
        // read the whole binary file and build the signal values
        while (reader.BaseStream.Position != reader.BaseStream.Length)
        {
          try
          {
            value = reader.ReadBytes(4);
            float myFloat = System.BitConverter.ToSingle(value, 0);
            resp_signal_values.Add(myFloat);
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
        this.resp_bin_signal_name = file_reader.ReadLine();
        this.resp_bin_subject_id = file_reader.ReadLine();
        this.resp_bin_date_time_from = file_reader.ReadLine();
        this.resp_bin_date_time_length = file_reader.ReadLine();
        this.resp_bin_sample_frequency_s = file_reader.ReadLine();

        bin_file.Close();

        this.resp_bin_sample_period = 1 / float.Parse(resp_bin_sample_frequency_s);

        DateTime epochs_from_datetime = DateTime.Parse(resp_bin_date_time_from);
        DateTime epochs_to_datetime = DateTime.Parse(resp_bin_date_time_length);

        resp_bin_max_epoch = (int)epochs_to_datetime.Subtract(epochs_from_datetime).TotalSeconds / 30;
        OnPropertyChanged(nameof(RespiratoryBinaryMaxEpochs));
        rm.RespiratoryBinaryStart = 1;
        OnPropertyChanged(nameof(RespiratoryBinaryStart));
        rm.RespiratoryBinaryDuration = 1;
        OnPropertyChanged(nameof(RespiratoryBinaryDuration));
        OnPropertyChanged(nameof(RespiratoryBinaryDurationMax));
        OnPropertyChanged(nameof(RespiratoryBinaryStartRecordMax));

        PerformRespiratoryAnalysisBinary();
        UpdateRespAnalysisInfoPlot();
      }
      else
      {
      }
    }
    /// <summary>
    /// Performs respiratory analysis on binary contents stored into memory 
    /// </summary>
    public void PerformRespiratoryAnalysisBinary()
    {
      bool updatePropertiesPlot = !IsAnalysisFromBinary;
      
      IsAnalysisFromBinary = true;
      RespiratoryProgressRingEnabled = true;

      // Finding From 
      int modelStartRecord = RespiratoryBinaryStart.Value;
      DateTime newFrom = DateTime.Parse(resp_bin_date_time_from);
      newFrom = newFrom.AddSeconds(30 * (modelStartRecord - 1));

      // Finding To 
      int modelLength = rm.RespiratoryBinaryDuration.Value;
      DateTime newTo = newFrom;
      newTo = newTo.AddSeconds(30 * (modelLength));

      if (newFrom < DateTime.Parse(resp_bin_date_time_from))
        newFrom = DateTime.Parse(resp_bin_date_time_from);
      if (newTo < newFrom)
        newTo = newFrom;

      int start_index = (int)(((double)(newFrom - DateTime.Parse(resp_bin_date_time_from)).TotalSeconds) / ((double)resp_bin_sample_period));
      int end_index = (int)(((double)(newTo - DateTime.Parse(resp_bin_date_time_from)).TotalSeconds) / ((double)resp_bin_sample_period));
      start_index = Math.Max(start_index, 0);
      end_index = Math.Min(end_index, resp_signal_values.Count - 1);

      Tuple<LineSeries, ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries, DateTimeAxis, LinearAxis> resp_plots = RespiratoryFactory.GetRespiratoryAnalysisPlot(
        resp_bin_signal_name,
        resp_signal_values.GetRange(start_index, end_index - start_index + 1),
        resp_bin_sample_period,
        resp_signal_values.Average(),
        true,
        RespiratoryMinimumPeakWidth,
        newFrom,
        newTo
      );

      UpdateRespAnalysisPlot(resp_plots, resp_bin_sample_period, modelStartRecord, modelStartRecord + modelLength - 1);

      if (updatePropertiesPlot)
        UpdateRespAnalysisInfoPlot();
    }

    // Respiratory Analysis From EDF File
    /// <summary>
    /// To know whether the signal is changing
    /// </summary>
    private string old_signal_name = null;
    /// <summary>
    /// Background process for performing respiratory analysis
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void BW_RespiratoryAnalysisEDF(object sender, DoWorkEventArgs e)
    {
      bool updatePropertiesPlot = IsAnalysisFromBinary || RespiratoryEDFSelectedSignal != old_signal_name;
      old_signal_name = RespiratoryEDFSelectedSignal;

      IsAnalysisFromBinary = false;

      PlotModel temp_SignalPlot = new PlotModel();

      temp_SignalPlot.Series.Clear();
      temp_SignalPlot.Axes.Clear();

      float sample_period;
      LineSeries series = GetSeriesFromSignalName(out sample_period,
                                                  RespiratoryEDFSelectedSignal,
                                                  Utils.EpochtoDateTime(RespiratoryEDFStartRecord ?? 1, LoadedEDFFile),
                                                  Utils.EpochtoDateTime(RespiratoryEDFStartRecord ?? 1, LoadedEDFFile) + Utils.EpochPeriodtoTimeSpan(RespiratoryEDFDuration ?? 1)
                                                  );


      PlotModel tempPlotModel = new PlotModel();
      Tuple<LineSeries, ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries, DateTimeAxis, LinearAxis> resp_plots = RespiratoryFactory.GetRespiratoryAnalysisPlot(
        RespiratoryEDFSelectedSignal,
        series.Points.Select(temp => (float)temp.Y).ToList(),
        sample_period,
        (float)(Utils.GetMaxSignalValue(RespiratoryEDFSelectedSignal, false, LoadedEDFFile, sm) - Utils.GetMaxSignalValue(RespiratoryEDFSelectedSignal, true, LoadedEDFFile, sm)),
        true,
        RespiratoryMinimumPeakWidth,
        Utils.EpochtoDateTime(RespiratoryEDFStartRecord ?? 1, LoadedEDFFile),
        Utils.EpochtoDateTime(RespiratoryEDFStartRecord ?? 1, LoadedEDFFile) + Utils.EpochPeriodtoTimeSpan(RespiratoryEDFDuration ?? 1)
        );

      if (RespiratoryUseConstantAxis)
      {
        resp_plots.Item7.Minimum = Utils.GetMinSignalValue(RespiratoryEDFSelectedSignal, true, LoadedEDFFile, sm);
        resp_plots.Item7.Maximum = Utils.GetMaxSignalValue(RespiratoryEDFSelectedSignal, true, LoadedEDFFile, sm);
      }

      UpdateRespAnalysisPlot(resp_plots, sample_period, RespiratoryEDFStartRecord ?? 1, (RespiratoryEDFStartRecord ?? 1) + (RespiratoryEDFDuration ?? 1) - 1);

      if (updatePropertiesPlot)
        UpdateRespAnalysisInfoPlot();
    }
    /// <summary>
    /// Peforms respiratory analysis 
    /// </summary>
    public void PerformRespiratoryAnalysisEDF()
    {
      if (RespiratoryEDFSelectedSignal == null)
        return;

      RespiratoryProgressRingEnabled = true;

      BackgroundWorker bw = new BackgroundWorker();
      bw.DoWork += BW_RespiratoryAnalysisEDF;
      bw.RunWorkerAsync();
    }

    private void UpdateRespAnalysisPlot(Tuple<LineSeries, ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries, DateTimeAxis, LinearAxis> resp_plots, float sample_period, int start_record, int end_record)
    {
      PlotModel tempPlotModel = new PlotModel();

      tempPlotModel.Series.Add(resp_plots.Item1);
      tempPlotModel.Series.Add(resp_plots.Item2);
      tempPlotModel.Series.Add(resp_plots.Item3);
      tempPlotModel.Series.Add(resp_plots.Item4);
      tempPlotModel.Series.Add(resp_plots.Item5);
      tempPlotModel.Axes.Add(resp_plots.Item6);
      tempPlotModel.Axes.Add(resp_plots.Item7);

      RespiratorySignalPlot = tempPlotModel;

      OnPropertyChanged(nameof(RespiratoryAnalyzedEpochs));
      UpdateRespAnalysisInfo();  
    }
    private double[] GetRespAnalysisInfo(int epoch)
    {
      DateTime start;
      float sample_period;
      PlotModel model = new PlotModel();

      if (IsAnalysisFromBinary)
      {
        start = DateTime.Parse(resp_bin_date_time_from).AddSeconds(30 * (epoch - 1));

        int start_index = (int)(((double)(start - DateTime.Parse(resp_bin_date_time_from)).TotalSeconds) / ((double)resp_bin_sample_period));
        int end_index = (int)(((double)(start.AddSeconds(30) - DateTime.Parse(resp_bin_date_time_from)).TotalSeconds) / ((double)resp_bin_sample_period));
        start_index = Math.Max(start_index, 0);
        end_index = Math.Min(end_index, resp_signal_values.Count - 1);

        sample_period = resp_bin_sample_period;

        Tuple<LineSeries, ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries, DateTimeAxis, LinearAxis> resp_plots = RespiratoryFactory.GetRespiratoryAnalysisPlot(
          resp_bin_signal_name,
          resp_signal_values.GetRange(start_index, end_index - start_index + 1),
          resp_bin_sample_period,
          resp_signal_values.Average(),
          true,
          RespiratoryMinimumPeakWidth,
          start,
          start.AddSeconds(30)
        );

        model.Series.Add(resp_plots.Item1);
        model.Series.Add(resp_plots.Item2);
        model.Series.Add(resp_plots.Item3);
        model.Series.Add(resp_plots.Item4);
        model.Series.Add(resp_plots.Item5);
        model.Axes.Add(resp_plots.Item6);
        model.Axes.Add(resp_plots.Item7);
      }
      else
      {
        start = Utils.EpochtoDateTime(epoch, LoadedEDFFile);

        LineSeries series = GetSeriesFromSignalName(out sample_period,
                                                    RespiratoryEDFSelectedSignal,
                                                    start,
                                                    start.AddSeconds(30)
                                                    );

        Tuple<LineSeries, ScatterSeries, ScatterSeries, ScatterSeries, ScatterSeries, DateTimeAxis, LinearAxis> resp_plots = RespiratoryFactory.GetRespiratoryAnalysisPlot(
          RespiratoryEDFSelectedSignal,
          series.Points.Select(temp => (float)temp.Y).ToList(),
          sample_period,
          (float)(Utils.GetMaxSignalValue(RespiratoryEDFSelectedSignal, false, LoadedEDFFile, sm) - Utils.GetMaxSignalValue(RespiratoryEDFSelectedSignal, true, LoadedEDFFile, sm)),
          true,
          RespiratoryMinimumPeakWidth,
          start,
          start.AddSeconds(30)
        );

        model.Series.Add(resp_plots.Item1);
        model.Series.Add(resp_plots.Item2);
        model.Series.Add(resp_plots.Item3);
        model.Series.Add(resp_plots.Item4);
        model.Series.Add(resp_plots.Item5);
        model.Axes.Add(resp_plots.Item6);
        model.Axes.Add(resp_plots.Item7);
      }

      return RespiratoryFactory.GetRespAnalysisInfo(model, start, sample_period);
    }
    private void UpdateRespAnalysisInfo()
    {
      if (RespiratoryAnalysisSelectedEpoch != null)
      {
        double[] output = GetRespAnalysisInfo(Int32.Parse(RespiratoryAnalysisSelectedEpoch));

        RespiratoryBreathingPeriodMean = output[0].ToString("0.## sec/breath");
        RespiratoryBreathingPeriodCoeffVar = output[1].ToString("0.## %");
        RespiratoryNegativePeakMean = output[2].ToString("0.##");
        RespiratoryNegativePeakCoeffVar = output[3].ToString("0.## %");
        RespiratoryPositivePeakMean = output[4].ToString("0.##");
        RespiratoryPositivePeakCoeffVar = output[5].ToString("0.## %");
        RespiratoryInspirationPeriodMean = output[6].ToString("0.## sec");
        RespiratoryInspirationPeriodCoeffVar = output[7].ToString("0.## %");
        RespiratoryExspirationPeriodMean = output[8].ToString("0.## sec");
        RespiratoryExspirationPeriodCoeffVar = output[9].ToString("0.## %");
        RespiratoryInspirationVolumeMean = output[10].ToString("0.##");
        RespiratoryInspirationVolumeCoeffVar = output[11].ToString("0.## %");
        RespiratoryExpirationVolumeMean = output[12].ToString("0.##");
        RespiratoryExpirationVolumeCoeffVar = output[13].ToString("0.## %");
      }
      else
      {
        RespiratoryBreathingPeriodMean = null;
        RespiratoryBreathingPeriodCoeffVar = null;
        RespiratoryNegativePeakMean = null;
        RespiratoryNegativePeakCoeffVar = null;
        RespiratoryPositivePeakMean = null;
        RespiratoryPositivePeakCoeffVar = null;
        RespiratoryInspirationPeriodMean = null;
        RespiratoryInspirationPeriodCoeffVar = null;
        RespiratoryExspirationPeriodMean = null;
        RespiratoryExspirationPeriodCoeffVar = null;
        RespiratoryInspirationVolumeMean = null;
        RespiratoryInspirationVolumeCoeffVar = null;
        RespiratoryExpirationVolumeMean = null;
        RespiratoryExpirationVolumeCoeffVar = null;
      }
    }
    private void UpdateRespAnalysisInfoPlot()
    {
      if (RespiratorySignalPlotExists)
      {
        PlotModel temp_PlotModel = new PlotModel();
        LinearAxis x_axis = new LinearAxis();
        LinearAxis y_axis = new LinearAxis();
        x_axis.Position = AxisPosition.Bottom;
        x_axis.Key = "X";
        y_axis.Key = "Y";
        List<LineSeries> series = new List<LineSeries>();
        for (int y = 0; y < 7; y++)
        {
          series.Add(new LineSeries());
          series[y].XAxisKey = "X";
          series[y].YAxisKey = "Y";
        }

        for (int x = 0; x < RespiratoryAnalyzedEpochs.Length; x++)
        {
          DateTime start;
          if (IsAnalysisFromBinary)
            start = DateTime.Parse(resp_bin_date_time_from).AddSeconds(30 * (Int32.Parse(RespiratoryAnalyzedEpochs[x]) - 1));
          else
            start = Utils.EpochtoDateTime(Int32.Parse(RespiratoryAnalyzedEpochs[x]), LoadedEDFFile);

          double[] output = GetRespAnalysisInfo(Int32.Parse(RespiratoryAnalyzedEpochs[x]));

          for (int y = 0; y < output.Length; y += 2)
          {
            series[y / 2].Points.Add(new DataPoint(Int32.Parse(RespiratoryAnalyzedEpochs[x]), output[y]));
          }
        }

        for (int x = 0; x < series.Count; x++)
        {
          temp_PlotModel.Series.Add(series[x]);
        }
        temp_PlotModel.Axes.Add(x_axis);
        temp_PlotModel.Axes.Add(y_axis);

        RespiratoryPropertiesSignalPlot = temp_PlotModel;
      }
      else
      {
        old_signal_name = null;
        RespiratoryPropertiesSignalPlot = null;
      }
    }

    #endregion

    #region etc

    // INotify Interface
    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string propertyName)
    {
      if (PropertyChanged != null)
        PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    public RespiratoryModelView(SettingsModelView i_svm)
    {
      svm = i_svm;
      svm.PropertyChanged += Exterior_PropertyChanged;
    }
  }
}
