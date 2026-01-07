using DDCSwitch;
using Spectre.Console;

if (args.Length == 0)
{
    ShowUsage();
    return 1;
}

var command = args[0].ToLowerInvariant();

try
{
    return command switch
    {
        "list" or "ls" => ListMonitors(),
        "get" => GetCurrentInput(args),
        "set" => SetInput(args),
        "help" or "-h" or "--help" or "/?" => ShowUsage(),
        _ => InvalidCommand(args[0])
    };
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
    return 1;
}

static int ShowUsage()
{
    AnsiConsole.Write(new FigletText("DDCSwitch").Color(Color.Blue));
    AnsiConsole.MarkupLine("[dim]Windows DDC/CI Monitor Input Switcher[/]\n");

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Command")
        .AddColumn("Description")
        .AddColumn("Example");

    table.AddRow(
        "[yellow]list[/] or [yellow]ls[/]",
        "List all DDC/CI capable monitors",
        "[dim]DDCSwitch list[/]");

    table.AddRow(
        "[yellow]get[/] <monitor>",
        "Get current input source for a monitor",
        "[dim]DDCSwitch get 0[/]");

    table.AddRow(
        "[yellow]set[/] <monitor> <input>",
        "Set input source for a monitor",
        "[dim]DDCSwitch set 0 HDMI1[/]");

    AnsiConsole.Write(table);

    AnsiConsole.MarkupLine("\n[bold]Monitor:[/] Monitor index (0, 1, 2...) or name pattern");
    AnsiConsole.MarkupLine("[bold]Input:[/] Input name (HDMI1, HDMI2, DP1, DP2, DVI1, VGA1) or hex code (0x11)");

    return 0;
}

static int InvalidCommand(string command)
{
    AnsiConsole.MarkupLine($"[red]Unknown command:[/] {command}");
    AnsiConsole.MarkupLine("Run [yellow]DDCSwitch help[/] for usage information.");
    return 1;
}

static int ListMonitors()
{
    AnsiConsole.Status()
        .Start("Enumerating monitors...", ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);
            Thread.Sleep(100); // Brief pause for visual feedback
        });

    var monitors = MonitorController.EnumerateMonitors();

    if (monitors.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No DDC/CI capable monitors found.[/]");
        return 1;
    }

    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("Index")
        .AddColumn("Monitor Name")
        .AddColumn("Device")
        .AddColumn("Current Input")
        .AddColumn("Status");

    foreach (var monitor in monitors)
    {
        string inputInfo = "N/A";
        string status = "[green]OK[/]";

        try
        {
            if (monitor.TryGetInputSource(out uint current, out uint max))
            {
                inputInfo = $"{InputSource.GetName(current)} (0x{current:X2})";
            }
            else
            {
                status = "[yellow]No DDC/CI[/]";
            }
        }
        catch
        {
            status = "[red]Error[/]";
        }

        table.AddRow(
            monitor.IsPrimary ? $"{monitor.Index} [yellow]*[/]" : monitor.Index.ToString(),
            monitor.Name,
            monitor.DeviceName,
            inputInfo,
            status);
    }

    AnsiConsole.Write(table);

    // Cleanup
    foreach (var monitor in monitors)
    {
        monitor.Dispose();
    }

    return 0;
}

static int GetCurrentInput(string[] args)
{
    if (args.Length < 2)
    {
        AnsiConsole.MarkupLine("[red]Error:[/] Monitor identifier required.");
        AnsiConsole.MarkupLine("Usage: [yellow]DDCSwitch get <monitor>[/]");
        return 1;
    }

    var monitors = MonitorController.EnumerateMonitors();

    if (monitors.Count == 0)
    {
        AnsiConsole.MarkupLine("[red]Error:[/] No DDC/CI capable monitors found.");
        return 1;
    }

    var monitor = MonitorController.FindMonitor(monitors, args[1]);

    if (monitor == null)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] Monitor '{args[1]}' not found.");
        AnsiConsole.MarkupLine("Use [yellow]DDCSwitch list[/] to see available monitors.");

        // Cleanup
        foreach (var m in monitors)
        {
            m.Dispose();
        }

        return 1;
    }

    if (!monitor.TryGetInputSource(out uint current, out uint max))
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] Failed to get input source from monitor '{monitor.Name}'.");
        AnsiConsole.MarkupLine("The monitor may not support DDC/CI or requires administrator privileges.");

        // Cleanup
        foreach (var m in monitors)
        {
            m.Dispose();
        }

        return 1;
    }

    AnsiConsole.MarkupLine($"[green]Monitor:[/] {monitor.Name} ({monitor.DeviceName})");
    AnsiConsole.MarkupLine($"[green]Current Input:[/] {InputSource.GetName(current)} (0x{current:X2})");

    // Cleanup
    foreach (var m in monitors)
    {
        m.Dispose();
    }

    return 0;
}

static int SetInput(string[] args)
{
    if (args.Length < 3)
    {
        AnsiConsole.MarkupLine("[red]Error:[/] Monitor and input source required.");
        AnsiConsole.MarkupLine("Usage: [yellow]DDCSwitch set <monitor> <input>[/]");
        return 1;
    }

    if (!InputSource.TryParse(args[2], out uint inputValue))
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] Invalid input source '{args[2]}'.");
        AnsiConsole.MarkupLine("Valid inputs: HDMI1, HDMI2, DP1, DP2, DVI1, DVI2, VGA1, VGA2, or hex code (0x11)");
        return 1;
    }

    var monitors = MonitorController.EnumerateMonitors();

    if (monitors.Count == 0)
    {
        AnsiConsole.MarkupLine("[red]Error:[/] No DDC/CI capable monitors found.");
        return 1;
    }

    var monitor = MonitorController.FindMonitor(monitors, args[1]);

    if (monitor == null)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] Monitor '{args[1]}' not found.");
        AnsiConsole.MarkupLine("Use [yellow]DDCSwitch list[/] to see available monitors.");

        // Cleanup
        foreach (var m in monitors)
        {
            m.Dispose();
        }

        return 1;
    }

    AnsiConsole.Status()
        .Start($"Switching {monitor.Name} to {InputSource.GetName(inputValue)}...", ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);

            if (!monitor.TrySetInputSource(inputValue))
            {
                throw new Exception(
                    $"Failed to set input source on monitor '{monitor.Name}'. The monitor may not support this input or requires administrator privileges.");
            }

            // Give the monitor a moment to switch
            Thread.Sleep(500);
        });

    AnsiConsole.MarkupLine($"[green]✓[/] Successfully switched {monitor.Name} to {InputSource.GetName(inputValue)}");

    // Cleanup
    foreach (var m in monitors)
    {
        m.Dispose();
    }

    return 0;
}