namespace SleepStrap.Models
{
    public sealed record FontChoice(string DisplayName, string FilePath, bool IsDefault = false)
    {
        public override string ToString() => DisplayName;
    }
}
