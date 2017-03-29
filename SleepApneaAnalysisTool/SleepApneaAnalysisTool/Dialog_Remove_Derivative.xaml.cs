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
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace SleepApneaAnalysisTool
{
  /// <summary>
  /// Interaction logic for Dialog_Remove_Derivative.xaml
  /// </summary>
  public partial class Dialog_Remove_Derivative
  {
    public string[] RemovedSignals
    {
      get
      {
        string[] output = new string[listBox_DerivedSignals.SelectedItems.Count];

        for (int x = 0; x < output.Length; x++)
          output[x] = listBox_DerivedSignals.SelectedItems[x].ToString();

        return output;
      }
    }

    private MetroWindow window;
    private SettingsModelView model;

    public Dialog_Remove_Derivative(MetroWindow i_window, SettingsModelView i_model)
    {
      InitializeComponent();

      string[] DerivedSignals = i_model.sm.DerivedSignals.Select(temp => temp.DerivativeName).ToArray();

      for (int x = 0; x < DerivedSignals.Length; x++)
        listBox_DerivedSignals.Items.Add(DerivedSignals[x]);

      window = i_window;
      model = i_model;
    }

    private void button_OK_Click(object sender, RoutedEventArgs e)
    {
      if (listBox_DerivedSignals.SelectedItems.Count > 0)
      {
        model.RemoveDerivativeOutput(RemovedSignals);
        DialogManager.HideMetroDialogAsync(window, this);
      }
      else
      {
        window.ShowMessageAsync("Error", "Please select a signal.");
      }
    }
    private void button_Cancel_Click(object sender, RoutedEventArgs e)
    {
      DialogManager.HideMetroDialogAsync(window, this);
    }
  }
}
