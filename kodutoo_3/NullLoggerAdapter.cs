using kodutoo_3;

namespace kodutoo_3;

public class NullLoggerAdapter : BaseLogger
{
	private readonly NullLogger _logger;

	public NullLoggerAdapter(NullLogger logger)
	{
		_logger = logger;
	}

	public override void Debug(string message)
	{
		_logger.WriteDebug(message);
	}

	public override void Info(string message)
	{
		_logger.WriteInfo(message);
	}

	public override void Warning(string message)
	{
		_logger.WriteWarning(message);
	}

	public override void Error(string message)
	{
		_logger.WriteError(message);
	}
}