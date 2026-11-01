using kodutoo_3;
using kodutoo_3;

Console.OutputEncoding = System.Text.Encoding.UTF8;

BaseLogger[] loggers =
{
	new NullLoggerAdapter(new NullLogger()),
	new DebugLogger(),
	new ConsoleLogger()
};

foreach (BaseLogger logger in loggers)
{
	Console.WriteLine(logger.ToString());

	logger.Debug("See on silumisteade.");
	logger.Info("Programm töötab.");
	logger.Warning("See on hoiatus.");
	logger.Error("See on veateade.");

	Console.WriteLine();
}