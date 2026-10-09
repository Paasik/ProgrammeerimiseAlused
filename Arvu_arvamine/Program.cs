using System;

// Arvuti mõtleb arvu välja ainult ühe korra mängu alguses.
Random juhuarv = new Random();
int salajaneArv = juhuarv.Next(1, 101);
bool arvatud = false;

Console.WriteLine("Arva ära arv vahemikus 1–100!");

while (!arvatud)
{
	Console.Write("Sisesta oma pakkumine: ");
	string? sisend = Console.ReadLine();

	// Lõpetame ka siis, kui sisendvoog suletakse.
	if (sisend == null)
	{
		break;
	}

	if (!int.TryParse(sisend, out int pakkumine))
	{
		Console.WriteLine("Palun sisesta täisarv.");
	}
	else if (pakkumine < 1 || pakkumine > 100)
	{
		Console.WriteLine("Palun sisesta arv vahemikus 1–100.");
	}
	else if (pakkumine == salajaneArv)
	{
		Console.WriteLine("Õige! Arvasid arvu ära.");
		arvatud = true;
	}
	else if (pakkumine < salajaneArv)
	{
		Console.WriteLine("Arv on suurem.");
	}
	else
	{
		Console.WriteLine("Arv on väiksem.");
	}
}
