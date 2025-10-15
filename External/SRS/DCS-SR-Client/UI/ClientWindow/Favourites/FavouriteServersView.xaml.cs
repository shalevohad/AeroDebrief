﻿using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Favourites;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites;

/// <summary>
///     Interaction logic for FavouriteServersView.xaml
/// </summary>
public partial class FavouriteServersView : UserControl
{
    public FavouriteServersView()
    {
        InitializeComponent();
    }

    private void DataGridRow_Drop(object sender, DragEventArgs e)
    {
        var droppedAddress = (ServerAddress)e.Data.GetData(typeof(ServerAddress));

        var targetGridRow = sender as DataGridRow;
        var targetAddress = targetGridRow.DataContext as ServerAddress;


        var serverAddresses = FavouritesGrid.ItemsSource as ObservableCollection<ServerAddress>;

        serverAddresses.Move(serverAddresses.IndexOf(droppedAddress), serverAddresses.IndexOf(targetAddress));
    }

    private void DataGridRow_MouseMove(object sender, MouseEventArgs e)
    {
        var selectedRow = sender as DataGridRow;

        if (selectedRow != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var serverAddress = selectedRow.DataContext as ServerAddress;

            try
            {
                //if we're editing it gets stuck editing if we trigger drag drop
                if (!selectedRow.IsEditing) DragDrop.DoDragDrop(FavouritesGrid, serverAddress, DragDropEffects.Move);
            }
            catch (Exception)
            {
                // catches any out of bounds movements, should probably be replaced with validation at some point
            }
        }
    }
}