using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.CLI.Lib;

namespace LenovoLegionToolkit.CLI;

public class Program
{
    public static Task<int> Main(string[] args) => BuildCommandLine().Parse(args).InvokeAsync();

    private static RootCommand BuildCommandLine()
    {
        var root = new RootCommand("Utility that controls Lenovo Legion Toolkit from command line.\n\n" +
                                   "Lenovo Legion Toolkit must be running in the background and CLI setting must be " +
                                   "turned on for this utility to work.");

        root.Subcommands.Add(BuildQuickActionsCommand());
        root.Subcommands.Add(BuildFeatureCommand());
        root.Subcommands.Add(BuildSpectrumCommand());
        root.Subcommands.Add(BuildRGBCommand());

        return root;
    }

    private static Command BuildQuickActionsCommand()
    {
        var nameArgument = new Argument<string?>("name") { Description = "Name of the Quick Action", Arity = ArgumentArity.ZeroOrOne };

        var listOption = new Option<bool>("--list", "-l") { Description = "List available Quick Actions" };

        var cmd = new Command("quickAction", "Run Quick Action");
        cmd.Aliases.Add("qa");
        cmd.Arguments.Add(nameArgument);
        cmd.Options.Add(listOption);
        cmd.SetAction(Handle(async result =>
        {
            if (result.GetValue(listOption))
            {
                var value = await IpcClient.ListQuickActionsAsync().ConfigureAwait(false);
                Console.WriteLine(value);
                return 0;
            }

            var name = result.GetValue(nameArgument);
            if (string.IsNullOrEmpty(name))
                return Error($"{nameArgument.Name} or --list should be specified");

            await IpcClient.RunQuickActionAsync(name).ConfigureAwait(false);
            return 0;
        }));

        return cmd;
    }

    private static Command BuildFeatureCommand()
    {
        var getCmd = BuildGetFeatureCommand();
        var setCmd = BuildSetFeatureCommand();

        var listOption = new Option<bool>("--list", "-l") { Description = "List available features" };

        var cmd = new Command("feature", "Control features");
        cmd.Aliases.Add("f");
        cmd.Subcommands.Add(getCmd);
        cmd.Subcommands.Add(setCmd);
        cmd.Options.Add(listOption);
        cmd.SetAction(Handle(async result =>
        {
            if (!result.GetValue(listOption))
                return Error($"{getCmd.Name}, {setCmd.Name} or --list should be specified");

            var value = await IpcClient.ListFeaturesAsync().ConfigureAwait(false);
            Console.WriteLine(value);
            return 0;
        }));

        return cmd;
    }

    private static Command BuildGetFeatureCommand()
    {
        var nameArgument = new Argument<string>("name") { Description = "Name of the feature", Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("get", "Get value of a feature");
        cmd.Aliases.Add("g");
        cmd.Arguments.Add(nameArgument);
        cmd.SetAction(Handle(async result =>
        {
            var name = result.GetValue(nameArgument) ?? string.Empty;
            var value = await IpcClient.GetFeatureValueAsync(name).ConfigureAwait(false);
            Console.WriteLine(value);
            return 0;
        }));

        return cmd;
    }

    private static Command BuildSetFeatureCommand()
    {
        var nameArgument = new Argument<string?>("name") { Description = "Name of the feature", Arity = ArgumentArity.ZeroOrOne };
        var valueArgument = new Argument<string?>("value") { Description = "Value of the feature", Arity = ArgumentArity.ZeroOrOne };

        var listOption = new Option<bool>("--list", "-l") { Description = "List available feature values" };

        var cmd = new Command("set", "Set value of a feature");
        cmd.Aliases.Add("s");
        cmd.Arguments.Add(nameArgument);
        cmd.Arguments.Add(valueArgument);
        cmd.Options.Add(listOption);
        cmd.SetAction(Handle(async result =>
        {
            var name = result.GetValue(nameArgument);
            if (string.IsNullOrEmpty(name))
                return Error($"{nameArgument.Name} or --list should be specified");

            if (result.GetValue(listOption))
            {
                var value = await IpcClient.ListFeatureValuesAsync(name).ConfigureAwait(false);
                Console.WriteLine(value);
                return 0;
            }

            await IpcClient.SetFeatureValueAsync(name, result.GetValue(valueArgument)).ConfigureAwait(false);
            return 0;
        }));

        return cmd;
    }

    private static Command BuildSpectrumCommand()
    {
        var profileCommand = BuildSpectrumProfileCommand();
        var brightnessCommand = BuildSpectrumBrightnessCommand();

        var cmd = new Command("spectrum", "Control Spectrum backlight");
        cmd.Aliases.Add("s");
        cmd.Subcommands.Add(profileCommand);
        cmd.Subcommands.Add(brightnessCommand);
        return cmd;
    }

    private static Command BuildSpectrumProfileCommand()
    {
        var getCmd = BuildGetSpectrumProfileCommand();
        var setCmd = BuildSetSpectrumProfileCommand();

        var cmd = new Command("profile", "Control Spectrum backlight profile");
        cmd.Aliases.Add("p");
        cmd.Subcommands.Add(getCmd);
        cmd.Subcommands.Add(setCmd);

        return cmd;
    }

    private static Command BuildGetSpectrumProfileCommand()
    {
        var cmd = new Command("get", "Get current Spectrum profile");
        cmd.Aliases.Add("g");
        cmd.SetAction(Handle(async _ =>
        {
            var value = await IpcClient.GetSpectrumProfileAsync().ConfigureAwait(false);
            Console.WriteLine(value);
            return 0;
        }));

        return cmd;
    }

    private static Command BuildSetSpectrumProfileCommand()
    {
        var valueArgument = new Argument<int>("profile") { Description = "Profile to set", Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("set", "Set current Spectrum profile");
        cmd.Aliases.Add("s");
        cmd.Arguments.Add(valueArgument);
        cmd.SetAction(Handle(async result =>
        {
            await IpcClient.SetSpectrumProfileAsync($"{result.GetValue(valueArgument)}").ConfigureAwait(false);
            return 0;
        }));

        return cmd;
    }

    private static Command BuildSpectrumBrightnessCommand()
    {
        var getCmd = BuildGetSpectrumBrightnessCommand();
        var setCmd = BuildSetSpectrumBrightnessCommand();

        var cmd = new Command("brightness", "Control Spectrum brightness");
        cmd.Aliases.Add("b");
        cmd.Subcommands.Add(getCmd);
        cmd.Subcommands.Add(setCmd);

        return cmd;
    }

    private static Command BuildGetSpectrumBrightnessCommand()
    {
        var cmd = new Command("get", "Get current Spectrum brightness");
        cmd.Aliases.Add("g");
        cmd.SetAction(Handle(async _ =>
        {
            var value = await IpcClient.GetSpectrumBrightnessAsync().ConfigureAwait(false);
            Console.WriteLine(value);
            return 0;
        }));

        return cmd;
    }

    private static Command BuildSetSpectrumBrightnessCommand()
    {
        var valueArgument = new Argument<int>("brightness") { Description = "Brightness to set", Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("set", "Set current Spectrum brightness");
        cmd.Aliases.Add("s");
        cmd.Arguments.Add(valueArgument);
        cmd.SetAction(Handle(async result =>
        {
            await IpcClient.SetSpectrumBrightnessAsync($"{result.GetValue(valueArgument)}").ConfigureAwait(false);
            return 0;
        }));

        return cmd;
    }

    private static Command BuildRGBCommand()
    {
        var getCmd = BuildGetRGBCommand();
        var setCmd = BuildSetRGBCommand();

        var cmd = new Command("rgb", "Control RGB backlight preset");
        cmd.Aliases.Add("r");
        cmd.Subcommands.Add(getCmd);
        cmd.Subcommands.Add(setCmd);

        return cmd;
    }

    private static Command BuildGetRGBCommand()
    {
        var cmd = new Command("get", "Get current RGB preset");
        cmd.Aliases.Add("g");
        cmd.SetAction(Handle(async _ =>
        {
            var value = await IpcClient.GetRGBPresetAsync().ConfigureAwait(false);
            Console.WriteLine(value);
            return 0;
        }));

        return cmd;
    }

    private static Command BuildSetRGBCommand()
    {
        var valueArgument = new Argument<int>("preset") { Description = "Preset to set", Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("set", "Set current RGB preset");
        cmd.Aliases.Add("s");
        cmd.Arguments.Add(valueArgument);
        cmd.SetAction(Handle(async result =>
        {
            await IpcClient.SetRGBPresetAsync($"{result.GetValue(valueArgument)}").ConfigureAwait(false);
            return 0;
        }));

        return cmd;
    }

    private static Func<ParseResult, CancellationToken, Task<int>> Handle(Func<ParseResult, Task<int>> action) =>
        async (result, _) =>
        {
            try
            {
                return await action(result).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return OnException(ex);
            }
        };

    private static int Error(string message)
    {
        WriteErrorLine(message);
        return 1;
    }

    private static int OnException(Exception ex)
    {
        var message = ex switch
        {
            IpcConnectException => "Failed to connect. " +
                                   "Make sure that Lenovo Legion Toolkit is running " +
                                   "in background and CLI is enabled in Settings.",
            IpcException => ex.Message,
            _ => ex.ToString()
        };
        var exitCode = ex switch
        {
            IpcConnectException => -1,
            IpcException => -2,
            _ => -99
        };

        WriteErrorLine(message);

        return exitCode;
    }

    private static void WriteErrorLine(string message)
    {
        if (!Console.IsOutputRedirected)
        {
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
        }

        Console.Error.WriteLine(message);

        if (!Console.IsOutputRedirected)
            Console.ResetColor();
    }
}
