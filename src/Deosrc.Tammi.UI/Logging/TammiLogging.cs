using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Deosrc.Tammi.Core;
using Deosrc.Tammi.Core.Logging;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using SerilogLogger = Serilog.Core.Logger;

namespace Deosrc.Tammi.UI.Logging;

/// <summary>
/// Sets up Tammi's logging: through <c>Microsoft.Extensions.Logging</c> to Serilog, which writes to the console and
/// to daily log files in <see cref="IAppPaths.LogDirectory"/>.
/// </summary>
/// <remarks>
/// Which messages are logged is decided by the <see cref="LogLevelSwitch"/>, which follows the log level in the
/// General settings, so a change applies straight away to every logger. Serilog itself writes everything it's given.
/// </remarks>
public static class TammiLogging
{
    /// <summary>
    /// The category of Tammi's own app-level messages, such as startup and unhandled exceptions.
    /// </summary>
    public const string AppCategory = "Deosrc.Tammi";

    /// <summary>
    /// How many daily log files are kept.
    /// </summary>
    public const int RetainedFileCount = 7;

    /// <summary>
    /// The largest a log file gets before the next one is started.
    /// </summary>
    public const long FileSizeLimitBytes = 10 * 1024 * 1024;

    private const string FileOutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private const string ConsoleOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Replaces any logging providers already registered with Tammi's.
    /// </summary>
    public static IServiceCollection AddTammiLogging(this IServiceCollection services)
    {
        var levelSwitch = new LogLevelSwitch();
        services.AddSingleton(levelSwitch);
        services.AddSingleton(provider => CreateSerilogLogger(provider.GetRequiredService<IAppPaths>()));
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddFilter((_, category, level) => levelSwitch.IsEnabled(category, level));
            logging.Services.AddSingleton<ILoggerProvider>(provider =>
                new SerilogLoggerProvider(provider.GetRequiredService<SerilogLogger>()));
        });
        return services;
    }

    /// <summary>
    /// Starts logging once the app's services are built: logs Tammi's startup and any unhandled exceptions.
    /// </summary>
    public static void UseTammiLogging(this IServiceProvider services)
    {
        var appPaths = services.GetRequiredService<IAppPaths>();
        var settingsStore = services.GetRequiredService<ISettingsStore>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(AppCategory);

        // The level follows the settings.
        var levelSwitch = services.GetRequiredService<LogLevelSwitch>();
        levelSwitch.MinimumLevel = settingsStore.Current.General.LogLevel;
        settingsStore.Changed += () =>
        {
            var level = settingsStore.Current.General.LogLevel;
            if (levelSwitch.MinimumLevel != level)
            {
                levelSwitch.MinimumLevel = level;
                logger.Log(level, "Log level changed to {LogLevel}.", level);
            }
        };

        logger.LogInformation(
            "Tammi {Version} started on {OS} ({Runtime}). Logs are written to {LogDirectory}.",
            AppInfo.Version,
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            appPaths.LogDirectory);
        if (settingsStore.LoadProblem is { } loadProblem)
        {
            logger.LogWarning("{Problem}", loadProblem);
        }

        var serilogLogger = services.GetRequiredService<SerilogLogger>();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            LogUnhandled(logger, e.ExceptionObject);
            if (e.IsTerminating)
            {
                // Nothing else will write out the log before the process ends.
                serilogLogger.Dispose();
            }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
            logger.LogError(e.Exception, "A background task failed and nothing observed its exception.");
    }

    /// <summary>
    /// Logs that Tammi is stopping, and writes out any messages not yet written.
    /// </summary>
    public static void StopTammiLogging(this IServiceProvider services)
    {
        services.GetRequiredService<ILoggerFactory>().CreateLogger(AppCategory).LogInformation("Tammi stopped.");
        services.GetRequiredService<SerilogLogger>().Dispose();
    }

    private static void LogUnhandled(ILogger logger, object exceptionObject)
    {
        if (exceptionObject is Exception exception)
        {
            logger.LogCritical(exception, "Unhandled exception.");
        }
        else
        {
            logger.LogCritical("Unhandled exception: {Exception}", exceptionObject);
        }
    }

    private static SerilogLogger CreateSerilogLogger(IAppPaths appPaths)
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console(outputTemplate: ConsoleOutputTemplate, formatProvider: null);

        string? fileProblem = null;
        try
        {
            Directory.CreateDirectory(appPaths.LogDirectory);
            configuration.WriteTo.File(
                Path.Combine(appPaths.LogDirectory, "tammi-.log"),
                outputTemplate: FileOutputTemplate,
                formatProvider: null,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCount,
                fileSizeLimitBytes: FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                // Another copy of Tammi (e.g. a development build) can write to the same file.
                shared: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            fileProblem = $"Couldn't create the log folder '{appPaths.LogDirectory}', so logs only go to the "
                + $"console. {ex.Message}";
        }

        var logger = configuration.CreateLogger();
        if (fileProblem is not null)
        {
            logger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, AppCategory).Warning(fileProblem);
        }

        return logger;
    }
}
