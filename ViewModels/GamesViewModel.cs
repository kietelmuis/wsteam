public class GamesViewModel : ViewModelBase
{
    public double DownloadProgress { get; set; }
    public required string DownloadSpeed { get; set; }
    public required string TimeRemaining { get; set; }
    public bool IsDownloading { get; set; }
}
