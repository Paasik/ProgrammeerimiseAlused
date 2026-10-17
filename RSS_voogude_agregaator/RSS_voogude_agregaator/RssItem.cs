public class RssItem
{
	public string Pealkiri { get; set; } = "";
	public string Link { get; set; } = "";
	public string Kirjeldus { get; set; } = "";
	public string Valjaanne { get; set; } = "";
	public DateTimeOffset AvaldamiseAeg { get; set; }
}
