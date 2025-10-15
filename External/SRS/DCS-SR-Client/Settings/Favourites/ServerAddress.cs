﻿using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.Settings.Favourites;

public class ServerAddress : INotifyPropertyChanged
{
    private string _address;

    private string _eamCoalitionPassword;

    private bool _isDefault;

    private string _name;

    public ServerAddress(string name, string address, string eamCoalitionPassword, bool isDefault)
    {
        // Set private values directly so we don't trigger useless re-saving of favourites list when being loaded for the first time
        _name = name;
        _address = address;
        _eamCoalitionPassword = eamCoalitionPassword;
        IsDefault = isDefault; // Explicitly use property setter here since IsDefault change includes additional logic
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Address
    {
        get => _address;
        set
        {
            if (_address != value)
            {
                _address = value;
                OnPropertyChanged();
            }
        }
    }

    public string EAMCoalitionPassword
    {
        get => _eamCoalitionPassword;
        set
        {
            if (_eamCoalitionPassword != value)
            {
                _eamCoalitionPassword = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            _isDefault = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}