using StreamKartoteka.Core;

var tests = new (string Name, Action Run)[]
{
    ("Parser reads Twitch tags and message", ParserReadsTaggedMessage),
    ("Parser supports plain PRIVMSG", ParserReadsPlainMessage),
    ("Parser rejects non-chat commands", ParserRejectsOtherCommands),
    ("IRC tags are unescaped", TagsAreUnescaped),
    ("Queue preserves global FIFO order", QueuePreservesFifo),
    ("Queue deduplicates Twitch message IDs", QueueDeduplicatesIds),
    ("Users are grouped into one inbox", UsersAreGrouped)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static void ParserReadsTaggedMessage()
{
    const string line = "@badge-info=;badges=subscriber/1;color=#12AbEF;display-name=ČeskýDivák;id=abc-123;tmi-sent-ts=1760000000000 :ceskydivak!ceskydivak@ceskydivak.tmi.twitch.tv PRIVMSG #kanal :Ahoj, streame!";
    Assert(IrcMessageParser.TryParsePrivMsg(line, out var message), "Expected a parsed message.");
    Assert(message is not null, "Message was null.");
    Equal("abc-123", message!.Id);
    Equal("ceskydivak", message.UserLogin);
    Equal("ČeskýDivák", message.DisplayName);
    Equal("Ahoj, streame!", message.Text);
    Equal("#12ABEF", message.ColorHex);
}

static void ParserReadsPlainMessage()
{
    const string line = ":viewer!viewer@viewer.tmi.twitch.tv PRIVMSG #channel :Text without tags";
    Assert(IrcMessageParser.TryParsePrivMsg(line, out var message), "Expected a parsed message.");
    Equal("viewer", message!.DisplayName);
    Equal("Text without tags", message.Text);
    Assert(message.ColorHex.StartsWith('#'), "Expected a generated fallback color.");
}

static void ParserRejectsOtherCommands()
{
    Assert(!IrcMessageParser.TryParsePrivMsg("PING :tmi.twitch.tv", out _), "PING must not become a chat message.");
    Assert(!IrcMessageParser.TryParsePrivMsg(":tmi.twitch.tv ROOMSTATE #channel", out _), "ROOMSTATE must not become a chat message.");
}

static void TagsAreUnescaped()
{
    Equal("Hello World;test\\ok", IrcMessageParser.UnescapeTag(@"Hello\sWorld\:test\\ok"));
}

static void QueuePreservesFifo()
{
    var queue = new ConversationQueue();
    queue.Add(Message("1", "alice", "A1"));
    queue.Add(Message("2", "bob", "B1"));
    queue.Add(Message("3", "alice", "A2"));

    Equal("A1", queue.Current!.Text);
    queue.Advance();
    Equal("B1", queue.Current!.Text);
    queue.Advance();
    Equal("A2", queue.Current!.Text);
    queue.Advance();
    Assert(queue.Current is null, "Queue should be empty.");
}

static void QueueDeduplicatesIds()
{
    var queue = new ConversationQueue();
    Assert(queue.Add(Message("same", "alice", "First")), "First message should be accepted.");
    Assert(!queue.Add(Message("same", "alice", "Duplicate")), "Duplicate should be rejected.");
    Equal(1, queue.PendingCount);
}

static void UsersAreGrouped()
{
    var queue = new ConversationQueue();
    queue.Add(Message("1", "alice", "First"));
    queue.Add(Message("2", "bob", "Second"));
    queue.Add(Message("3", "alice", "Third"));

    Equal(2, queue.UserCount);
    Equal(2, queue.GetUser("ALICE")!.Messages.Count);
    Equal(2, queue.GetUser("alice")!.PendingCount);
}

static ChatEntry Message(string id, string login, string text) =>
    new(id, login, login, text, DateTimeOffset.Now, "#8B5CF6");

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

