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
using System.Windows.Shapes;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.Controls;

namespace SleepApneaAnalysisTool
{
  /// <summary>
  /// Interaction logic for Dialog_Hide_Signals.xaml
  /// </summary>
  public partial class Dialog_Hide_Signals
  {
    public bool[] hide_signals_new
    {
      get
      {
        bool[] value = new bool[listBox_Signals.Items.Count];
        for (int x = 0; x < listBox_Signals.Items.Count; x++)
        {
          if (listBox_Signals.SelectedItems.Contains(listBox_Signals.Items[x]))
            value[x] = false;
          else
            value[x] = true;
        }
        return value;
      }
    }

    private MetroWindow window;
    private SettingsModelView model;

    public Dialog_Hide_Signals(MetroWindow i_window, SettingsModelView i_model)
    {
      string[] edf_signals = i_model.EDFAllSignals.ToArray();

      bool[] input = new bool[i_model.EDFAllSignals.Count];
      for (int x = 0; x < i_model.EDFAllSignals.Count; x++)
      {
        if (i_model.sm.HiddenSignals.Contains(i_model.EDFAllSignals[x]))
          input[x] = true;
        else
          input[x] = false;
      }
      
      InitializeComponent();

      for (int x = 0; x < edf_signals.Length; x++)
      {
        listBox_Signals.Items.Add(edf_signals[x]);
        if (!input[x])
          listBox_Signals.SelectedItems.Add(listBox_Signals.Items[listBox_Signals.Items.Count - 1]);
      }

      window = i_window;
      model = i_model;
    }

    private void button_Done_Click(object sender, RoutedEventArgs e)
    {
      model.HideSignalsOutput(hide_signals_new);
      DialogManager.HideMetroDialogAsync(window, this);
    }
  }
}
