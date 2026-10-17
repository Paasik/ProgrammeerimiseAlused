
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Uudisvoogude aadressid
var vood = new Dictionary<string, string>
{
	["Postimees"] = "https://www.postimees.ee/rss",
	["Äripäev"] = "https://feeds.feedburner.com/aripaev-rss",
	["ERR"] = "https://www.err.ee/rss",
	["Maaleht"] =
		"https://maaleht.delfi.ee/artikkel/23970575/rss",
	["Delfi"] = "https://feeds2.feedburner.com/delfiuudised"
};

// Väljundfailid
string kaust = AppContext.BaseDirectory;

string htmlFail = Path.Combine(
	kaust, "uudised.html");

string arhiiviFail = Path.Combine(
	kaust, "arhiiv.json");

string tulemusteFail = Path.Combine(
	kaust, "mootmistulemused.txt");

// Kogu programmi tööaja mõõtmine
var koguKell = Stopwatch.StartNew();

using var klient = new HttpClient
{
	Timeout = TimeSpan.FromSeconds(20)
};

klient.DefaultRequestHeaders.UserAgent.ParseAdd(
	"VarskedUudised/1.0");

// Kolm kiiruskatset
var katsed = new[]
{
	await Katse("Järjestikune", LoeJarjest),
	await Katse("Task.Run + ConcurrentBag", LoeTaskRun),
	await Katse("Paralleelne async/await", LoeAsync)
};

// Koondame uudised kõigist katsetest.
// Kordused eemaldatakse NewsPage klassis.
List<RssItem> koikUudised = katsed
	.SelectMany(k => k.Uudised)
	.ToList();

// Uue HTML-lehe ja arhiivi loomine
NewsPage.Save(
	koikUudised,
	htmlFail,
	arhiiviFail);

koguKell.Stop();

// Kiiruskatsete aruanne
var raport = new StringBuilder();

raport.AppendLine("RSS-VOOGUDE KIIRUSE VÕRDLUS");

raport.AppendLine(
	$"Mõõdetud: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");

raport.AppendLine();

foreach (var k in katsed)
{
	raport.AppendLine(
		$"{k.Nimi}: RSS {k.RssAeg.TotalMilliseconds:F0} ms; " +
		$"katse {k.KoguAeg.TotalMilliseconds:F0} ms; " +
		$"{k.Uudised.Count} uudist");
}

raport.AppendLine();

if (katsed[1].RssAeg.TotalMilliseconds > 0)
{
	double kordaja =
		katsed[0].RssAeg.TotalMilliseconds /
		katsed[1].RssAeg.TotalMilliseconds;

	raport.AppendLine(
		$"Task.Run võrreldes järjestikusega: {kordaja:F2}x");
}

raport.AppendLine(
	$"Kogu programmi aeg: " +
	$"{koguKell.Elapsed.TotalMilliseconds:F0} ms");

File.WriteAllText(
	tulemusteFail,
	raport.ToString(),
	new UTF8Encoding(false));

Console.WriteLine(raport);
Console.WriteLine("Veebileht: " + htmlFail);
Console.WriteLine("Arhiiv: " + arhiiviFail);

// Windowsis avame lehe brauseris.
// GitHub Actionsi Linuxis brauserit ei avata.
if (OperatingSystem.IsWindows())
{
	try
	{
		Process.Start(
			new ProcessStartInfo(htmlFail)
			{
				UseShellExecute = true
			});
	}
	catch (Exception ex)
	{
		Console.WriteLine(
			"Brauserit ei saanud avada: " + ex.Message);
	}
}

// Ühe kiiruskatse läbiviimine
async Task<KatseTulemus> Katse(
	string nimi,
	Func<Task<List<RssItem>>> lugemine)
{
	var kogu = Stopwatch.StartNew();
	var rss = Stopwatch.StartNew();

	List<RssItem> uudised = await lugemine();

	rss.Stop();

	// LINQ: valime 20 värskeimat
	var viimased = uudised
		.OrderByDescending(u => u.AvaldamiseAeg)
		.Take(20)
		.ToList();

	// Kiiruskatse HTML väljund
	using (var fs = new FileStream(
		Path.Combine(kaust, "katse.html"),
		FileMode.Create,
		FileAccess.Write))
	using (var w = new StreamWriter(
		fs, new UTF8Encoding(false)))
	{
		w.WriteLine("<!DOCTYPE html>");
		w.WriteLine("<html lang=\"et\">");
		w.WriteLine("<meta charset=\"utf-8\">");
		w.WriteLine("<ul>");

		foreach (var u in viimased)
		{
			w.WriteLine(
				$"<li><a href=\"{WebUtility.HtmlEncode(u.Link)}\">" +
				$"{WebUtility.HtmlEncode(u.Pealkiri)}</a></li>");
		}

		w.WriteLine("</ul></html>");
	}

	kogu.Stop();

	Console.WriteLine(
		$"{nimi}: {uudised.Count} uudist, " +
		$"{rss.Elapsed.TotalMilliseconds:F0} ms");

	return new KatseTulemus(
		nimi,
		rss.Elapsed,
		kogu.Elapsed,
		uudised);
}

// Järjestikune lugemine
async Task<List<RssItem>> LoeJarjest()
{
	var tulemus = new List<RssItem>();

	foreach (var v in vood)
	{
		tulemus.AddRange(
			await LoeVoog(v.Key, v.Value));
	}

	return tulemus;
}

// Paralleelne Task.Run + ConcurrentBag
async Task<List<RssItem>> LoeTaskRun()
{
	var bag = new ConcurrentBag<RssItem>();

	var taskid = vood.Select(v =>
		Task.Run(async () =>
		{
			List<RssItem> uudised =
				await LoeVoog(v.Key, v.Value);

			foreach (RssItem u in uudised)
			{
				bag.Add(u);
			}
		}));

	await Task.WhenAll(taskid);

	return bag.ToList();
}

// Paralleelne async/await
async Task<List<RssItem>> LoeAsync()
{
	var tulemused = await Task.WhenAll(
		vood.Select(v => LoeVoog(v.Key, v.Value)));

	return tulemused
		.SelectMany(x => x)
		.ToList();
}

// Ühe RSS-voo lugemine
async Task<List<RssItem>> LoeVoog(
	string nimi,
	string url)
{
	var tulemus = new List<RssItem>();

	try
	{
		string xml = await klient.GetStringAsync(url);
		XDocument doc = XDocument.Parse(xml);

		var kirjed = doc.Descendants()
			.Where(x =>
				x.Name.LocalName is "item" or "entry");

		foreach (var item in kirjed)
		{
			string pealkiri =
				Puhasta(Vaartus(item, "title"));

			var linkElement = item.Elements()
				.FirstOrDefault(x =>
					x.Name.LocalName == "link");

			string link =
				linkElement?.Attribute("href")?.Value ??
				linkElement?.Value.Trim() ?? "";

			string kirjeldus =
				Vaartus(item, "description");

			if (string.IsNullOrEmpty(kirjeldus))
				kirjeldus = Vaartus(item, "summary");

			if (string.IsNullOrEmpty(kirjeldus))
				kirjeldus = Vaartus(item, "content");

			string aeg = Vaartus(item, "pubDate");

			if (string.IsNullOrEmpty(aeg))
				aeg = Vaartus(item, "published");

			if (string.IsNullOrEmpty(aeg))
				aeg = Vaartus(item, "updated");

			if (!DateTimeOffset.TryParse(
				aeg,
				CultureInfo.InvariantCulture,
				DateTimeStyles.AllowWhiteSpaces,
				out var avaldamiseAeg))
			{
				continue;
			}

			if (!Uri.TryCreate(
					link,
					UriKind.Absolute,
					out var uri) ||
				uri.Scheme is not ("http" or "https") ||
				pealkiri.Length == 0)
			{
				continue;
			}

			tulemus.Add(new RssItem
			{
				Pealkiri = pealkiri,
				Link = link,
				Kirjeldus = Puhasta(kirjeldus),
				Valjaanne = nimi,
				AvaldamiseAeg = avaldamiseAeg
			});
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine(
			$"{nimi}: RSS-viga: {ex.Message}");
	}

	return tulemus;
}

// XML-elemendi väärtus
static string Vaartus(XElement x, string nimi)
{
	return x.Elements()
		.FirstOrDefault(e => e.Name.LocalName == nimi)
		?.Value.Trim() ?? "";
}

// Puhastame RSS-tekstist HTML-märgendid
static string Puhasta(string tekst)
{
	// Näiteks <em>performance</em>
	// muutub tavaliseks sõnaks performance.
	for (int i = 0; i < 2; i++)
	{
		tekst = WebUtility.HtmlDecode(tekst);
	}

	tekst = Regex.Replace(
		tekst, "<[^>]+>", " ");

	return Regex.Replace(
		tekst, @"\s+", " ").Trim();
}

// Kiiruskatse andmed
record KatseTulemus(
	string Nimi,
	TimeSpan RssAeg,
	TimeSpan KoguAeg,
	List<RssItem> Uudised);
