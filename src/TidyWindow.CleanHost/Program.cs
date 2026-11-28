// The elevated half of TSUGU. All behavior lives in TSUGU.Core so it can be tested without a permission prompt.
return args.Length > 0 && args[0] == TSUGU.Core.RepairHost.Command
    ? await TSUGU.Core.RepairHost.RunAsync(args)
    : args.Length > 0 && args[0] == TSUGU.Core.StartupHost.Command
    ? await TSUGU.Core.StartupHost.RunAsync(args)
    : args.Length > 0 && args[0] == TSUGU.Core.BackgroundHost.Command
    ? await TSUGU.Core.BackgroundHost.RunAsync(args)
    : args.Length > 0 && args[0] == TSUGU.Core.FixHost.Command
    ? await TSUGU.Core.FixHost.RunAsync(args)
    : args.Length > 0 && args[0] == TSUGU.Core.TuneHost.Command
    ? await TSUGU.Core.TuneHost.RunAsync(args)
    : args.Length > 0 && args[0] == TSUGU.Core.MachinePathHost.Command
    ? await TSUGU.Core.MachinePathHost.RunAsync(args)
    : args.Length > 0 && args[0] == TSUGU.Core.FontHost.Command
    ? await TSUGU.Core.FontHost.RunAsync(args)
    : await TSUGU.Core.SystemCleanHost.RunAsync(args);
