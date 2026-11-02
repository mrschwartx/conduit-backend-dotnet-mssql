namespace Conduit.Host.Configuration;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddDotEnvFile(this IConfigurationBuilder builder, string path, string[] args)
    {
        if (File.Exists(path))
        {
            builder.AddInMemoryCollection(DotEnvParser.Parse(File.ReadAllLines(path)));
        }

        builder.AddEnvironmentVariables();
        if (args.Length > 0)
        {
            builder.AddCommandLine(args);
        }

        return builder;
    }
}
