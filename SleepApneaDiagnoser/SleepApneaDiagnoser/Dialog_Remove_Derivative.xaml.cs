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

namespace SleepApneaDiagnoser
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
        public bool DialogResult = false;

        public Dialog_Remove_Derivative(string[][] DerivedSignals)
        {
            InitializeComponent();

            for (int x = 0; x < DerivedSignals.Length; x++)
                listBox_DerivedSignals.Items.Add(DerivedSignals[x][0]);
        }

        private void button_OK_Click(object sender, RoutedEventArgs e)
        {
            if (listBox_DerivedSignals.SelectedItems.Count > 0)
            {
                DialogResult = true;
                this.RequestCloseAsync();
            }
            else
            {
                /* TODO FIND A WAY TO SHOW THIS MESSAGE */
                // this.ShowMessageAsync("Error", "Please select a signal.");
            }
        }
        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.RequestCloseAsync();
        }
    }
}
