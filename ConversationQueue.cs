namespace StreamKartoteka.Core;

public sealed class ConversationQueue
{
    private readonly Dictionary<string, UserInbox> _users =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<ChatEntry> _pending = new();
    private readonly HashSet<string> _knownIds = new(StringComparer.Ordinal);

    public ChatEntry? Current => _pending.Count == 0 ? null : _pending.Peek();
    public int PendingCount => _pending.Count;
    public int UserCount => _users.Count;

    public bool Add(ChatEntry message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.UserLogin) || string.IsNullOrWhiteSpace(message.Text))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(message.Id) && !_knownIds.Add(message.Id))
        {
            return false;
        }

        if (!_users.TryGetValue(message.UserLogin, out var inbox))
        {
            inbox = new UserInbox(message.UserLogin, message.DisplayName, message.ColorHex);
            _users.Add(message.UserLogin, inbox);
        }

        inbox.Add(message);
        _pending.Enqueue(message);
        return true;
    }

    public ChatEntry? Advance()
    {
        if (_pending.TryDequeue(out var handled))
        {
            handled.IsHandled = true;
        }

        return Current;
    }

    public UserInbox? GetUser(string login) =>
        _users.GetValueOrDefault(login);

    public IReadOnlyList<UserInbox> GetUsersInDisplayOrder()
    {
        var firstPendingPosition = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var position = 0;

        foreach (var message in _pending)
        {
            firstPendingPosition.TryAdd(message.UserLogin, position++);
        }

        return _users.Values
            .OrderBy(user => firstPendingPosition.ContainsKey(user.Login) ? 0 : 1)
            .ThenBy(user => firstPendingPosition.GetValueOrDefault(user.Login, int.MaxValue))
            .ThenByDescending(user => user.LatestMessage?.SentAt)
            .ToList();
    }

    public void Clear()
    {
        _users.Clear();
        _pending.Clear();
        _knownIds.Clear();
    }
}

