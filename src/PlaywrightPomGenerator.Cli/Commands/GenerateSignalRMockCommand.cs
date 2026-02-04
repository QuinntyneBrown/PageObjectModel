using System.CommandLine;
using Microsoft.Extensions.Logging;
using PlaywrightPomGenerator.Core.Abstractions;

namespace PlaywrightPomGenerator.Cli.Commands;

/// <summary>
/// Command to generate a SignalR mock fixture using RxJS.
/// </summary>
public sealed class GenerateSignalRMockCommand : Command
{
    /// <summary>
    /// Gets the output argument.
    /// </summary>
    public Argument<string> OutputArgument { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateSignalRMockCommand"/> class.
    /// </summary>
    public GenerateSignalRMockCommand()
        : base("signalr-mock", "Generate a fully functional SignalR mock fixture using RxJS")
    {
        OutputArgument = new Argument<string>("output")
        {
            Description = "Output directory for the generated SignalR mock file",
            DefaultValueFactory = _ => "."
        };

        Add(OutputArgument);
    }
}

/// <summary>
/// Handler for the generate SignalR mock command.
/// </summary>
public sealed class GenerateSignalRMockCommandHandler
{
    private readonly ICodeGenerator _generator;
    private readonly ILogger<GenerateSignalRMockCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateSignalRMockCommandHandler"/> class.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="logger">The logger.</param>
    public GenerateSignalRMockCommandHandler(
        ICodeGenerator generator,
        ILogger<GenerateSignalRMockCommandHandler> logger)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="output">The output directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exit code.</returns>
    public async Task<int> ExecuteAsync(string output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        _logger.LogInformation("Generating SignalR mock fixture at {OutputPath}", output);

        try
        {
            var result = await _generator.GenerateSignalRMockAsync(output, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success)
            {
                Console.WriteLine("Successfully generated SignalR mock fixture:");
                foreach (var file in result.GeneratedFiles)
                {
                    Console.WriteLine($"  - {file.AbsolutePath}");
                }

                Console.WriteLine();
                Console.WriteLine("The mock provides:");
                Console.WriteLine("  - RxJS-based observable streams (not promises)");
                Console.WriteLine("  - Connection state management");
                Console.WriteLine("  - Method invocation tracking");
                Console.WriteLine("  - Server message simulation");
                Console.WriteLine("  - Error simulation");
                Console.WriteLine("  - Reconnection simulation");

                return 0;
            }

            Console.Error.WriteLine("Generation failed:");
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SignalR mock fixture");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
