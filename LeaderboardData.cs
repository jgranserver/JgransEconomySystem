public class LeaderboardEntry
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int CurrencyAmount { get; set; }
    public int Position { get; set; }
    public DateTime UpdatedAt { get; set; }
}
