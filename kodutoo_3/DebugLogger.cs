namespace kodutoo_3;

public class DebugLogger : BaseLogger
{
	public override void Debug(string message)
	{
		System.Diagnostics.Debug.WriteLine($"DEBUG: {message}");
	}

	public override void Info(string message)
	{
		System.Diagnostics.Debug.WriteLine($"INFO: {message}");
	}

	public override void Warning(string message)
	{
		System.Diagnostics.Debug.WriteLine($"WARNING: {message}");
	}

	public override void Error(string message)
	{
		System.Diagnostics.Debug.WriteLine($"ERROR: {message}");
	}
}