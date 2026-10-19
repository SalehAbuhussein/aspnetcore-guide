namespace TodoApi.Services;

public class EmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "";
    public string NotifyTo { get; set; } = "";
}
