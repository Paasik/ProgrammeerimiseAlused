
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class NewsPage
{
	// Eesti ajavöönd
	private static readonly TimeZoneInfo Eesti =
		TimeZoneInfo.FindSystemTimeZoneById("Europe/Tallinn");

	// Väljaannete CSS-klassid ja logod.
	// Logod on alles tulevaseks kasutamiseks.
	private static readonly Dictionary<string, (string Css, string Logo)>
		Allikad = new()
		{
			["ERR"] = ("err", "wwwroot/logos/err.svg"),
			["Postimees"] = ("postimees", "wwwroot/logos/postimees.svg"),
			["Äripäev"] = ("aripaev", "wwwroot/logos/aripaev.svg"),
			["Delfi"] = ("delfi", "wwwroot/logos/delfi.svg"),
			["Maaleht"] = ("maaleht", "wwwroot/logos/maaleht.png")
		};

	// HTML-i erimärkide kodeerimine
	private static string E(string? tekst)
	{
		return WebUtility.HtmlEncode(tekst ?? "");
	}

	// Eesti kuupäev ja kellaaeg
	private static string Aeg(DateTimeOffset aeg)
	{
		return TimeZoneInfo.ConvertTime(aeg, Eesti)
			.ToString("dd.MM.yyyy HH:mm");
	}

	// Kuvame kuni neli lauset RSS-kirjeldusest
	private static string Kirjeldus(string tekst)
	{
		if (string.IsNullOrWhiteSpace(tekst))
			return "Loe täispikka uudist väljaande veebilehelt.";

		string[] laused = Regex.Split(
			tekst.Trim(),
			@"(?<=[.!?])\s+");

		string tulemus = string.Join(" ", laused.Take(4));

		if (tulemus.Length > 500)
			tulemus = tulemus[..497].TrimEnd() + "…";

		return tulemus;
	}

	// Uudiste ja arhiivi salvestamine
	public static void Save(
		List<RssItem> uued,
		string htmlFail,
		string arhiivFail)
	{
		DateTimeOffset praegu = DateTimeOffset.UtcNow;

		List<RssItem> eelmised = new();

		if (File.Exists(arhiivFail))
		{
			try
			{
				eelmised =
					JsonSerializer.Deserialize<List<RssItem>>(
						File.ReadAllText(arhiivFail)) ?? new();
			}
			catch (Exception ex)
			{
				Console.WriteLine(
					"Arhiivi lugemise viga: " + ex.Message);
			}
		}

		// Liidame uudised ja eemaldame kordused.
		// Säilitame ainult viimase 24 tunni uudised.
		List<RssItem> koik = uued
			.Concat(eelmised)
			.Where(x =>
				x.AvaldamiseAeg >= praegu.AddHours(-24) &&
				x.AvaldamiseAeg <= praegu.AddMinutes(10))
			.Where(x => !string.IsNullOrWhiteSpace(x.Link))
			.GroupBy(
				x => x.Link.Trim(),
				StringComparer.OrdinalIgnoreCase)
			.Select(g => g
				.OrderByDescending(x => x.AvaldamiseAeg)
				.First())
			.OrderByDescending(x => x.AvaldamiseAeg)
			.ToList();

		// JSON-arhiivi salvestamine
		string json = JsonSerializer.Serialize(
			koik,
			new JsonSerializerOptions
			{
				WriteIndented = true
			});

		File.WriteAllText(
			arhiivFail,
			json,
			new UTF8Encoding(false));

		List<RssItem> esileht = koik.Take(20).ToList();
		List<RssItem> vanemad = koik.Skip(20).ToList();

		// HTML-i loomine
		using FileStream fs = new(
			htmlFail,
			FileMode.Create,
			FileAccess.Write);

		using StreamWriter w = new(
			fs,
			new UTF8Encoding(false));

		w.WriteLine("<!DOCTYPE html>");
		w.WriteLine("<html lang=\"et\">");
		w.WriteLine("<head>");
		w.WriteLine("<meta charset=\"utf-8\">");
		w.WriteLine(
			"<meta name=\"viewport\" " +
			"content=\"width=device-width,initial-scale=1\">");
		w.WriteLine("<title>Värsked Uudised</title>");
		w.WriteLine(
			"<link rel=\"stylesheet\" href=\"wwwroot/style.css\">");
		w.WriteLine("</head>");
		w.WriteLine("<body>");

		// Päis
		w.WriteLine("<header>");
		w.WriteLine("<div class=\"header-left\">");
		w.WriteLine(
			"<button type=\"button\" " +
			"id=\"menuButton\" class=\"menu-button\" " +
			"aria-controls=\"archiveOverlay\" " +
			"aria-expanded=\"false\" " +
			"aria-label=\"Ava arhiiv\">☰</button>");

		w.WriteLine(
			"<div class=\"site-name\">" +
			"VÄRSKED <span>UUDISED</span></div>");

		w.WriteLine("</div>");

		w.WriteLine(
			$"<div class=\"updated\">" +
			$"Uuendatud {Aeg(praegu)}</div>");

		w.WriteLine("</header>");

		// Avaleht
		w.WriteLine("<main>");
		w.WriteLine(
			"<p class=\"subtitle\">" +
			"Eesti uudised ühes kohas</p>");

		w.WriteLine("<h1>20 viimast uudist</h1>");
		w.WriteLine("<ul class=\"news-grid\">");

		foreach (RssItem x in esileht)
		{
			var ilme = Allikad.GetValueOrDefault(
				x.Valjaanne,
				("err", ""));

			w.WriteLine("<li>");

			w.WriteLine(
				$"<a class=\"news-card {ilme.Item1}\" " +
				$"href=\"{E(x.Link)}\" " +
				"target=\"_blank\" " +
				"rel=\"noopener noreferrer\">");

			// Kuvame ainult väljaande nime.
			w.WriteLine(
				"<div class=\"news-top\">" +
				$"<span class=\"news-source\">" +
				$"{E(x.Valjaanne)}</span>" +
				"</div>");

			// LOGOD ON AJUTISELT VÄLJA LÜLITATUD.
			// Kui soovime kunagi logod taastada,
			// saame kasutada järgmist HTML-i:
			//
			// w.WriteLine(
			//     $"<div class=\"news-logo-box\">" +
			//     $"<img class=\"news-logo\" " +
			//     $"src=\"{E(ilme.Item2)}\" " +
			//     $"alt=\"{E(x.Valjaanne)} logo\">" +
			//     "</div>");

			w.WriteLine(
				$"<h2 class=\"news-title\">" +
				$"{E(x.Pealkiri)}</h2>");

			w.WriteLine(
				$"<p class=\"news-summary\">" +
				$"{E(Kirjeldus(x.Kirjeldus))}</p>");

			w.WriteLine("<div class=\"news-bottom\">");

			w.WriteLine(
				$"<span class=\"news-time\">" +
				$"{Aeg(x.AvaldamiseAeg)}</span>");

			w.WriteLine(
				"<span class=\"read-more\">" +
				"LOE ARTIKLIT ↗</span>");

			w.WriteLine("</div>");
			w.WriteLine("</a>");
			w.WriteLine("</li>");
		}

		w.WriteLine("</ul>");
		w.WriteLine("</main>");

		// Arhiivi hüpikaken
		w.WriteLine(
			"<div id=\"archiveOverlay\" " +
			"class=\"archive-overlay\" " +
			"aria-hidden=\"true\">");

		w.WriteLine(
			"<section class=\"archive-modal\" " +
			"role=\"dialog\" aria-modal=\"true\" " +
			"aria-label=\"Uudiste arhiiv\">");

		w.WriteLine("<div class=\"archive-header\">");

		w.WriteLine(
			"<h2>Viimase 24 tunni uudised</h2>");

		w.WriteLine(
			"<button type=\"button\" " +
			"id=\"closeArchive\" " +
			"class=\"close-button\" " +
			"aria-label=\"Sulge arhiiv\">✕</button>");

		w.WriteLine("</div>");

		w.WriteLine(
			"<p>Varasemad uudised, mis ei mahu " +
			"20 kõige värskema hulka.</p>");

		w.WriteLine("<ul class=\"archive-list\">");

		foreach (RssItem x in vanemad)
		{
			w.WriteLine("<li class=\"archive-item\">");

			w.WriteLine(
				$"<a href=\"{E(x.Link)}\" " +
				"target=\"_blank\" " +
				"rel=\"noopener noreferrer\">" +
				$"{E(x.Pealkiri)}</a>");

			w.WriteLine(
				$"<span class=\"archive-meta\">" +
				$"{E(x.Valjaanne)} · " +
				$"{Aeg(x.AvaldamiseAeg)}</span>");

			w.WriteLine("</li>");
		}

		if (vanemad.Count == 0)
		{
			w.WriteLine(
				"<li class=\"archive-item\">" +
				"Varasemaid uudiseid hetkel ei ole.</li>");
		}

		w.WriteLine("</ul>");
		w.WriteLine("</section>");
		w.WriteLine("</div>");

		// JavaScript
		w.WriteLine(
			"<script src=\"wwwroot/site.js\" defer></script>");

		w.WriteLine("</body>");
		w.WriteLine("</html>");

		Console.WriteLine(
			$"Avalehel {esileht.Count}, " +
			$"arhiivis {vanemad.Count} uudist.");
	}
}
