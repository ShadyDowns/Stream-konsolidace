using System.Collections.ObjectModel;
using System.Windows.Media;
using StreamKartoteka.Core;

namespace StreamKartoteka.ViewModels;

public sealed class UserCardViewModel
{
    public UserCardViewModel(UserInbox inbox, int displayIndex, bool isCurrent)
    {
        Login = inbox.Login;
        DisplayName = inbox.DisplayName;
        PendingCount = inbox.PendingCount;
        LatestText = inbox.LatestMessage?.Text ?? string.Empty;
        LatestTime = inbox.LatestMessage?.SentAt.ToString("HH:mm") ?? string.Empty;
        Accent = CreateBrush(inbox.ColorHex);
        Initial = CreateInitial(inbox.DisplayName);
        DisplayIndex = displayIndex;
        IsCurrent = isCurrent;
        Messages = new ObservableCollection<ChatEntry>(inbox.Messages);
    }

    public string Login { get; }
    public string DisplayName { get; }
    public int PendingCount { get; }
    public string LatestText { get; }
    public string LatestTime { get; }
    public Brush Accent { get; }
    public string Initial { get; }
    public int DisplayIndex { get; }
    public bool IsCurrent { get; }
    public bool IsComplete => PendingCount == 0;
    public ObservableCollection<ChatEntry> Messages { get; }

    private static string CreateInitial(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length == 0 ? "?" : char.ToUpperInvariant(trimmed[0]).ToString();
    }

    private static Brush CreateBrush(string color)
    {
        try
        {
            var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
            brush.Freeze();
            return brush;
        }
        catch (Exception)
        {
            return Brushes.MediumPurple;
        }
    }
}

