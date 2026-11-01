using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace objekt_orienteeritud_progem
{
	public class Book
	{
		private readonly List<Borrowing> borrowings = new List<Borrowing>();

		public string Title { get; }
		public string Author { get; }
		public string ISBN { get; }
		public int YearPublished { get; }
		public int Pages { get; }
		public bool IsAvailable => !borrowings.Any(b => !b.ReturnDate.HasValue);
		public IReadOnlyList<Borrowing> Borrowings => borrowings.AsReadOnly();

		public Book(string title, string author, string isbn, int yearPublished, int pages)
		{
			if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
				throw new ArgumentException("Pealkiri on kohustuslik ja võib olla kuni 200 tähemärki.");
			if (string.IsNullOrWhiteSpace(author) || author.Trim().Length > 100)
				throw new ArgumentException("Autor on kohustuslik ja võib olla kuni 100 tähemärki.");
			if (string.IsNullOrWhiteSpace(isbn) || isbn.Length != 13)
				throw new ArgumentException("ISBN peab olema täpselt 13 tähemärki pikk.");
			if (yearPublished < 1900 || yearPublished > DateTime.Today.Year)
				throw new ArgumentException("Ilmumisaasta peab olema vahemikus 1900 kuni praegune aasta.");
			if (pages < 1)
				throw new ArgumentException("Lehekülgede arv peab olema vähemalt 1.");

			Title = title.Trim();
			Author = author.Trim();
			ISBN = isbn;
			YearPublished = yearPublished;
			Pages = pages;
		}

		internal void AddBorrowing(Borrowing borrowing) => borrowings.Add(borrowing);
	}

	public class User
	{
		private readonly List<Borrowing> borrowings = new List<Borrowing>();

		public string FirstName { get; }
		public string LastName { get; }
		public string Email { get; }
		// Tühi string tähendab, et telefoninumbrit pole lisatud.
		public string Phone { get; }
		public int MembershipNo { get; }
		public DateTime DateRegistered { get; }
		public IReadOnlyList<Borrowing> Borrowings => borrowings.AsReadOnly();
		public int ActiveBorrowingCount => borrowings.Count(b => !b.ReturnDate.HasValue);

		public User(string firstName, string lastName, string email, string phone,
			int membershipNo, DateTime dateRegistered)
		{
			if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
				throw new ArgumentException("Ees- ja perekonnanimi on kohustuslikud.");
			if (string.IsNullOrWhiteSpace(email))
				throw new ArgumentException("E-posti aadress on kohustuslik.");
			phone = phone ?? "";
			if (phone.Length > 0 && !Regex.IsMatch(phone, @"\A[0-9]{8}\z"))
				throw new ArgumentException("Telefoninumber peab koosnema täpselt 8 numbrist.");
			if (membershipNo < 1)
				throw new ArgumentException("Kasutaja tunnusnumber peab olema positiivne.");
			if (dateRegistered.Date > DateTime.Today)
				throw new ArgumentException("Liitumise kuupäev ei saa olla tulevikus.");

			FirstName = firstName.Trim();
			LastName = lastName.Trim();
			Email = email.Trim();
			Phone = phone;
			MembershipNo = membershipNo;
			DateRegistered = dateRegistered.Date;
		}

		internal void AddBorrowing(Borrowing borrowing) => borrowings.Add(borrowing);
	}

	public class Borrowing
	{
		public Book Book { get; }
		public User User { get; }
		public DateTime BorrowingDate { get; }
		public DateTime ReturnDeadline => BorrowingDate.AddDays(14);
		public DateTime? ReturnDate { get; private set; }
		public bool ReminderSent { get; private set; }

		public Borrowing(Book book, User user, DateTime borrowingDate)
		{
			if (book == null) throw new ArgumentNullException(nameof(book));
			if (user == null) throw new ArgumentNullException(nameof(user));
			if (!book.IsAvailable)
				throw new InvalidOperationException("Raamat on juba välja laenutatud.");
			if (user.ActiveBorrowingCount >= 5)
				throw new InvalidOperationException("Kasutajal võib korraga olla kuni 5 raamatut.");
			if (borrowingDate.Date > DateTime.Today || borrowingDate.Date < user.DateRegistered)
				throw new ArgumentException("Laenutuse kuupäev peab olema liitumise ja tänase päeva vahel.");
			if (book.Borrowings.Any(b => b.ReturnDate.HasValue && b.ReturnDate.Value > borrowingDate.Date))
				throw new ArgumentException("Laenutus ei saa kattuda raamatu varasema laenutusega.");

			Book = book;
			User = user;
			BorrowingDate = borrowingDate.Date;
			book.AddBorrowing(this);
			user.AddBorrowing(this);
		}

		public decimal CalculateFine(DateTime asOfDate)
		{
			DateTime endDate = ReturnDate ?? asOfDate.Date;
			int lateDays = Math.Max(0, (endDate - ReturnDeadline).Days);
			return lateDays * 0.50m;
		}

		public void ReturnBook(DateTime returnDate)
		{
			if (ReturnDate.HasValue)
				throw new InvalidOperationException("Raamat on juba tagastatud.");
			if (returnDate.Date < BorrowingDate || returnDate.Date > DateTime.Today)
				throw new ArgumentException("Tagastamine peab olema laenutuse ja tänase päeva vahel.");
			ReturnDate = returnDate.Date;
		}

		// Õppeprogrammis on meeldetuletuse saatmine asendatud konsooliteatega.
		// Meetodit tuleb käivitada regulaarselt, näiteks kord päevas.
		public void SendReminder(DateTime asOfDate)
		{
			if (!ReturnDate.HasValue && !ReminderSent && asOfDate.Date >= ReturnDeadline)
			{
				Console.WriteLine("Meeldetuletus aadressile " + User.Email +
					": palun tagasta „" + Book.Title + "”. Tähtaeg: " +
					ReturnDeadline.ToString("dd.MM.yyyy"));
				ReminderSent = true;
			}
		}
	}

	internal class Program
	{
		private static void Main()
		{
			Console.OutputEncoding = Encoding.UTF8;
			try
			{
				Book book = new Book("Kevade", "Oskar Luts", "9789949012345", 1912, 240);
				User user = new User("Mari", "Tamm", "mari@example.com", "51234567",
					1, DateTime.Today.AddMonths(-1));

				// Laenutame minevikukuupäevaga, et näidata hilinemist ja trahvi.
				Borrowing borrowing = new Borrowing(book, user, DateTime.Today.AddDays(-17));
				Console.WriteLine("Raamat: " + book.Title);
				Console.WriteLine("Laenutaja: " + user.FirstName + " " + user.LastName);
				Console.WriteLine("Tagastamise tähtaeg: " + borrowing.ReturnDeadline.ToString("dd.MM.yyyy"));
				Console.WriteLine("Raamat saadaval: " + book.IsAvailable);
				borrowing.SendReminder(DateTime.Today);
				borrowing.ReturnBook(DateTime.Today);
				Console.WriteLine("Trahv: " + borrowing.CalculateFine(DateTime.Today).ToString("0.00") + " €");
				Console.WriteLine("Raamat pärast tagastamist saadaval: " + book.IsAvailable);
				Console.WriteLine("Aktiivseid laenutusi: " + user.ActiveBorrowingCount);
			}
			catch (Exception exception)
			{
				Console.WriteLine("Viga: " + exception.Message);
			}
		}
	}
}
