using Microsoft.Playwright;

namespace Dashboard.Integration.Tests;

/// <summary>
/// E2E tests for calendar-related functionality.
/// </summary>
[Collection("E2E Tests")]
[Trait("Category", "E2E")]
public sealed class CalendarE2ETests
{
    private readonly E2EFixture _fixture;

    /// <summary>
    /// Constructor receives shared fixture.
    /// </summary>
    public CalendarE2ETests(E2EFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Calendar page displays appointments in calendar grid.
    /// </summary>
    [Fact]
    public async Task CalendarPage_DisplaysAppointmentsInCalendarGrid()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#calendar");
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.WaitForSelectorAsync(
            ".calendar-grid-container",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("calendar-grid", content);
        Assert.Contains("Sun", content);
        Assert.Contains("Mon", content);
        Assert.Contains("Today", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Calendar page allows clicking on a day to view appointments.
    /// </summary>
    [Fact]
    public async Task CalendarPage_ClickOnDay_ShowsAppointmentDetails()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var today = DateTime.Now;
        var startTime = new DateTime(
            today.Year,
            today.Month,
            today.Day,
            14,
            0,
            0,
            DateTimeKind.Local
        ).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var endTime = new DateTime(
            today.Year,
            today.Month,
            today.Day,
            14,
            30,
            0,
            DateTimeKind.Local
        ).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var uniqueServiceType = $"CalTest{DateTime.Now.Ticks % 100000}";

        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Appointment",
            new StringContent(
                $$$"""{"ServiceCategory": "General", "ServiceType": "{{{uniqueServiceType}}}", "Priority": "routine", "Start": "{{{startTime}}}", "End": "{{{endTime}}}", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Schedule");
        await page.WaitForSelectorAsync(
            ".calendar-grid",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.WaitForSelectorAsync(
            ".calendar-cell.today.has-appointments",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var todayCell = page.Locator(".calendar-cell.today").First;
        await todayCell.ClickAsync();

        await page.WaitForSelectorAsync(
            ".calendar-details-panel h4",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );
        await page.WaitForSelectorAsync(
            $"text={uniqueServiceType}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains(uniqueServiceType, content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Calendar page Edit button opens edit appointment page.
    /// </summary>
    [Fact]
    public async Task CalendarPage_EditButton_OpensEditAppointmentPage()
    {
        using var client = E2EFixture.CreateAuthenticatedClient();

        var today = DateTime.Now;
        var startTime = new DateTime(
            today.Year,
            today.Month,
            today.Day,
            15,
            0,
            0,
            DateTimeKind.Local
        ).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var endTime = new DateTime(
            today.Year,
            today.Month,
            today.Day,
            15,
            30,
            0,
            DateTimeKind.Local
        ).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var uniqueServiceType = $"CalEdit{DateTime.Now.Ticks % 100000}";

        var createResponse = await client.PostAsync(
            $"{E2EFixture.SchedulingUrl}/Appointment",
            new StringContent(
                $$$"""{"ServiceCategory": "General", "ServiceType": "{{{uniqueServiceType}}}", "Priority": "routine", "Start": "{{{startTime}}}", "End": "{{{endTime}}}", "PatientReference": "Patient/1", "PractitionerReference": "Practitioner/1"}""",
                System.Text.Encoding.UTF8,
                "application/json"
            )
        );
        createResponse.EnsureSuccessStatusCode();

        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Schedule");
        await page.WaitForSelectorAsync(
            ".calendar-grid",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );
        await page.WaitForSelectorAsync(
            ".calendar-cell.today.has-appointments",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var todayCell = page.Locator(".calendar-cell.today").First;
        await todayCell.ClickAsync();
        await page.WaitForSelectorAsync(
            $"text={uniqueServiceType}",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var editButton = await page.QuerySelectorAsync(
            $".calendar-appointment-item:has-text('{uniqueServiceType}') button:has-text('Edit')"
        );
        Assert.NotNull(editButton);
        await editButton.ClickAsync();

        await page.WaitForSelectorAsync(
            "text=Edit Appointment",
            new PageWaitForSelectorOptions { Timeout = 5000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Edit Appointment", content);

        await page.CloseAsync();
    }

    /// <summary>
    /// Calendar navigation (previous/next month) works.
    /// </summary>
    [Fact]
    public async Task CalendarPage_NavigationButtons_ChangeMonth()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync(E2EFixture.DashboardUrl);
        await page.WaitForSelectorAsync(
            ".sidebar",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );
        await page.ClickAsync("text=Schedule");
        await page.WaitForSelectorAsync(
            ".calendar-grid",
            new PageWaitForSelectorOptions { Timeout = 10000 }
        );

        var currentMonthYear = await page.TextContentAsync(".text-lg.font-semibold");
        Assert.NotNull(currentMonthYear);

        var headerControls = page.Locator(".page-header .flex.items-center.gap-4");
        var nextButton = headerControls.Locator("button.btn-secondary").Nth(1);
        await nextButton.ClickAsync();
        await Task.Delay(300);

        var newMonthYear = await page.TextContentAsync(".text-lg.font-semibold");
        Assert.NotEqual(currentMonthYear, newMonthYear);

        var prevButton = headerControls.Locator("button.btn-secondary").First;
        await prevButton.ClickAsync();
        await Task.Delay(300);
        await prevButton.ClickAsync();
        await Task.Delay(300);

        await page.ClickAsync("button:has-text('Today')");
        await Task.Delay(500);

        var todayContent = await page.ContentAsync();
        Assert.Contains("today", todayContent);

        await page.CloseAsync();
    }

    /// <summary>
    /// Deep linking to calendar page works.
    /// </summary>
    [Fact]
    public async Task CalendarPage_DeepLinkingWorks()
    {
        var page = await _fixture.Browser!.NewPageAsync();
        page.Console += (_, msg) => Console.WriteLine($"[BROWSER] {msg.Text}");

        await page.GotoAsync($"{E2EFixture.DashboardUrl}#calendar");
        await page.WaitForSelectorAsync(
            ".calendar-grid",
            new PageWaitForSelectorOptions { Timeout = 20000 }
        );

        var content = await page.ContentAsync();
        Assert.Contains("Schedule", content);
        Assert.Contains("calendar-grid", content);

        await page.CloseAsync();
    }
}
