using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Kolm katset kasutavad samu vooge. Voo aadress võib aja jooksul muutuda.
var vood = new Dictionary<string, string>
{
	["Postimees"] = "https://www.postimees.ee/rss",
	["Äripäev"] = "https://feeds.feedburner.com/aripaev-rss",
	["ERR"] = "https://www.err.ee/rss",
	["Maaleht"] = "https://maaleht.delfi.ee/artikkel/23970575/rss",
	["Delfi"] = "https://feeds2.feedburner.com/delfiuudised"
};

string htmlFail = Path.Combine(AppContext.BaseDirectory, "uudised.html");
string tulemusteFail = Path.Combine(AppContext.BaseDirectory, "mootmistulemused.txt");
var koguProgrammiKell = Stopwatch.StartNew();

// HttpClient on mõeldud korduvaks kasutamiseks, ka mitme samaaegse päringu korral.
using var klient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
klient.DefaultRequestHeaders.UserAgent.ParseAdd("RSSAgregaator/1.0");

Console.WriteLine("RSS-agregaatori kiiruskatse\n");

// 1. katse: järjestikune töö (vana lahenduse mõõtmine)
var esimene = await TeeKatseAsync("1. Järjestikune", () => LoeJarjestAsync());

// 2. katse: Task.Run ja ConcurrentBag
var teine = await TeeKatseAsync("2. Paralleelne Task.Run", () => LoeParalleelseltTaskRunAsync());

// 3. katse: optimeeritud võrgupäringud ilma tarbetu Task.Run ümbriseta
var kolmas = await TeeKatseAsync("3. Paralleelne async/await", () => LoeParalleelseltAsync());

koguProgrammiKell.Stop();

var aruanne = new StringBuilder();
aruanne.AppendLine("RSS-VOOGUDE KIIRUSE VÕRDLUS");
aruanne.AppendLine($"Mõõtmise aeg: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
aruanne.AppendLine();
foreach (var tulemus in new[] { esimene, teine, kolmas })
{
	aruanne.AppendLine(tulemus.Nimi);
	aruanne.AppendLine($"  RSS-voogude lugemine: {tulemus.RssAeg.TotalMilliseconds:F0} ms");
	aruanne.AppendLine($"  Katse koguaeg (koos HTML-iga): {tulemus.KoguAeg.TotalMilliseconds:F0} ms");
	aruanne.AppendLine($"  Leitud uudiseid: {tulemus.UudisteArv}");
	aruanne.AppendLine();
}

LisaVordlus(aruanne, "Task.Run võrreldes järjestikusega", esimene, teine);
LisaVordlus(aruanne, "Async/await võrreldes Task.Run-iga", teine, kolmas);
LisaVordlus(aruanne, "Async/await võrreldes järjestikusega", esimene, kolmas);
aruanne.AppendLine($"Kogu programmi tööaeg (kõik kolm katset): {koguProgrammiKell.Elapsed.TotalMilliseconds:F0} ms");
aruanne.AppendLine("Märkus: katsed loevad vooge korduvalt; võrguolud ja serveri vahemälu mõjutavad tulemusi.");

File.WriteAllText(tulemusteFail, aruanne.ToString(), new UTF8Encoding(false));
Console.WriteLine("\n" + aruanne);
Console.WriteLine("Uudiste fail: " + htmlFail);
Console.WriteLine("Mõõtmiste fail: " + tulemusteFail);

// Brauser avaneb alles pärast mõõtmiste lõppu; selle käivitusaeg ei kuulu mõõtmisse.
try
{
	Process.Start(new ProcessStartInfo(htmlFail) { UseShellExecute = true });
}
catch (Exception ex)
{
	Console.WriteLine("Ava HTML-fail käsitsi. Brauseri avamise viga: " + ex.Message);
}

async Task<KatseTulemus> TeeKatseAsync(string nimi, Func<Task<List<RssItem>>> lugemine)
{
	Console.WriteLine("Alustan: " + nimi);
	var katseKell = Stopwatch.StartNew();
	var rssKell = Stopwatch.StartNew();
	List<RssItem> uudised = await lugemine();
	rssKell.Stop();

	// Kõikide voogude uudised sorteeritakse koos.
	List<RssItem> viimased = uudised
		.OrderByDescending(x => x.AvaldamiseAeg)
		.Take(20)
		.ToList();

	SalvestaHtml(viimased, htmlFail);
	katseKell.Stop();
	Console.WriteLine($"Valmis: {nimi}, RSS {rssKell.Elapsed.TotalMilliseconds:F0} ms, kogu katse {katseKell.Elapsed.TotalMilliseconds:F0} ms\n");
	return new KatseTulemus(nimi, rssKell.Elapsed, katseKell.Elapsed, uudised.Count);
}

async Task<List<RssItem>> LoeJarjestAsync()
{
	var uudised = new List<RssItem>();
	foreach (var voog in vood)
	{
		uudised.AddRange(await LoeVoogOhutultAsync(voog.Key, voog.Value));
	}
	return uudised;
}

async Task<List<RssItem>> LoeParalleelseltTaskRunAsync()
{
	var kogum = new ConcurrentBag<RssItem>();
	var taskid = vood.Select(voog => Task.Run(async () =>
	{
		List<RssItem> yheVooUudised = await LoeVoogOhutultAsync(voog.Key, voog.Value);
		foreach (var uudis in yheVooUudised)
			kogum.Add(uudis);
	})).ToArray();

	await Task.WhenAll(taskid);
	return kogum.ToList();
}

async Task<List<RssItem>> LoeParalleelseltAsync()
{
	// HTTP päring on asünkroonne I/O: Task.Run ei ole selle jaoks vajalik.
	Task<List<RssItem>>[] taskid = vood
		.Select(voog => LoeVoogOhutultAsync(voog.Key, voog.Value))
		.ToArray();

	List<RssItem>[] koikVood = await Task.WhenAll(taskid);
	return koikVood.SelectMany(x => x).ToList();
}

async Task<List<RssItem>> LoeVoogOhutultAsync(string nimi, string url)
{
	try
	{
		string xml = await klient.GetStringAsync(url);
		var dokument = XDocument.Parse(xml);
		var tulemus = new List<RssItem>();
		var kirjed = dokument.Descendants().Where(x => x.Name.LocalName is "item" or "entry");

		foreach (var kirje in kirjed)
		{
			string pealkiri = Vaartus(kirje, "title");
			XElement? lingiElement = kirje.Elements().FirstOrDefault(x => x.Name.LocalName == "link");
			string link = lingiElement?.Attribute("href")?.Value ?? lingiElement?.Value.Trim() ?? "";
			string kirjeldus = Vaartus(kirje, "description");
			if (string.IsNullOrWhiteSpace(kirjeldus)) kirjeldus = Vaartus(kirje, "summary");
			if (string.IsNullOrWhiteSpace(kirjeldus)) kirjeldus = Vaartus(kirje, "content");

			string aeg = Vaartus(kirje, "pubDate");
			if (string.IsNullOrWhiteSpace(aeg)) aeg = Vaartus(kirje, "published");
			if (string.IsNullOrWhiteSpace(aeg)) aeg = Vaartus(kirje, "updated");
			if (!DateTimeOffset.TryParse(aeg, CultureInfo.InvariantCulture,
					DateTimeStyles.AllowWhiteSpaces, out var avaldamiseAeg)) continue;
			if (string.IsNullOrWhiteSpace(pealkiri) ||
				!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
				(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) continue;

			kirjeldus = Regex.Replace(kirjeldus, "<[^>]*>", " ");
			kirjeldus = WebUtility.HtmlDecode(kirjeldus);
			kirjeldus = Regex.Replace(kirjeldus, @"\s+", " ").Trim();
			tulemus.Add(new RssItem
			{
				Pealkiri = pealkiri,
				Link = link,
				Kirjeldus = kirjeldus,
				Valjaanne = nimi,
				AvaldamiseAeg = avaldamiseAeg
			});
		}
		Console.WriteLine($"  {nimi}: {tulemus.Count} uudist");
		return tulemus;
	}
	catch (Exception ex)
	{
		Console.WriteLine($"  {nimi}: viga - {ex.Message}");
		return new List<RssItem>();
	}
}

static string Vaartus(XElement element, string nimi) =>
	element.Elements().FirstOrDefault(x => x.Name.LocalName == nimi)?.Value.Trim() ?? "";

static void SalvestaHtml(List<RssItem> uudised, string failitee)
{
	using var fail = new FileStream(failitee, FileMode.Create, FileAccess.Write);
	using var writer = new StreamWriter(fail, new UTF8Encoding(false));
	writer.WriteLine("<!doctype html><html lang=\"et\"><head><meta charset=\"utf-8\"><title>Viimased 20 uudist</title>");
	writer.WriteLine("<style>body{font-family:Arial,sans-serif;max-width:850px;margin:35px auto;padding:16px;background:#f5f6f8}li{background:#fff;padding:16px;margin:12px 0;border-radius:8px}a{font-weight:bold;color:#175b9b}.meta{color:#666;margin:8px 0}ul{list-style:none;padding:0}</style></head><body>");
	writer.WriteLine("<h1>20 viimast uudist</h1><ul>");
	foreach (RssItem uudis in uudised)
	{
		writer.WriteLine("<li>");
		writer.WriteLine($"<a href=\"{WebUtility.HtmlEncode(uudis.Link)}\">{WebUtility.HtmlEncode(uudis.Pealkiri)}</a><br />");
		writer.WriteLine($"<div class=\"meta\">{uudis.AvaldamiseAeg.ToLocalTime():dd.MM.yyyy HH:mm} | {WebUtility.HtmlEncode(uudis.Valjaanne)}</div>");
		writer.WriteLine($"{WebUtility.HtmlEncode(uudis.Kirjeldus)}");
		writer.WriteLine("</li>");
	}
	writer.WriteLine("</ul></body></html>");
}

static void LisaVordlus(StringBuilder tekst, string nimi, KatseTulemus enne, KatseTulemus parast)
{
	tekst.AppendLine(nimi + ":");
	LisaAegVordlus(tekst, "RSS lugemine", enne.RssAeg, parast.RssAeg);
	LisaAegVordlus(tekst, "Kogu katse", enne.KoguAeg, parast.KoguAeg);
	tekst.AppendLine();
}

static void LisaAegVordlus(StringBuilder tekst, string silt, TimeSpan enne, TimeSpan parast)
{
	double vahe = (enne - parast).TotalMilliseconds;
	double kordaja = parast.TotalMilliseconds > 0 ? enne.TotalMilliseconds / parast.TotalMilliseconds : 0;
	tekst.AppendLine($"  {silt}: {(vahe >= 0 ? "kiirem" : "aeglasem")} {Math.Abs(vahe):F0} ms; " +
		(vahe >= 0 ? $"kiiruse suhe {kordaja:F2}x" : $"aeglasem {1 / Math.Max(kordaja, 0.000001):F2}x"));
}

record KatseTulemus(string Nimi, TimeSpan RssAeg, TimeSpan KoguAeg, int UudisteArv);
