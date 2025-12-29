using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// E2E tests for appointment-related functionality.
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class AppointmentE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public AppointmentE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Dashboard loads and displays appointment data from Scheduling API.
    /// </summary>
    [Fact]
    public async Task Dashboard_DisplaysAppointmentData_FromSchedulingApi()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            "text=Checkup",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Checkup", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Add Appointment button opens modal and creates appointment via API.
    /// </summary>
    [Fact]
    public async Task AddAppointmentButton_OpensModal_AndCreatesAppointment()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            "[data-testid='add-appointment-btn']",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.ClickAsync("[data-testid='add-appointment-btn']");
        await page.WaitForSelectorAsync(
            ".modal",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        var uniqueServiceType = $"E2EConsult{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("[data-testid='appointment-service-type']", uniqueServiceType);
        await page.ClickAsync("[data-testid='submit-appointment']");

        await page.WaitForSelectorAsync(
            $"text={uniqueServiceType}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        using var client = E2EFixture.CreateAuthenticatedClient();
        var response = await client.GetStringAsync($"{E2EFixture.SchedulingUrl}/Appointment");
        Assert.Contains(uniqueServiceType, response);

        await page.CloseAsync();
    }

    /// <summary>
    /// View Schedule button navigates to appointments view.
    /// </summary>
    [Fact]
    public async Task ViewScheduleButton_NavigatesToAppointments()
    {
        var page = await _fixture.Browser!.NewPageAsync();

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=View Schedule");
        await page.WaitForSelectorAsync(
            "text=Appointments",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        await page.WaitForSelectorAsync(
            "text=Checkup",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Checkup", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Edit Appointment button opens edit page and updates appointment via API.
    /// </summary>
    [Fact]
    public async Task EditAppointmentButton_OpensEditPage_AndUpdatesAppointment()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var uniqueServiceType = $"EditApptTest{DateTime.UtcNow.Ticks % 100000}";
        var startTime = DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var endTime = DateTime
            .UtcNow.AddDays(7)
            .AddMinutes(30)
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Appointment",
            new StringContent(
                $$$"""{"ServiceCategory": "General", "ServiceType": "{{{uniqueServiceType}}}", "Priority": "routine", "Start": "{{{startTime}}}", "End": "{{{endTime}}}", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();
        var createdAppointmentJson = await createResponse.Content.ReadAsStringAsync();

        var appointmentIdMatch = Regex.Match(createdAppointmentJson, "\"Id\"\\s*:\\s*\"([^\"]+)\"");
        Assert.True(appointmentIdMatch.Success);
        var appointmentId = appointmentIdMatch.Groups[1].Value;

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Appointments");
        await page.WaitForSelectorAsync(
            $"text={uniqueServiceType}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var editButton = await page.QuerySelectorAsync(
            $"tr:has-text('{uniqueServiceType}') .btn-secondary"
        );
        Assert.NotNull(editButton);
        await editButton.ClickAsync();

        await page.WaitForSelectorAsync(
            "text=Edit Appointment",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        var newServiceType = $"Edited{DateTime.UtcNow.Ticks % 100000}";
        await page.FillAsync("#appointment-service-type", newServiceType);
        await page.ClickAsync("button:has-text('Save Changes')");

        await page.WaitForSelectorAsync(
            "text=Appointment updated successfully",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var updatedAppointmentJson = await client.GetStringAsync(
            $"{E2EFixture.SchedulingUrl}/Appointment/{appointmentId}"
        );
        Assert.Contains(newServiceType, updatedAppointmentJson);

        await page.CloseAsync();
    }
}
