namespace kodutoo_3;

public abstract class BaseLogger
{
	public abstract void Debug(string message);
	public abstract void Info(string message);
	public abstract void Warning(string message);
	public abstract void Error(string message);
}