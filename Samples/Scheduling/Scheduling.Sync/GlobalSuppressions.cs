using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1848:Use LoggerMessage delegates",
    Justification = "Sample code"
)]
[assembly: SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "Background service"
)]
[assembly: SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance methods preferred"
)]
[assembly: SuppressMessage(
    "Design",
    "CA1050:Declare types in namespaces",
    Justification = "Top-level program"
)]
[assembly: SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "Sample code"
)]
[assembly: SuppressMessage(
    "Usage",
    "CA2234:Pass system uri objects instead of strings",
    Justification = "Sample code"
)]
[assembly: SuppressMessage(
    "Globalization",
    "CA1305:Specify IFormatProvider",
    Justification = "Sample code"
)]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Scope = "type",
    Target = "~T:Scheduling.Sync.SchedulingSyncWorker",
    Justification = "Instantiated by DI framework"
)]
