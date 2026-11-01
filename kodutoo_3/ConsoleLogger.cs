namespace kodutoo_3;

public class ConsoleLogger : BaseLogger
{
	public override void Debug(string message)
	{
		Console.WriteLine($"DEBUG: {message}");
	}

	public override void Info(string message)
	{
		Console.WriteLine($"INFO: {message}");
	}

	public override void Warning(string message)
	{
		Console.WriteLine($"WARNING: {message}");
	}

	public override void Error(string message)
	{
		Console.WriteLine($"ERROR: {message}");
	}
}