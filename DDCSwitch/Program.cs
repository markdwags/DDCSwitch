using DDCSwitch;
using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Serialization;

return DDCSwitchProgram.Run(args);

static class DDCSwitchProgram
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = JsonContext.Default
    };

    public static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        // Check for --json flag
        bool jsonOutput = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var filteredArgs = args.Where(a => !a.Equals("--json", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (filteredArgs.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        var command = filteredArgs[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "list" or "ls" => ListMonitors(jsonOutput),
                "get" => GetCurrentInput(filteredArgs, jsonOutput),
                "set" => SetInput(filteredArgs, jsonOutput),
                "version" or "-v" or "--version" => ShowVersion(jsonOutput),
                "help" or "-h" or "--help" or "/?" => ShowUsage(),
                _ => InvalidCommand(filteredArgs[0], jsonOutput)
            };
        }
        catch (Exception ex)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, ex.Message);
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }

            return 1;
        }
    }

    private static string GetVersion()
    {
        var version = typeof(DDCSwitchProgram).Assembly
            .GetName().Version?.ToString(3) ?? "0.0.0";
        return version;
    }

    private static int ShowVersion(bool jsonOutput)
    {
        var version = GetVersion();

        if (jsonOutput)
        {
            Console.WriteLine($"{{\"version\":\"{version}\"}}");
        }
        else
        {
            AnsiConsole.Write(new FigletText("DDCSwitch").Color(Color.Blue));
            AnsiConsole.MarkupLine($"[bold]Version:[/] [green]{version}[/]");
            AnsiConsole.MarkupLine("[dim]Windows DDC/CI Monitor Input Switcher[/]");
        }

        return 0;
    }

    private static int ShowUsage()
    {
        var version = GetVersion();

        AnsiConsole.Write(new FigletText("DDCSwitch").Color(Color.Blue));
        AnsiConsole.MarkupLine($"[dim]Windows DDC/CI Monitor Input Switcher v{version}[/]\n");

        AnsiConsole.MarkupLine("[yellow]Commands:[/]");
        AnsiConsole.MarkupLine("  list - List all DDC/CI capable monitors");
        AnsiConsole.MarkupLine("  get monitor feature - Get current value for a monitor feature");
        AnsiConsole.MarkupLine("  set monitor feature value - Set value for a monitor feature");
        AnsiConsole.MarkupLine("  version - Display version information");
        
        AnsiConsole.MarkupLine("\nSupported features: brightness, contrast, input, or VCP codes like 0x10");
        AnsiConsole.MarkupLine("Use --json flag for JSON output");

        return 0;
    }

    private static int InvalidCommand(string command, bool jsonOutput)
    {
        if (jsonOutput)
        {
            var error = new ErrorResponse(false, $"Unknown command: {command}");
            Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Unknown command:[/] {command}");
            AnsiConsole.MarkupLine("Run [yellow]DDCSwitch help[/] for usage information.");
        }

        return 1;
    }

    private static int ListMonitors(bool jsonOutput)
    {
        if (!jsonOutput)
        {
            AnsiConsole.Status()
                .Start("Enumerating monitors...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    Thread.Sleep(100); // Brief pause for visual feedback
                });
        }

        var monitors = MonitorController.EnumerateMonitors();

        if (monitors.Count == 0)
        {
            if (jsonOutput)
            {
                var result = new ListMonitorsResponse(false, null, "No DDC/CI capable monitors found");
                Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.ListMonitorsResponse));
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No DDC/CI capable monitors found.[/]");
            }

            return 1;
        }

        if (jsonOutput)
        {
            var monitorList = monitors.Select(monitor =>
            {
                string? inputName = null;
                uint? inputCode = null;
                string status = "ok";

                try
                {
                    if (monitor.TryGetInputSource(out uint current, out uint max))
                    {
                        inputName = InputSource.GetName(current);
                        inputCode = current;
                    }
                    else
                    {
                        status = "no_ddc_ci";
                    }
                }
                catch
                {
                    status = "error";
                }

                return new MonitorInfo(
                    monitor.Index,
                    monitor.Name,
                    monitor.DeviceName,
                    monitor.IsPrimary,
                    inputName,
                    inputCode != null ? $"0x{inputCode:X2}" : null,
                    status);
            }).ToList();

            var result = new ListMonitorsResponse(true, monitorList);
            Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.ListMonitorsResponse));
        }
        else
        {
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
        }

        // Cleanup
        foreach (var monitor in monitors)
        {
            monitor.Dispose();
        }

        return 0;
    }

    private static int GetCurrentInput(string[] args, bool jsonOutput)
    {
        if (args.Length < 3)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, "Monitor identifier and feature required");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Monitor identifier and feature required.");
                AnsiConsole.MarkupLine("Usage: [yellow]DDCSwitch get <monitor> <feature>[/]");
            }

            return 1;
        }

        string featureInput = args[2];
        
        if (!FeatureResolver.TryResolveFeature(featureInput, out VcpFeature? feature))
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, $"Invalid feature '{featureInput}'");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid feature '{featureInput}'.");
                AnsiConsole.MarkupLine("Valid features: brightness, contrast, input, or VCP code (0x10, 0x12, etc.)");
            }

            return 1;
        }

        var monitors = MonitorController.EnumerateMonitors();

        if (monitors.Count == 0)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, "No DDC/CI capable monitors found");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No DDC/CI capable monitors found.");
            }

            return 1;
        }

        var monitor = MonitorController.FindMonitor(monitors, args[1]);

        if (monitor == null)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, $"Monitor '{args[1]}' not found");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Monitor '{args[1]}' not found.");
                AnsiConsole.MarkupLine("Use [yellow]DDCSwitch list[/] to see available monitors.");
            }

            // Cleanup
            foreach (var m in monitors)
            {
                m.Dispose();
            }

            return 1;
        }

        // Use generic VCP method for all features
        bool success = monitor.TryGetVcpFeature(feature!.Code, out uint current, out uint max);

        if (!success)
        {
            if (jsonOutput)
            {
                var monitorRef =
                    new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
                var error = new ErrorResponse(false, $"Failed to get {feature.Name} from monitor '{monitor.Name}'",
                    monitorRef);
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Failed to get {feature.Name} from monitor '{monitor.Name}'.");
                AnsiConsole.MarkupLine("The monitor may not support this feature or requires administrator privileges.");
            }

            // Cleanup
            foreach (var m in monitors)
            {
                m.Dispose();
            }

            return 1;
        }

        if (jsonOutput)
        {
            var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
            
            if (feature.Code == InputSource.VcpInputSource)
            {
                // Use generic VCP response for input as well
                uint? percentageValue = feature.SupportsPercentage ? FeatureResolver.ConvertRawToPercentage(current, max) : null;
                var result = new GetVcpResponse(true, monitorRef, feature.Name, current, max, percentageValue);
                Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.GetVcpResponse));
            }
            else
            {
                // Use generic VCP response for other features
                uint? percentageValue = feature.SupportsPercentage ? FeatureResolver.ConvertRawToPercentage(current, max) : null;
                var result = new GetVcpResponse(true, monitorRef, feature.Name, current, max, percentageValue);
                Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.GetVcpResponse));
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Monitor:[/] {monitor.Name} ({monitor.DeviceName})");
            
            if (feature.Code == InputSource.VcpInputSource)
            {
                // Display input with name resolution
                AnsiConsole.MarkupLine($"[green]Current {feature.Name}:[/] {InputSource.GetName(current)} (0x{current:X2})");
            }
            else if (feature.SupportsPercentage)
            {
                // Display percentage for brightness/contrast
                uint percentage = FeatureResolver.ConvertRawToPercentage(current, max);
                AnsiConsole.MarkupLine($"[green]Current {feature.Name}:[/] {percentage}% (raw: {current}/{max})");
            }
            else
            {
                // Display raw values for unknown VCP codes
                AnsiConsole.MarkupLine($"[green]Current {feature.Name}:[/] {current} (max: {max})");
            }
        }

        // Cleanup
        foreach (var m in monitors)
        {
            m.Dispose();
        }

        return 0;
    }

    private static int SetInput(string[] args, bool jsonOutput)
    {
        // Require 4 arguments: set <monitor> <feature> <value>
        if (args.Length < 4)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, "Monitor, feature, and value required");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Monitor, feature, and value required.");
                AnsiConsole.MarkupLine("Usage: [yellow]DDCSwitch set <monitor> <feature> <value>[/]");
            }

            return 1;
        }

        string featureInput = args[2];
        string valueInput = args[3];

        if (!FeatureResolver.TryResolveFeature(featureInput, out VcpFeature? feature))
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, $"Invalid feature '{featureInput}'");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid feature '{featureInput}'.");
                AnsiConsole.MarkupLine("Valid features: brightness, contrast, input, or VCP code (0x10, 0x12, etc.)");
            }

            return 1;
        }

        // Parse the value based on feature type
        uint setValue;
        uint? percentageValue = null;
        
        if (feature!.Code == InputSource.VcpInputSource)
        {
            // Use existing input source parsing for input feature
            if (!InputSource.TryParse(valueInput, out setValue))
            {
                if (jsonOutput)
                {
                    var error = new ErrorResponse(false, $"Invalid input source '{valueInput}'");
                    Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Invalid input source '{valueInput}'.");
                    AnsiConsole.MarkupLine(
                        "Valid inputs: HDMI1, HDMI2, DP1, DP2, DVI1, DVI2, VGA1, VGA2, or hex code (0x11)");
                }

                return 1;
            }
        }
        else if (feature.SupportsPercentage && FeatureResolver.TryParsePercentage(valueInput, out uint percentage))
        {
            // Parse as percentage for brightness/contrast
            percentageValue = percentage;
            // We'll convert to raw value after getting monitor's max value
            setValue = 0; // Placeholder
        }
        else if (uint.TryParse(valueInput, out uint rawValue))
        {
            // Parse as raw value - we'll validate range after getting monitor's max value
            setValue = rawValue;
        }
        else
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, $"Invalid value '{valueInput}' for feature '{feature.Name}'");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid value '{valueInput}' for feature '{feature.Name}'.");
                if (feature.SupportsPercentage)
                {
                    AnsiConsole.MarkupLine("Valid values: 0-100% or raw numeric value");
                }
                else
                {
                    AnsiConsole.MarkupLine("Valid values: numeric value within monitor's supported range");
                }
            }

            return 1;
        }

        var monitors = MonitorController.EnumerateMonitors();

        if (monitors.Count == 0)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, "No DDC/CI capable monitors found");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No DDC/CI capable monitors found.");
            }

            return 1;
        }

        var monitor = MonitorController.FindMonitor(monitors, args[1]);

        if (monitor == null)
        {
            if (jsonOutput)
            {
                var error = new ErrorResponse(false, $"Monitor '{args[1]}' not found");
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Monitor '{args[1]}' not found.");
                AnsiConsole.MarkupLine("Use [yellow]DDCSwitch list[/] to see available monitors.");
            }

            // Cleanup
            foreach (var m in monitors)
            {
                m.Dispose();
            }

            return 1;
        }

        // If we have a percentage value, we need to get the max value first to convert it
        // For raw values, we also need to validate they're within the monitor's supported range
        if (percentageValue.HasValue || (!feature!.SupportsPercentage && feature.Code != InputSource.VcpInputSource))
        {
            if (monitor.TryGetVcpFeature(feature.Code, out uint currentValue, out uint maxValue))
            {
                if (percentageValue.HasValue)
                {
                    // Convert percentage to raw value
                    setValue = FeatureResolver.ConvertPercentageToRaw(percentageValue.Value, maxValue);
                }
                else if (feature.Code != InputSource.VcpInputSource)
                {
                    // Validate raw value is within supported range
                    if (setValue > maxValue)
                    {
                        if (jsonOutput)
                        {
                            var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
                            var error = new ErrorResponse(false, $"Value {setValue} is out of range for {feature.Name}. Valid range: 0-{maxValue}", monitorRef);
                            Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]Error:[/] Value {setValue} is out of range for {feature.Name}.");
                            AnsiConsole.MarkupLine($"Valid range: 0-{maxValue}");
                        }

                        // Cleanup
                        foreach (var m in monitors)
                        {
                            m.Dispose();
                        }

                        return 1;
                    }
                }
            }
            else
            {
                if (jsonOutput)
                {
                    var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
                    var error = new ErrorResponse(false, $"Failed to read current {feature.Name} from monitor '{monitor.Name}' to validate range", monitorRef);
                    Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Failed to read current {feature.Name} from monitor '{monitor.Name}' to validate range.");
                    AnsiConsole.MarkupLine("The monitor may not support this feature or requires administrator privileges.");
                }

                // Cleanup
                foreach (var m in monitors)
                {
                    m.Dispose();
                }

                return 1;
            }
        }

        bool success = false;
        string? errorMsg = null;

        if (!jsonOutput)
        {
            string displayValue = percentageValue.HasValue ? $"{percentageValue}%" : setValue.ToString();
            AnsiConsole.Status()
                .Start($"Setting {monitor.Name} {feature.Name} to {displayValue}...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);

                    if (!monitor.TrySetVcpFeature(feature!.Code, setValue))
                    {
                        errorMsg = $"Failed to set {feature.Name} on monitor '{monitor.Name}'. The monitor may not support this feature or requires administrator privileges.";
                    }
                    else
                    {
                        success = true;
                    }

                    if (success)
                    {
                        // Give the monitor a moment to apply the change
                        Thread.Sleep(500);
                    }
                });
        }
        else
        {
            if (!monitor.TrySetVcpFeature(feature!.Code, setValue))
            {
                errorMsg = $"Failed to set {feature.Name} on monitor '{monitor.Name}'";
            }
            else
            {
                success = true;
                Thread.Sleep(500);
            }
        }

        if (!success)
        {
            if (jsonOutput)
            {
                var monitorRef =
                    new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
                var error = new ErrorResponse(false, errorMsg!, monitorRef);
                Console.WriteLine(JsonSerializer.Serialize(error, JsonContext.Default.ErrorResponse));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {errorMsg}");
            }

            // Cleanup
            foreach (var m in monitors)
            {
                m.Dispose();
            }

            return 1;
        }

        if (jsonOutput)
        {
            var monitorRef = new MonitorReference(monitor.Index, monitor.Name, monitor.DeviceName, monitor.IsPrimary);
            
            // Use generic VCP response for all features
            var result = new SetVcpResponse(true, monitorRef, feature!.Name, setValue, percentageValue);
            Console.WriteLine(JsonSerializer.Serialize(result, JsonContext.Default.SetVcpResponse));
        }
        else
        {
            if (feature!.Code == InputSource.VcpInputSource)
            {
                // Display input with name resolution
                AnsiConsole.MarkupLine($"[green]✓[/] Successfully set {monitor.Name} {feature.Name} to {InputSource.GetName(setValue)}");
            }
            else if (percentageValue.HasValue)
            {
                // Display percentage for brightness/contrast
                AnsiConsole.MarkupLine($"[green]✓[/] Successfully set {monitor.Name} {feature.Name} to {percentageValue}%");
            }
            else
            {
                // Display raw value for unknown VCP codes
                AnsiConsole.MarkupLine($"[green]✓[/] Successfully set {monitor.Name} {feature.Name} to {setValue}");
            }
        }

        // Cleanup
        foreach (var m in monitors)
        {
            m.Dispose();
        }

        return 0;
    }
}