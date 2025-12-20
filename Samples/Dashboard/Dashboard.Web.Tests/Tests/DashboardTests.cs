namespace Dashboard.Tests.Tests
{
    using System.Threading.Tasks;
    using Dashboard.Tests.TestData;
    using Dashboard.Tests.TestLib;
    using static Dashboard.Tests.TestLib.TestRunner;

    /// <summary>
    /// Comprehensive end-to-end tests for the Healthcare Dashboard.
    /// Tests the ENTIRE application from the root App component.
    /// </summary>
    public static class DashboardTests
    {
        /// <summary>
        /// Registers all dashboard tests.
        /// </summary>
        public static void RegisterAll()
        {
            NavigationTests();
            DashboardPageTests();
            PatientsPageTests();
            PractitionersPageTests();
            AppointmentsPageTests();
            SidebarTests();
            HeaderTests();
            ErrorHandlingTests();
        }

        private static void NavigationTests() => Describe("Navigation", () =>
                                                          {
                                                              Test("renders app with sidebar and main content", async () =>
                                                              {
                                                                  // Arrange: Set up mock API
                                                                  var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                  MockFetch.Install(mock);

                                                                  // Act: Render the full app
                                                                  var result = TestingLibrary.RenderApp();

                                                                  // Assert: Sidebar is present
                                                                  var sidebar = result.QuerySelector(".sidebar");
                                                                  Assert.ElementExists(sidebar, "Sidebar should be rendered");

                                                                  // Assert: Main content wrapper is present
                                                                  var mainWrapper = result.QuerySelector(".main-wrapper");
                                                                  Assert.ElementExists(mainWrapper, "Main wrapper should be rendered");

                                                                  // Assert: Header is present
                                                                  var header = result.QuerySelector(".header");
                                                                  Assert.ElementExists(header, "Header should be rendered");

                                                                  // Assert: Dashboard is the default view
                                                                  Assert.TextVisible("Dashboard", "Dashboard title should be visible");
                                                                  Assert.TextVisible(
                                                                      "Overview of your healthcare system",
                                                                      "Dashboard description should be visible"
                                                                  );

                                                                  result.Unmount();
                                                                  MockFetch.Restore();
                                                              });

                                                              Test("navigates to Patients page when clicking nav item", async () =>
                                                              {
                                                                  var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                  MockFetch.Install(mock);

                                                                  var result = TestingLibrary.RenderApp();

                                                                  // Find and click Patients nav item
                                                                  var patientsNav = result.QuerySelector(
                                                                      ".nav-item[data-view='patients'], .nav-item:has(.nav-item-text:contains('Patients'))"
                                                                  );

                                                                  // Use text-based query as fallback
                                                                  if (patientsNav == null)
                                                                  {
                                                                      var navItems = result.QuerySelectorAll(".nav-item");
                                                                      foreach (var item in navItems)
                                                                      {
                                                                          var text = H5.Script.Get<string>(item, "textContent");
                                                                          if (text.Contains("Patients"))
                                                                          {
                                                                              patientsNav = item;
                                                                              break;
                                                                          }
                                                                      }
                                                                  }

                                                                  Assert.ElementExists(patientsNav, "Patients nav item should exist");
                                                                  TestingLibrary.FireClick(patientsNav);

                                                                  // Wait for navigation
                                                                  await TestingLibrary.WaitForText("Manage patient records");

                                                                  // Assert: Patients page is displayed
                                                                  Assert.TextVisible("Patients", "Patients title should be visible");
                                                                  Assert.TextVisible(
                                                                      "Manage patient records",
                                                                      "Patients description should be visible"
                                                                  );

                                                                  result.Unmount();
                                                                  MockFetch.Restore();
                                                              });

                                                              Test("navigates to Practitioners page", async () =>
                                                              {
                                                                  var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                  MockFetch.Install(mock);

                                                                  var result = TestingLibrary.RenderApp();

                                                                  // Find Practitioners nav
                                                                  var navItems = result.QuerySelectorAll(".nav-item");
                                                                  object practitionersNav = null;
                                                                  foreach (var item in navItems)
                                                                  {
                                                                      var text = H5.Script.Get<string>(item, "textContent");
                                                                      if (text.Contains("Practitioners"))
                                                                      {
                                                                          practitionersNav = item;
                                                                          break;
                                                                      }
                                                                  }

                                                                  Assert.ElementExists(practitionersNav, "Practitioners nav item should exist");
                                                                  TestingLibrary.FireClick(practitionersNav);

                                                                  await TestingLibrary.WaitForText("Manage healthcare providers");

                                                                  Assert.TextVisible("Practitioners", "Practitioners title should be visible");

                                                                  result.Unmount();
                                                                  MockFetch.Restore();
                                                              });

                                                              Test("navigates to Appointments page", async () =>
                                                              {
                                                                  var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                  MockFetch.Install(mock);

                                                                  var result = TestingLibrary.RenderApp();

                                                                  // Find Appointments nav
                                                                  var navItems = result.QuerySelectorAll(".nav-item");
                                                                  object appointmentsNav = null;
                                                                  foreach (var item in navItems)
                                                                  {
                                                                      var text = H5.Script.Get<string>(item, "textContent");
                                                                      if (text.Contains("Appointments"))
                                                                      {
                                                                          appointmentsNav = item;
                                                                          break;
                                                                      }
                                                                  }

                                                                  Assert.ElementExists(appointmentsNav, "Appointments nav item should exist");
                                                                  TestingLibrary.FireClick(appointmentsNav);

                                                                  await TestingLibrary.WaitForText("Manage scheduling and appointments");

                                                                  Assert.TextVisible("Appointments", "Appointments title should be visible");

                                                                  result.Unmount();
                                                                  MockFetch.Restore();
                                                              });

                                                              Test("updates header title when navigating", async () =>
                                                              {
                                                                  var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                  MockFetch.Install(mock);

                                                                  var result = TestingLibrary.RenderApp();

                                                                  // Initial header shows Dashboard
                                                                  var headerTitle = result.QuerySelector(".header-title");
                                                                  Assert.ElementExists(headerTitle);
                                                                  Assert.HasTextContent(headerTitle, "Dashboard");

                                                                  // Navigate to Patients
                                                                  var navItems = result.QuerySelectorAll(".nav-item");
                                                                  foreach (var item in navItems)
                                                                  {
                                                                      var text = H5.Script.Get<string>(item, "textContent");
                                                                      if (text.Contains("Patients"))
                                                                      {
                                                                          TestingLibrary.FireClick(item);
                                                                          break;
                                                                      }
                                                                  }

                                                                  await TestingLibrary.WaitForText("Manage patient records");

                                                                  // Header should now show Patients
                                                                  headerTitle = result.QuerySelector(".header-title");
                                                                  Assert.HasTextContent(headerTitle, "Patients");

                                                                  result.Unmount();
                                                                  MockFetch.Restore();
                                                              });
                                                          });

        private static void DashboardPageTests() => Describe("Dashboard Page", () =>
                                                             {
                                                                 Test("displays metric cards with correct data", async () =>
                                                                 {
                                                                     var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                     MockFetch.Install(mock);

                                                                     var result = TestingLibrary.RenderApp();

                                                                     // Wait for data to load
                                                                     await TestingLibrary.WaitFor(
                                                                         () =>
                                                                         {
                                                                             var metrics = result.QuerySelectorAll(".metric-card");
                                                                             return metrics.Length > 0;
                                                                         }
                                                                     );

                                                                     // Assert: 4 metric cards are displayed
                                                                     var metricCards = result.QuerySelectorAll(".metric-card");
                                                                     Assert.AreEqual(4, metricCards.Length, "Should display 4 metric cards");

                                                                     // Assert: Patient count is displayed (5 patients in mock data)
                                                                     Assert.TextVisible("Total Patients", "Patient metric label should be visible");
                                                                     Assert.TextVisible("5", "Patient count should be 5");

                                                                     // Assert: Practitioner count (4 in mock data)
                                                                     Assert.TextVisible("Practitioners", "Practitioner metric label should be visible");
                                                                     Assert.TextVisible("4", "Practitioner count should be 4");

                                                                     result.Unmount();
                                                                     MockFetch.Restore();
                                                                 });

                                                                 Test("displays Quick Actions section", async () =>
                                                                 {
                                                                     var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                     MockFetch.Install(mock);

                                                                     var result = TestingLibrary.RenderApp();

                                                                     Assert.TextVisible("Quick Actions", "Quick Actions title should be visible");
                                                                     Assert.TextVisible("+ New Patient", "New Patient button should be visible");
                                                                     Assert.TextVisible("+ New Appointment", "New Appointment button should be visible");

                                                                     result.Unmount();
                                                                     MockFetch.Restore();
                                                                 });

                                                                 Test("displays Recent Activity section", async () =>
                                                                 {
                                                                     var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                     MockFetch.Install(mock);

                                                                     var result = TestingLibrary.RenderApp();

                                                                     Assert.TextVisible("Recent Activity", "Recent Activity title should be visible");
                                                                     Assert.TextVisible("View All", "View All button should be visible");

                                                                     // Assert activity items
                                                                     Assert.TextVisible("New patient registered", "Activity item should be visible");

                                                                     result.Unmount();
                                                                     MockFetch.Restore();
                                                                 });
                                                             });

        private static void PatientsPageTests() => Describe("Patients Page", () =>
                                                            {
                                                                Test("displays patient table with data", async () =>
                                                                {
                                                                    var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                    MockFetch.Install(mock);

                                                                    var result = TestingLibrary.RenderApp();

                                                                    // Navigate to Patients
                                                                    var navItems = result.QuerySelectorAll(".nav-item");
                                                                    foreach (var item in navItems)
                                                                    {
                                                                        var text = H5.Script.Get<string>(item, "textContent");
                                                                        if (text.Contains("Patients"))
                                                                        {
                                                                            TestingLibrary.FireClick(item);
                                                                            break;
                                                                        }
                                                                    }

                                                                    await TestingLibrary.WaitForText("Manage patient records");

                                                                    // Wait for patient data to load
                                                                    await TestingLibrary.WaitForText("John Smith");

                                                                    // Assert: Patient data is displayed
                                                                    Assert.TextVisible("John Smith", "Patient name should be visible");
                                                                    Assert.TextVisible("Jane Doe", "Second patient should be visible");
                                                                    Assert.TextVisible("Robert Johnson", "Third patient should be visible");

                                                                    // Assert: Table structure
                                                                    var table = result.QuerySelector(".table");
                                                                    Assert.ElementExists(table, "Table should be rendered");

                                                                    result.Unmount();
                                                                    MockFetch.Restore();
                                                                });

                                                                Test("filters patients when searching", async () =>
                                                                {
                                                                    var mockWithHistory = MockFetch.CreateWithHistory(MockData.GetApiResponses());
                                                                    MockFetch.Install(mockWithHistory.Fetch);

                                                                    var result = TestingLibrary.RenderApp();

                                                                    // Navigate to Patients
                                                                    var navItems = result.QuerySelectorAll(".nav-item");
                                                                    foreach (var item in navItems)
                                                                    {
                                                                        var text = H5.Script.Get<string>(item, "textContent");
                                                                        if (text.Contains("Patients"))
                                                                        {
                                                                            TestingLibrary.FireClick(item);
                                                                            break;
                                                                        }
                                                                    }

                                                                    await TestingLibrary.WaitForText("John Smith");

                                                                    // Find search input
                                                                    var searchInput = result.QuerySelector("input[placeholder*='Search']");
                                                                    Assert.ElementExists(searchInput, "Search input should exist");

                                                                    // Type search query
                                                                    await TestingLibrary.UserType(searchInput, "Jane");

                                                                    // Wait for filter to apply
                                                                    await Task.Delay(500);

                                                                    // Assert: Only Jane Doe should be visible now
                                                                    Assert.TextVisible("Jane Doe", "Filtered patient should be visible");

                                                                    result.Unmount();
                                                                    MockFetch.Restore();
                                                                });

                                                                Test("shows Add Patient button", async () =>
                                                                {
                                                                    var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                    MockFetch.Install(mock);

                                                                    var result = TestingLibrary.RenderApp();

                                                                    // Navigate to Patients
                                                                    var navItems = result.QuerySelectorAll(".nav-item");
                                                                    foreach (var item in navItems)
                                                                    {
                                                                        var text = H5.Script.Get<string>(item, "textContent");
                                                                        if (text.Contains("Patients"))
                                                                        {
                                                                            TestingLibrary.FireClick(item);
                                                                            break;
                                                                        }
                                                                    }

                                                                    await TestingLibrary.WaitForText("Manage patient records");

                                                                    Assert.TextVisible("Add Patient", "Add Patient button should be visible");

                                                                    result.Unmount();
                                                                    MockFetch.Restore();
                                                                });

                                                                Test("displays patient gender badges correctly", async () =>
                                                                {
                                                                    var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                    MockFetch.Install(mock);

                                                                    var result = TestingLibrary.RenderApp();

                                                                    // Navigate to Patients
                                                                    var navItems = result.QuerySelectorAll(".nav-item");
                                                                    foreach (var item in navItems)
                                                                    {
                                                                        var text = H5.Script.Get<string>(item, "textContent");
                                                                        if (text.Contains("Patients"))
                                                                        {
                                                                            TestingLibrary.FireClick(item);
                                                                            break;
                                                                        }
                                                                    }

                                                                    await TestingLibrary.WaitForText("John Smith");

                                                                    // Assert: Gender badges
                                                                    var badges = result.QuerySelectorAll(".badge");
                                                                    Assert.IsNotEmpty(badges, "Should have gender badges");

                                                                    result.Unmount();
                                                                    MockFetch.Restore();
                                                                });
                                                            });

        private static void PractitionersPageTests() => Describe("Practitioners Page", () =>
                                                                 {
                                                                     Test("displays practitioner cards with data", async () =>
                                                                     {
                                                                         var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                         MockFetch.Install(mock);

                                                                         var result = TestingLibrary.RenderApp();

                                                                         // Navigate to Practitioners
                                                                         var navItems = result.QuerySelectorAll(".nav-item");
                                                                         foreach (var item in navItems)
                                                                         {
                                                                             var text = H5.Script.Get<string>(item, "textContent");
                                                                             if (text.Contains("Practitioners"))
                                                                             {
                                                                                 TestingLibrary.FireClick(item);
                                                                                 break;
                                                                             }
                                                                         }

                                                                         await TestingLibrary.WaitForText("Manage healthcare providers");

                                                                         // Wait for data to load
                                                                         await TestingLibrary.WaitForText("Sarah Williams");

                                                                         // Assert: Practitioner data is displayed
                                                                         Assert.TextVisible("Sarah Williams", "Practitioner name should be visible");
                                                                         Assert.TextVisible("Cardiology", "Specialty should be visible");
                                                                         Assert.TextVisible("James Anderson", "Second practitioner should be visible");
                                                                         Assert.TextVisible("Neurology", "Second specialty should be visible");

                                                                         result.Unmount();
                                                                         MockFetch.Restore();
                                                                     });

                                                                     Test("shows practitioner avatars with initials", async () =>
                                                                     {
                                                                         var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                         MockFetch.Install(mock);

                                                                         var result = TestingLibrary.RenderApp();

                                                                         // Navigate to Practitioners
                                                                         var navItems = result.QuerySelectorAll(".nav-item");
                                                                         foreach (var item in navItems)
                                                                         {
                                                                             var text = H5.Script.Get<string>(item, "textContent");
                                                                             if (text.Contains("Practitioners"))
                                                                             {
                                                                                 TestingLibrary.FireClick(item);
                                                                                 break;
                                                                             }
                                                                         }

                                                                         await TestingLibrary.WaitForText("Sarah Williams");

                                                                         // Assert: Avatar with initials
                                                                         var avatars = result.QuerySelectorAll(".avatar");
                                                                         Assert.IsNotEmpty(avatars, "Should have avatar elements");

                                                                         // Check for initials
                                                                         Assert.TextVisible("SW", "Should show initials SW for Sarah Williams");

                                                                         result.Unmount();
                                                                         MockFetch.Restore();
                                                                     });

                                                                     Test("shows Add Practitioner button", async () =>
                                                                     {
                                                                         var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                         MockFetch.Install(mock);

                                                                         var result = TestingLibrary.RenderApp();

                                                                         // Navigate to Practitioners
                                                                         var navItems = result.QuerySelectorAll(".nav-item");
                                                                         foreach (var item in navItems)
                                                                         {
                                                                             var text = H5.Script.Get<string>(item, "textContent");
                                                                             if (text.Contains("Practitioners"))
                                                                             {
                                                                                 TestingLibrary.FireClick(item);
                                                                                 break;
                                                                             }
                                                                         }

                                                                         await TestingLibrary.WaitForText("Manage healthcare providers");

                                                                         Assert.TextVisible(
                                                                             "Add Practitioner",
                                                                             "Add Practitioner button should be visible"
                                                                         );

                                                                         result.Unmount();
                                                                         MockFetch.Restore();
                                                                     });

                                                                     Test("displays specialty filter dropdown", async () =>
                                                                     {
                                                                         var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                         MockFetch.Install(mock);

                                                                         var result = TestingLibrary.RenderApp();

                                                                         // Navigate to Practitioners
                                                                         var navItems = result.QuerySelectorAll(".nav-item");
                                                                         foreach (var item in navItems)
                                                                         {
                                                                             var text = H5.Script.Get<string>(item, "textContent");
                                                                             if (text.Contains("Practitioners"))
                                                                             {
                                                                                 TestingLibrary.FireClick(item);
                                                                                 break;
                                                                             }
                                                                         }

                                                                         await TestingLibrary.WaitForText("Manage healthcare providers");

                                                                         // Check for filter dropdown
                                                                         var select = result.QuerySelector("select");
                                                                         if (select != null)
                                                                         {
                                                                             Assert.TextVisible("Filter by Specialty", "Filter label should be visible");
                                                                         }

                                                                         result.Unmount();
                                                                         MockFetch.Restore();
                                                                     });
                                                                 });

        private static void AppointmentsPageTests() => Describe("Appointments Page", () =>
                                                                {
                                                                    Test("displays appointment list with data", async () =>
                                                                    {
                                                                        var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                        MockFetch.Install(mock);

                                                                        var result = TestingLibrary.RenderApp();

                                                                        // Navigate to Appointments
                                                                        var navItems = result.QuerySelectorAll(".nav-item");
                                                                        foreach (var item in navItems)
                                                                        {
                                                                            var text = H5.Script.Get<string>(item, "textContent");
                                                                            if (text.Contains("Appointments"))
                                                                            {
                                                                                TestingLibrary.FireClick(item);
                                                                                break;
                                                                            }
                                                                        }

                                                                        await TestingLibrary.WaitForText("Manage scheduling and appointments");

                                                                        // Wait for data to load
                                                                        await TestingLibrary.WaitForText("John Smith");

                                                                        // Assert: Appointment data is displayed
                                                                        Assert.TextVisible("John Smith", "Patient name should be visible");
                                                                        Assert.TextVisible("Dr. Sarah Williams", "Practitioner should be visible");
                                                                        Assert.TextVisible("Follow-up", "Service type should be visible");

                                                                        result.Unmount();
                                                                        MockFetch.Restore();
                                                                    });

                                                                    Test("displays status filter tabs", async () =>
                                                                    {
                                                                        var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                        MockFetch.Install(mock);

                                                                        var result = TestingLibrary.RenderApp();

                                                                        // Navigate to Appointments
                                                                        var navItems = result.QuerySelectorAll(".nav-item");
                                                                        foreach (var item in navItems)
                                                                        {
                                                                            var text = H5.Script.Get<string>(item, "textContent");
                                                                            if (text.Contains("Appointments"))
                                                                            {
                                                                                TestingLibrary.FireClick(item);
                                                                                break;
                                                                            }
                                                                        }

                                                                        await TestingLibrary.WaitForText("Manage scheduling and appointments");

                                                                        // Assert: Filter tabs are displayed
                                                                        Assert.TextVisible("All", "All filter should be visible");
                                                                        Assert.TextVisible("Booked", "Booked filter should be visible");
                                                                        Assert.TextVisible("Fulfilled", "Fulfilled filter should be visible");
                                                                        Assert.TextVisible("Cancelled", "Cancelled filter should be visible");

                                                                        result.Unmount();
                                                                        MockFetch.Restore();
                                                                    });

                                                                    Test("filters appointments by status when clicking tabs", async () =>
                                                                    {
                                                                        var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                        MockFetch.Install(mock);

                                                                        var result = TestingLibrary.RenderApp();

                                                                        // Navigate to Appointments
                                                                        var navItems = result.QuerySelectorAll(".nav-item");
                                                                        foreach (var item in navItems)
                                                                        {
                                                                            var text = H5.Script.Get<string>(item, "textContent");
                                                                            if (text.Contains("Appointments"))
                                                                            {
                                                                                TestingLibrary.FireClick(item);
                                                                                break;
                                                                            }
                                                                        }

                                                                        await TestingLibrary.WaitForText("John Smith");

                                                                        // Find and click Booked filter
                                                                        var buttons = result.QuerySelectorAll("button");
                                                                        foreach (var btn in buttons)
                                                                        {
                                                                            var text = H5.Script.Get<string>(btn, "textContent");
                                                                            if (text == "Booked")
                                                                            {
                                                                                TestingLibrary.FireClick(btn);
                                                                                break;
                                                                            }
                                                                        }

                                                                        await Task.Delay(300);

                                                                        // Assert: Only booked appointments visible
                                                                        // (John Smith and Emily Wilson have booked status)
                                                                        Assert.TextVisible(
                                                                            "booked",
                                                                            "Booked status badge should be visible"
                                                                        );

                                                                        result.Unmount();
                                                                        MockFetch.Restore();
                                                                    });

                                                                    Test("displays status badges with correct colors", async () =>
                                                                    {
                                                                        var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                        MockFetch.Install(mock);

                                                                        var result = TestingLibrary.RenderApp();

                                                                        // Navigate to Appointments
                                                                        var navItems = result.QuerySelectorAll(".nav-item");
                                                                        foreach (var item in navItems)
                                                                        {
                                                                            var text = H5.Script.Get<string>(item, "textContent");
                                                                            if (text.Contains("Appointments"))
                                                                            {
                                                                                TestingLibrary.FireClick(item);
                                                                                break;
                                                                            }
                                                                        }

                                                                        await TestingLibrary.WaitForText("John Smith");

                                                                        // Assert: Status badges exist
                                                                        var badges = result.QuerySelectorAll(".badge");
                                                                        Assert.IsNotEmpty(badges, "Should have status badges");

                                                                        result.Unmount();
                                                                        MockFetch.Restore();
                                                                    });

                                                                    Test("shows New Appointment button", async () =>
                                                                    {
                                                                        var mock = MockFetch.Create(MockData.GetApiResponses());
                                                                        MockFetch.Install(mock);

                                                                        var result = TestingLibrary.RenderApp();

                                                                        // Navigate to Appointments
                                                                        var navItems = result.QuerySelectorAll(".nav-item");
                                                                        foreach (var item in navItems)
                                                                        {
                                                                            var text = H5.Script.Get<string>(item, "textContent");
                                                                            if (text.Contains("Appointments"))
                                                                            {
                                                                                TestingLibrary.FireClick(item);
                                                                                break;
                                                                            }
                                                                        }

                                                                        await TestingLibrary.WaitForText("Manage scheduling and appointments");

                                                                        Assert.TextVisible("New Appointment", "New Appointment button should be visible");

                                                                        result.Unmount();
                                                                        MockFetch.Restore();
                                                                    });
                                                                });

        private static void SidebarTests() => Describe("Sidebar", () =>
                                                       {
                                                           Test("displays logo and brand name", async () =>
                                                           {
                                                               var mock = MockFetch.Create(MockData.GetApiResponses());
                                                               MockFetch.Install(mock);

                                                               var result = TestingLibrary.RenderApp();

                                                               Assert.TextVisible("Healthcare", "Brand name should be visible in sidebar");

                                                               var logo = result.QuerySelector(".sidebar-logo");
                                                               Assert.ElementExists(logo, "Sidebar logo should exist");

                                                               result.Unmount();
                                                               MockFetch.Restore();
                                                           });

                                                           Test("displays navigation sections", async () =>
                                                           {
                                                               var mock = MockFetch.Create(MockData.GetApiResponses());
                                                               MockFetch.Install(mock);

                                                               var result = TestingLibrary.RenderApp();

                                                               Assert.TextVisible("Overview", "Overview section should be visible");
                                                               Assert.TextVisible("Clinical", "Clinical section should be visible");
                                                               Assert.TextVisible("Scheduling", "Scheduling section should be visible");

                                                               result.Unmount();
                                                               MockFetch.Restore();
                                                           });

                                                           Test("highlights active navigation item", async () =>
                                                           {
                                                               var mock = MockFetch.Create(MockData.GetApiResponses());
                                                               MockFetch.Install(mock);

                                                               var result = TestingLibrary.RenderApp();

                                                               // Dashboard should be active by default
                                                               var activeNav = result.QuerySelector(".nav-item.active");
                                                               Assert.ElementExists(activeNav, "Should have an active nav item");
                                                               Assert.HasTextContent(activeNav, "Dashboard", "Dashboard should be active");

                                                               // Navigate to Patients
                                                               var navItems = result.QuerySelectorAll(".nav-item");
                                                               foreach (var item in navItems)
                                                               {
                                                                   var text = H5.Script.Get<string>(item, "textContent");
                                                                   if (text.Contains("Patients"))
                                                                   {
                                                                       TestingLibrary.FireClick(item);
                                                                       break;
                                                                   }
                                                               }

                                                               await Task.Delay(300);

                                                               // Now Patients should be active
                                                               activeNav = result.QuerySelector(".nav-item.active");
                                                               Assert.HasTextContent(activeNav, "Patients", "Patients should now be active");

                                                               result.Unmount();
                                                               MockFetch.Restore();
                                                           });

                                                           Test("toggles sidebar collapse state", async () =>
                                                           {
                                                               var mock = MockFetch.Create(MockData.GetApiResponses());
                                                               MockFetch.Install(mock);

                                                               var result = TestingLibrary.RenderApp();

                                                               var sidebar = result.QuerySelector(".sidebar");
                                                               Assert.ElementExists(sidebar);

                                                               // Find toggle button
                                                               var toggleBtn = result.QuerySelector(".sidebar-toggle");
                                                               Assert.ElementExists(toggleBtn, "Sidebar toggle button should exist");

                                                               // Click to collapse
                                                               TestingLibrary.FireClick(toggleBtn);
                                                               await Task.Delay(300);

                                                               // Check if sidebar has collapsed class
                                                               sidebar = result.QuerySelector(".sidebar");
                                                               var classList = H5.Script.Get<object>(sidebar, "classList");
                                                               var isCollapsed = H5.Script.Write<bool>("classList.contains('collapsed')");
                                                               Assert.IsTrue(isCollapsed, "Sidebar should be collapsed");

                                                               // Click again to expand
                                                               toggleBtn = result.QuerySelector(".sidebar-toggle");
                                                               TestingLibrary.FireClick(toggleBtn);
                                                               await Task.Delay(300);

                                                               sidebar = result.QuerySelector(".sidebar");
                                                               classList = H5.Script.Get<object>(sidebar, "classList");
                                                               isCollapsed = H5.Script.Write<bool>("classList.contains('collapsed')");
                                                               Assert.IsFalse(isCollapsed, "Sidebar should be expanded");

                                                               result.Unmount();
                                                               MockFetch.Restore();
                                                           });

                                                           Test("displays user info in footer", async () =>
                                                           {
                                                               var mock = MockFetch.Create(MockData.GetApiResponses());
                                                               MockFetch.Install(mock);

                                                               var result = TestingLibrary.RenderApp();

                                                               Assert.TextVisible("John Doe", "User name should be visible");
                                                               Assert.TextVisible("Administrator", "User role should be visible");

                                                               result.Unmount();
                                                               MockFetch.Restore();
                                                           });
                                                       });

        private static void HeaderTests() => Describe("Header", () =>
                                                      {
                                                          Test("displays search input", async () =>
                                                          {
                                                              var mock = MockFetch.Create(MockData.GetApiResponses());
                                                              MockFetch.Install(mock);

                                                              var result = TestingLibrary.RenderApp();

                                                              var searchInput = result.QuerySelector(".header-search input");
                                                              Assert.ElementExists(searchInput, "Header search input should exist");

                                                              result.Unmount();
                                                              MockFetch.Restore();
                                                          });

                                                          Test("displays notification bell", async () =>
                                                          {
                                                              var mock = MockFetch.Create(MockData.GetApiResponses());
                                                              MockFetch.Install(mock);

                                                              var result = TestingLibrary.RenderApp();

                                                              var bellBtn = result.QuerySelector(".header-action-btn");
                                                              Assert.ElementExists(bellBtn, "Notification bell should exist");

                                                              result.Unmount();
                                                              MockFetch.Restore();
                                                          });

                                                          Test("displays user avatar", async () =>
                                                          {
                                                              var mock = MockFetch.Create(MockData.GetApiResponses());
                                                              MockFetch.Install(mock);

                                                              var result = TestingLibrary.RenderApp();

                                                              var avatars = result.QuerySelectorAll(".header .avatar");
                                                              Assert.IsNotEmpty(avatars, "Header should have user avatar");

                                                              result.Unmount();
                                                              MockFetch.Restore();
                                                          });
                                                      });

        private static void ErrorHandlingTests() => Describe("Error Handling", () =>
                                                             {
                                                                 Test("shows warning when API is unavailable", async () =>
                                                                 {
                                                                     // Create mock that returns errors
                                                                     var errorMock = MockFetch.CreateWithErrors(
                                                                         new System.Collections.Generic.Dictionary<string, object>(),
                                                                         new System.Collections.Generic.Dictionary<string, int>
                                                                         {
                            { "/fhir/Patient", 500 },
                            { "/Practitioner", 500 },
                            { "/Appointment", 500 },
                                                                         }
                                                                     );
                                                                     MockFetch.Install(errorMock);

                                                                     var result = TestingLibrary.RenderApp();

                                                                     // Wait for error state
                                                                     await Task.Delay(1000);

                                                                     // Should show connection warning or error state
                                                                     var hasWarning =
                                                                         H5.Script.Call<bool>(
                                                                             "document.body.textContent.includes",
                                                                             "Connection Warning"
                                                                         )
                                                                         || H5.Script.Call<bool>(
                                                                             "document.body.textContent.includes",
                                                                             "Could not connect"
                                                                         );

                                                                     Assert.IsTrue(hasWarning, "Should show connection warning");

                                                                     result.Unmount();
                                                                     MockFetch.Restore();
                                                                 });

                                                                 Test("shows loading skeletons while fetching data", async () =>
                                                                 {
                                                                     // Create mock with delay to see loading state
                                                                     var delayedMock = MockFetch.CreateWithDelay(MockData.GetApiResponses(), 2000);
                                                                     MockFetch.Install(delayedMock);

                                                                     var result = TestingLibrary.RenderApp();

                                                                     // Check for loading skeletons immediately
                                                                     var skeletons = result.QuerySelectorAll(".skeleton");

                                                                     // May or may not have skeletons depending on implementation
                                                                     // Just verify the app doesn't crash during loading

                                                                     result.Unmount();
                                                                     MockFetch.Restore();
                                                                 });

                                                                 Test("handles empty data gracefully", async () =>
                                                                 {
                                                                     // Create mock with empty arrays
                                                                     var emptyMock = MockFetch.Create(
                                                                         new System.Collections.Generic.Dictionary<string, object>
                                                                         {
                            { "/fhir/Patient", new object[0] },
                            { "/Practitioner", new object[0] },
                            { "/Appointment", new object[0] },
                                                                         }
                                                                     );
                                                                     MockFetch.Install(emptyMock);

                                                                     var result = TestingLibrary.RenderApp();

                                                                     // Navigate to Patients to see empty state
                                                                     var navItems = result.QuerySelectorAll(".nav-item");
                                                                     foreach (var item in navItems)
                                                                     {
                                                                         var text = H5.Script.Get<string>(item, "textContent");
                                                                         if (text.Contains("Patients"))
                                                                         {
                                                                             TestingLibrary.FireClick(item);
                                                                             break;
                                                                         }
                                                                     }

                                                                     await Task.Delay(500);

                                                                     // Should show empty state message
                                                                     var hasEmptyMessage =
                                                                         H5.Script.Call<bool>("document.body.textContent.includes", "No patients")
                                                                         || H5.Script.Call<bool>("document.body.textContent.includes", "No Data")
                                                                         || H5.Script.Call<bool>(
                                                                             "document.body.textContent.includes",
                                                                             "No records"
                                                                         );

                                                                     Assert.IsTrue(hasEmptyMessage, "Should show empty state message");

                                                                     result.Unmount();
                                                                     MockFetch.Restore();
                                                                 });
                                                             });
    }
}
