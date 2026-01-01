using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Scope = "type",
    Target = "~T:Scheduling.Api.Schedule",
    Justification = "Model for future endpoints"
)]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Scope = "type",
    Target = "~T:Scheduling.Api.Slot",
    Justification = "Model for future endpoints"
)]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Scope = "type",
    Target = "~T:Scheduling.Api.SyncedPatient",
    Justification = "Model for future endpoints"
)]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Scope = "type",
    Target = "~T:Scheduling.Api.CreatePractitionerRequest",
    Justification = "Used by minimal API model binding"
)]
[assembly: SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Scope = "type",
    Target = "~T:Scheduling.Api.CreateAppointmentRequest",
    Justification = "Used by minimal API model binding"
)]
[assembly: SuppressMessage(
    "Usage",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Schema file is trusted"
)]
[assembly: SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "Sample code"
)]
[assembly: SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "ASP.NET Core application"
)]
[assembly: SuppressMessage(
    "Performance",
    "CA1826:Do not use Enumerable methods on indexable collections",
    Justification = "Sample code"
)]
[assembly: SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "RS1035 analyzer issue"
)]
[assembly: SuppressMessage(
    "Design",
    "CA1050:Declare types in namespaces",
    Scope = "namespaceanddescendants",
    Target = "~N",
    Justification = "RS1035 analyzer issue"
)]
[assembly: SuppressMessage(
    "Reliability",
    "RS1035",
    Justification = "Sample code - not an analyzer"
)]
[assembly: SuppressMessage("Reliability", "EPC12", Justification = "Sample code")]
