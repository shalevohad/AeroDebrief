using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AeroDebrief.UI.ViewModels
{
    /// <summary>
    /// ViewModel representing a frequency with connected users
    /// </summary>
    public class FrequencyPresenceViewModel : INotifyPropertyChanged
    {
        private double _frequencyMHz;
        private ObservableCollection<UserNodeViewModel> _users = new();

        public double FrequencyMHz
        {
            get => _frequencyMHz;
            set
            {
                if (_frequencyMHz != value)
                {
                    _frequencyMHz = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<UserNodeViewModel> Users
        {
            get => _users;
            set
            {
                if (_users != value)
                {
                    _users = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// ViewModel representing a user node in the presence graph
    /// </summary>
    public class UserNodeViewModel : INotifyPropertyChanged
    {
        private string _userName = string.Empty;
        private bool _isTalking;
        private bool _isJustJoined;
        private bool _isJustLeft;
        private bool _isInGroup;
        private string? _groupColor;

        public string UserName
        {
            get => _userName;
            set
            {
                if (_userName != value)
                {
                    _userName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsTalking
        {
            get => _isTalking;
            set
            {
                if (_isTalking != value)
                {
                    _isTalking = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsJustJoined
        {
            get => _isJustJoined;
            set
            {
                if (_isJustJoined != value)
                {
                    _isJustJoined = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsJustLeft
        {
            get => _isJustLeft;
            set
            {
                if (_isJustLeft != value)
                {
                    _isJustLeft = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsInGroup
        {
            get => _isInGroup;
            set
            {
                if (_isInGroup != value)
                {
                    _isInGroup = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? GroupColor
        {
            get => _groupColor;
            set
            {
                if (_groupColor != value)
                {
                    _groupColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
