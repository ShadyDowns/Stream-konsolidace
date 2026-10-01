using System.Collections.ObjectModel;
using StreamKartoteka.Core;

namespace StreamKartoteka.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ConversationQueue _queue = new();
    private ChatEntry? _currentMessage;
    private UserCardViewModel? _currentUser;
    private int _pendingCount;
    private int _userCount;
    private bool _hasMessages;
    private string _statusText = "Nepřipojeno";
    private bool _isConnected;

    public ObservableCollection<UserCardViewModel> Users { get; } = [];

    public ChatEntry? CurrentMessage
    {
        get => _currentMessage;
        private set => SetProperty(ref _currentMessage, value);
    }

    public UserCardViewModel? CurrentUser
    {
        get => _currentUser;
        private set => SetProperty(ref _currentUser, value);
    }

    public int PendingCount
    {
        get => _pendingCount;
        private set => SetProperty(ref _pendingCount, value);
    }

    public int UserCount
    {
        get => _userCount;
        private set
        {
            if (SetProperty(ref _userCount, value))
            {
                OnPropertyChanged(nameof(HasUsers));
                OnPropertyChanged(nameof(HasNoUsers));
            }
        }
    }

    public bool HasUsers => UserCount > 0;
    public bool HasNoUsers => !HasUsers;

    public bool HasMessages
    {
        get => _hasMessages;
        private set
        {
            if (SetProperty(ref _hasMessages, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public bool IsEmpty => !HasMessages;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionButtonText));
            }
        }
    }

    public string ConnectionButtonText => IsConnected ? "Odpojit" : "Připojit";

    public void AddMessage(ChatEntry message)
    {
        if (_queue.Add(message))
        {
            Refresh();
        }
    }

    public void NextMessage()
    {
        _queue.Advance();
        Refresh();
    }

    public void Clear()
    {
        _queue.Clear();
        Refresh();
    }

    private void Refresh()
    {
        CurrentMessage = _queue.Current;
        PendingCount = _queue.PendingCount;
        UserCount = _queue.UserCount;
        HasMessages = _queue.Current is not null;

        Users.Clear();
        var currentLogin = _queue.Current?.UserLogin;
        var index = 1;
        foreach (var inbox in _queue.GetUsersInDisplayOrder())
        {
            Users.Add(new UserCardViewModel(
                inbox,
                index++,
                string.Equals(inbox.Login, currentLogin, StringComparison.OrdinalIgnoreCase)));
        }

        CurrentUser = currentLogin is null
            ? null
            : Users.FirstOrDefault(user => string.Equals(user.Login, currentLogin, StringComparison.OrdinalIgnoreCase));
    }
}
