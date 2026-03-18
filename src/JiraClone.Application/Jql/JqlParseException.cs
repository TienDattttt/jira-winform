namespace JiraClone.Application.Jql;

public sealed class JqlParseException : Exception
{
    public JqlParseException(string message, int position, int length = 1)
        : base(message)
    {
        Position = position;
        Length = length;
    }

    public int Position { get; }
    public int Length { get; }
}
