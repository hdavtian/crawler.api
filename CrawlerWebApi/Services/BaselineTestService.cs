using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Providers.Logger;
using IC.Test.Playwright.Crawler.Utility;
using System.Text;
using AngleSharp.Dom;
using IC.Test.Playwright.Crawler.Providers.Logger.Enums;
using IC.Test.Playwright.Crawler.Enums;
using AngleSharp.Io;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Microsoft.Playwright;
using IC.Test.Playwright.Crawler.Interfaces;
using IC.Test.Playwright.Crawler.Providers.Playwright;
using System.Collections.Concurrent;

namespace CrawlerWebApi.Services
{
    /// <summary>
    /// Service responsible for running baseline tests using Playwright.
    /// It handles browser initialization, login, crawling, and data collection.
    /// </summary>
    public class BaselineTestService : IBaselineTestService
    {
        // Dependencies injected via constructor
        private readonly IPlaywrightFactory PlaywrightFactory;
        private readonly LoginDriver LoginDriver;
        private readonly CrawlTest CrawlTest;
        private readonly CrawlDriver CrawlDriver;
        private readonly CrawlContext CrawlContext;
        private readonly CrawlerCommon CrawlerCommon;
        private readonly ILoggingProvider Logger;
        private readonly string ApiRootWinPath;
        private readonly string SiteArtifactsWinPath;
        private readonly string SourceFilePath;
        private readonly WriterQueueService WriterQueueService;
        private readonly TestRegistryService TestRegistryService;

        /// <summary>
        /// Constructor to initialize dependencies and configuration values.
        /// </summary>
        public BaselineTestService(
            IPlaywrightFactory PlaywrightFactory,
            LoginDriver LoginDriver,
            CrawlTest CrawlTest,
            CrawlDriver CrawlDriver,
            CrawlContext CrawlContext,
            CrawlerCommon CrawlerCommon,
            IConfiguration AppConfiguration,
            WriterQueueService WriterQueueService,
            ILoggingProvider loggingProvider,
            TestRegistryService testRegistryService
        )
        {
            this.PlaywrightFactory = PlaywrightFactory;
            this.LoginDriver = LoginDriver;
            this.CrawlTest = CrawlTest;
            this.CrawlDriver = CrawlDriver;
            this.CrawlContext = CrawlContext;
            this.CrawlerCommon = CrawlerCommon;
            Logger = loggingProvider;
            ApiRootWinPath = AppConfiguration["ApiRootWinPath"];
            SiteArtifactsWinPath = AppConfiguration["SiteArtifactsWinPath"];
            SourceFilePath = AppConfiguration["FileSettings:SourceFilePath"];
            this.WriterQueueService = WriterQueueService;
            this.TestRegistryService = testRegistryService;
        }

        /// <summary>
        /// Executes the baseline test workflow: initializes Playwright, configures the test context,
        /// sets up network interception, performs login (if required), applies site version restrictions,
        /// runs the crawl, finalizes timers, saves reports, disposes resources, and moves logs.
        /// </summary>
        /// <param name="request">The test configuration and parameters.</param>
        /// <returns>A <see cref="TestResult"/> indicating success or failure.</returns>
        public async Task<TestResult> RunBaselineTestAsync(BaselineTestPostRequestModel request)
        {
            var testId = CrawlTest.Id;

            // Register the test at the very beginning with test registry
            TestRegistryService.RegisterTest(testId, new TestStatus
            {
                TestId = testId,
                TestType = "Crawl",
                StartTime = DateTime.UtcNow,
                Name = CrawlTest.Name,
                Description = CrawlTest.Description,
                TriggeredBy = CrawlTest.TaffieUser?.DisplayName
            });

            try
            {
                // 1. Initialize Playwright
                var initResult = await InitializePlaywrightAsync(request);
                if (!initResult.Success) return initResult;

                // 2. Configure Test and Context
                var configResult = ConfigureTestAndContext(request, initResult.Options, initResult.Context, initResult.Page);
                if (!configResult.Success) return configResult;

                // 3. Setup Network Interception
                var networkResult = SetupNetworkInterception(initResult.Page);
                if (!networkResult.Success) return networkResult;

                // 4. Perform Login (if required)
                var loginResult = await PerformLoginIfRequiredAsync(request, initResult.Page, initResult.Options);
                if (!loginResult.Success) return loginResult;
                if (loginResult is LoginOnlyTestResult loginOnly && loginOnly.IsLoginOnly) return loginOnly;

                // 5. Handle Site Version Restrictions
                var versionResult = HandleSiteVersionRestrictions();
                if (!versionResult.Success) return versionResult;

                // 6. Perform Crawl
                var crawlResult = await PerformCrawlAsync(request, initResult.Options, initResult.Page);
                if (!crawlResult.Success) return crawlResult;

                // 7. Finalize Timers
                var timerResult = FinalizeTimers();
                if (!timerResult.Success) return timerResult;

                // 8. Save Reports
                var reportResult = await SaveReportsAsync(request, initResult.Context);
                if (!reportResult.Success) return reportResult;

                // 9. Dispose Context
                var disposeResult = await DisposeContextAsync(initResult.Context);
                if (!disposeResult.Success) return disposeResult;

                // 10. Move Log and Raise Events
                await FileUtil.MoveFileAsync(SourceFilePath, CrawlTest.BaseSaveFolder, CrawlTest.LogFileName, Logger);
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");

                return new TestResult { Success = true };
            }
            finally
            {
                // Always unregister the test
                TestRegistryService.MarkTestCompleted(testId);
            }
        }

        /// <summary>
        /// Represents the result of initializing Playwright, including the browser context, page, options, and success status.
        /// </summary>
        private class PlaywrightInitResult : TestResult
        {
            public IBrowserContext Context { get; set; }
            public IPage Page { get; set; }
            public PlaywrightOptions Options { get; set; }
        }

        /// <summary>
        /// Represents the result of a login operation, indicating if the test was a login-only scenario.
        /// </summary>
        private class LoginOnlyTestResult : TestResult
        {
            public bool IsLoginOnly { get; set; }
        }

        /// <summary>
        /// Initializes Playwright with the specified test request options and creates a browser context and page.
        /// </summary>
        /// <param name="request">The test configuration and parameters.</param>
        /// <returns>A <see cref="PlaywrightInitResult"/> containing the context, page, options, and success status.</returns>
        private async Task<PlaywrightInitResult> InitializePlaywrightAsync(BaselineTestPostRequestModel request)
        {
            try
            {
                var options = new PlaywrightOptions
                {
                    TestId = CrawlTest.Id,
                    BrowserName = request.Browser,
                    BrowserType = request.Browser.ToBrowserTypeEnum(),
                    Headless = request.Headless,
                    Width = request.WindowWidth,
                    Height = request.WindowHeight,
                    RecordVideo = request.RecordVideo,
                    VideoSavePath = request.RecordVideo ? Path.Combine(CrawlTest.BaseSaveFolder, "videos") : null,
                    SaveHar = request.SaveHar,
                    HarPath = request.SaveHar ? CrawlTest.BaseSaveFolder : null,
                };

                var (context, page) = await PlaywrightFactory.CreateContextAndPageAsync(options);

                CrawlTest.Browser.Name = options.BrowserName;
                CrawlTest.Browser.Width = options.Width;
                CrawlTest.Browser.Height = options.Height;
                CrawlTest.Browser.Headless = options.Headless;
                Logger.Info("Playwright context initialized successfully.");

                await CrawlerCommon.SetViewPortSizeAsync(page, CrawlTest.Browser.Width, CrawlTest.Browser.Height);
                Logger.Info($"Set browser viewport to {CrawlTest.Browser.Width}px by {CrawlTest.Browser.Height}px (width x height)");

                return new PlaywrightInitResult
                {
                    Success = true,
                    Context = context,
                    Page = page,
                    Options = options
                };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "<<Error>> Failed to initialize Playwright context.");
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                return new PlaywrightInitResult { Success = false, ErrorMessage = "Failed to initialize Playwright context." };
            }
        }

        /// <summary>
        /// Configures the test model and crawl context with settings from the request and initializes test metadata.
        /// </summary>
        /// <param name="request">The test configuration and parameters.</param>
        /// <param name="options">The Playwright options to use for the test.</param>
        /// <param name="context">The Playwright browser context.</param>
        /// <param name="page">The Playwright page instance.</param>
        /// <returns>A <see cref="TestResult"/> indicating success or failure.</returns>
        private TestResult ConfigureTestAndContext(
            BaselineTestPostRequestModel request,
            PlaywrightOptions options,
            IBrowserContext context,
            IPage page)
        {
            try
            {
                Logger.Info("<<TestStarted>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestStarted, "The crawl test has started");
                Logger.Info("TestId: " + CrawlTest.Id);

                LogBaselineTestPostRequestModel(request);

                // Update CrawlContext with request settings
                CrawlContext.TakePageScreenshots = request.TakePageScreenshots;
                CrawlContext.TakeAppScreenshots = request.TakeAppScreenshots;
                CrawlContext.CaptureAppHtml = request.CaptureAppHtml;
                CrawlContext.CaptureAppText = request.CaptureAppText;
                CrawlContext.GenerateAxeReports = request.GenerateAxeReports;
                CrawlContext.SaveNetworkTraffic = request.SaveNetworkTraffic;

                string _harFileName = $"{CrawlTest.Id}.har";
                string CrawlDurationTimerName = $"CrawlDuration_{Guid.NewGuid()}";

                // Store timer name in CrawlTest for later use (if needed)
                // (If you want to use this timer name in other methods, consider storing it in CrawlTest or as a field.)

                // Initialize test metadata
                TimerUtil.StartTimer(CrawlTest.Timers, CrawlDurationTimerName);
                CrawlTest.Name = "";
                CrawlTest.Description = "";
                CrawlTest.DateTime = DateTime.Now;
                CrawlTest.Browser.Width = request.WindowWidth;
                CrawlTest.Browser.Height = request.WindowHeight;
                string projectNameSubdomain = UrlUtil.GetSubdomainFromUrl(request.Url).ToLower();
                CrawlTest.BaseSaveFolder = PathUtil.CreateSavePath(
                    "crawl-tests",
                    projectNameSubdomain,
                    request.WindowWidth,
                    request.WindowHeight,
                    CrawlTest.Id.ToString());
                CrawlTest.ExtraUrls = request.ExtraUrls;
                CrawlTest.OnlyCrawlExtraUrls = request.OnlyCrawlExtraUrls;
                CrawlTest.PtierVersion = request.PtierVersion;
                CrawlTest.TaffieUser = request.TaffieUser;
                Logger.Info("Test model set up successfully.");
                Logger.Info($"BaseSaveFolder: {CrawlTest.BaseSaveFolder}");

                // Set storage state path for Playwright
                options.StorageStatePath = Path.Combine(CrawlTest.BaseSaveFolder, "storage-state.json");
                Logger.Info($"Set storage state path: {options.StorageStatePath}");

                // Optionally, store the timer name for later use (if needed in other steps)
                // CrawlTest.CrawlDurationTimerName = CrawlDurationTimerName;

                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "<<Error>> Failed to set up test model.");
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                return new TestResult { Success = false, ErrorMessage = "Failed to set up test model." };
            }
        }

        /// <summary>
        /// Sets up network interception and event handlers for requests, responses, and console errors on the given page.
        /// </summary>
        /// <param name="page">The Playwright page instance.</param>
        /// <returns>A <see cref="TestResult"/> indicating success or failure.</returns>
        private TestResult SetupNetworkInterception(IPage page)
        {
            try
            {
                CrawlTest.NetworkData = new List<NetworkData>();
                CrawlTest.XhrRequestStartTimes = new Dictionary<IRequest, DateTime>();
                string _currentPageUrl = page.Url;

                // Track navigation, requests, and responses
                page.FrameNavigated += (_, frame) =>
                {
                    if (frame == page.MainFrame)
                    {
                        _currentPageUrl = frame.Url;
                    }
                };

                page.Request += (_, request) =>
                {
                    if (request?.ResourceType is "xhr" or "fetch")
                    {
                        try
                        {
                            if (request != null && !CrawlTest.XhrRequestStartTimes.ContainsKey(request))
                            {
                                CrawlTest.XhrRequestStartTimes[request] = DateTime.UtcNow;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn(ex, $"<<Warning>> Failed to track XHR start time for: {request?.Url}");
                        }
                    }

                    CrawlTest.NetworkData.Add(new NetworkData
                    {
                        Url = request.Url,
                        Method = request.Method,
                        Headers = request.Headers,
                        PostData = request.PostData,
                        PageUrl = _currentPageUrl
                    });
                };

                page.Console += (_, msg) =>
                {
                    if (msg.Type == "error")
                    {
                        string url = page.Url;
                        string errorMessage = $"[Console Javascript Error] {msg.Text} (URL: {url})";

                        var errorBag = CrawlContext.ConsoleJsErrors.GetOrAdd(url, _ => new ConcurrentBag<string>());

                        if (!errorBag.Contains(msg.Text))
                        {
                            errorBag.Add(msg.Text);
                            Logger.Warn(errorMessage);
                        }

                        Logger.Warn(errorMessage);
                    }
                };

                page.Response += async (_, response) =>
                {
                    // Find the matching request in the custom NetworkData list
                    var matchingRequest = CrawlTest.NetworkData.FirstOrDefault(r => r.Url == response.Url);
                    if (matchingRequest != null)
                    {
                        matchingRequest.StatusCode = response.Status;
                        matchingRequest.ResponseHeaders = response.Headers;

                        try
                        {
                            matchingRequest.ResponseBody = await response.BodyAsync();
                            matchingRequest.ResponseBodyAsString = await response.TextAsync();
                        }
                        catch (Exception)
                        {
                            // Optionally log or ignore
                        }
                    }

                    var req = response.Request;

                    if ((req.ResourceType == "xhr" || req.ResourceType == "fetch") &&
                        CrawlTest.XhrRequestStartTimes.TryGetValue(req, out var start))
                    {
                        var end = DateTime.UtcNow;

                        var xhrGroupsSnapshot = CrawlTest.GroupedXhrTimings.ToList();
                        var group = xhrGroupsSnapshot.FirstOrDefault(g => g.Url == _currentPageUrl);

                        if (group == null)
                        {
                            group = new PageXhrTimingsGroup { Url = _currentPageUrl };
                            CrawlTest.GroupedXhrTimings.Add(group);
                        }

                        group.XhrCalls.Add(new XhrCallTiming
                        {
                            Url = response.Url,
                            StartTime = start,
                            EndTime = end
                        });

                        CrawlTest.XhrRequestStartTimes.Remove(req);
                    }
                };

                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "<<Error>> Failed to set up network interception.");
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                return new TestResult { Success = false, ErrorMessage = "Failed to set up network interception." };
            }
        }

        /// <summary>
        /// Performs login if required by the test request, or navigates to the target URL if not.
        /// </summary>
        /// <param name="request">The test configuration and parameters.</param>
        /// <param name="page">The Playwright page instance.</param>
        /// <param name="options">The Playwright options to use for the test.</param>
        /// <returns>
        /// A <see cref="TestResult"/> indicating success or failure, or a <see cref="LoginOnlyTestResult"/> if the test is login-only.
        /// </returns>
        private async Task<TestResult> PerformLoginIfRequiredAsync(
            BaselineTestPostRequestModel request,
            IPage page,
            PlaywrightOptions options)
        {
            if (request.RequiresLogin)
            {
                CrawlTest.PlaywrightOptions = options;

                try
                {
                    LoginSelectorOverrides overrides = null;

                    if (!string.IsNullOrWhiteSpace(request.UsernameQuerySelector) &&
                        !string.IsNullOrWhiteSpace(request.PasswordQuerySelector) &&
                        !string.IsNullOrWhiteSpace(request.LoginButtonQuerySelector))
                    {
                        overrides = new LoginSelectorOverrides
                        {
                            UsernameFieldQuerySelector = request.UsernameQuerySelector,
                            PasswordFieldQuerySelector = request.PasswordQuerySelector,
                            LoginButtonQuerySelector = request.LoginButtonQuerySelector
                        };
                    }

                    LoginDriver.SetPage(page);
                    await LoginDriver.LoginToApplication(page, request.Url, request.Username, request.Password, overrides);
                    Logger.Info("Login performed successfully.");

                    if (CrawlTest.CrawlType.Equals(CrawlType.LoginOnly))
                    {
                        Logger.Info($"<<TestEnded>> This was a '{CrawlTest.CrawlType}' test");
                        Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                        return new LoginOnlyTestResult { Success = true, IsLoginOnly = true };
                    }

                    return new TestResult { Success = true };
                }
                catch (Exception ex)
                {
                    string errMsg = "<<Error>> Failed during login process.";
                    Logger.Error(ex, errMsg);
                    Logger.SystemLog(LogLevel.Error, errMsg);
                    Logger.Info("<<TestEnded>>");
                    Logger.RaiseEvent(TaffieEventType.LoginFailed, errMsg);
                    Logger.RaiseEvent(TaffieEventType.Error, errMsg);
                    Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                    return new TestResult { Success = false, ErrorMessage = errMsg };
                }
            }
            else
            {
                try
                {
                    await CrawlerCommon.NavigateToUrlAsync(page, request.Url);
                    CrawlTest.BaseUrl = CrawlerCommon.ExtractSchemeAndTLD(request.Url);
                    return new TestResult { Success = true };
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to navigate to URL (no login required).");
                    Logger.Info("<<TestEnded>>");
                    Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                    return new TestResult { Success = false, ErrorMessage = "Failed to navigate to URL (no login required)." };
                }
            }
        }

        /// <summary>
        /// Applies site version-specific restrictions to the crawl context (e.g., disables unsupported features for V1 sites).
        /// </summary>
        /// <returns>A <see cref="TestResult"/> indicating success or failure.</returns>
        private TestResult HandleSiteVersionRestrictions()
        {
            try
            {
                // Only apply restrictions for V1 sites
                if (CrawlTest.SiteVersion is SiteVersion.V1)
                {
                    StringBuilder logMsg = new StringBuilder();
                    if (CrawlContext.TakeAppScreenshots)
                    {
                        CrawlContext.TakeAppScreenshots = false;
                        logMsg.AppendLine("V1 crawl currently does not support taking app screenshots, will skip this operation.");
                    }
                    if (CrawlContext.CaptureAppHtml)
                    {
                        CrawlContext.CaptureAppHtml = false;
                        logMsg.AppendLine("V1 crawl currently does not support capturing app html, will skip this operation.");
                    }
                    if (CrawlContext.CaptureAppText)
                    {
                        CrawlContext.CaptureAppText = false;
                        logMsg.AppendLine("V1 crawl currently does not support capturing app text, will skip this operation.");
                    }

                    if (logMsg.Length != 0)
                    {
                        Logger.Info(logMsg.ToString());
                    }
                }

                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "<<Error>> Failed to apply site version restrictions.");
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                return new TestResult { Success = false, ErrorMessage = "Failed to apply site version restrictions." };
            }
        }

        /// <summary>
        /// Executes the main crawl process using the provided options and page, handling post-login waits if necessary.
        /// </summary>
        /// <param name="request">The test configuration and parameters.</param>
        /// <param name="options">The Playwright options to use for the test.</param>
        /// <param name="page">The Playwright page instance.</param>
        /// <returns>A <see cref="TestResult"/> indicating success or failure.</returns>
        private async Task<TestResult> PerformCrawlAsync(
            BaselineTestPostRequestModel request,
            PlaywrightOptions options,
            IPage page)
        {
            try
            {
                if (request.RequiresLogin)
                {
                    // Wait for any post-login redirects/forwards to complete
                    await CrawlerCommon.WaitInMilliseconds(page, 3000);
                    // Reuse the logged-in page
                    await CrawlDriver.Crawl(options, page);
                }
                else
                {
                    // Let CrawlDriver create its own context and page
                    await CrawlDriver.Crawl(options, page);
                }

                Logger.Info("Crawl completed successfully.");
                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                string errMsg = $"<<Error>> {ex.Message}";
                Logger.Error(ex, errMsg);
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                return new TestResult { Success = false, ErrorMessage = errMsg };
            }
        }

        /// <summary>
        /// Finalizes and stops all timers associated with the crawl test, recording the main crawl duration.
        /// </summary>
        /// <returns>A <see cref="TestResult"/> indicating success or failure.</returns>
        private TestResult FinalizeTimers()
        {
            try
            {
                if (CrawlTest.Timers == null || CrawlTest.Timers.Count == 0)
                {
                    Logger.Warn("No timers found to finalize.");
                    return new TestResult { Success = true };
                }

                // Attempt to find the crawl duration timer (by convention, starts with "CrawlDuration_")
                var crawlDurationKey = CrawlTest.Timers.Keys.FirstOrDefault(k => k.StartsWith("CrawlDuration_"));
                if (crawlDurationKey != null)
                {
                    var stopwatch = CrawlTest.Timers[crawlDurationKey];
                    if (stopwatch.IsRunning)
                        stopwatch.Stop();

                    CrawlTest.Duration = stopwatch.Elapsed;
                    Logger.Info($"Timer '{crawlDurationKey}' stopped. Duration: {CrawlTest.Duration}");
                }
                else
                {
                    // Stop all timers as a fallback
                    foreach (var kvp in CrawlTest.Timers)
                    {
                        if (kvp.Value.IsRunning)
                            kvp.Value.Stop();
                    }
                    Logger.Warn("No crawl duration timer found. All timers stopped.");
                }

                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "<<Error>> Failed to finalize timers.");
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                return new TestResult { Success = false, ErrorMessage = "Failed to finalize timers." };
            }
        }

        /// <summary>
        /// Saves all reports and artifacts generated during the crawl test, including network data, screenshots, and metadata.
        /// </summary>
        /// <param name="request">The test configuration and parameters.</param>
        /// <param name="context">The Playwright browser context used during the test.</param>
        /// <returns>A <see cref="TestResult"/> indicating success or failure.</returns>
        private async Task<TestResult> SaveReportsAsync(BaselineTestPostRequestModel request, IBrowserContext context)
        {
            try
            {
                await createJsonFilesForCrawlTest(request, context);
                Logger.Info("All reports saved successfully.");
                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "<<Error>> Failed to save reports.");
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                return new TestResult { Success = false, ErrorMessage = "Failed to save reports." };
            }
        }

        /// <summary>
        /// Disposes the Playwright browser context, releasing all associated resources.
        /// </summary>
        /// <param name="context">The Playwright browser context to dispose.</param>
        /// <returns>A <see cref="TestResult"/> indicating success or failure.</returns>
        private async Task<TestResult> DisposeContextAsync(IBrowserContext context)
        {
            try
            {
                if (context != null)
                {
                    await context.CloseAsync();
                    Logger.Info("Playwright context disposed successfully.");
                }
                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "<<Error>> Failed to dispose Playwright context.");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                return new TestResult { Success = false, ErrorMessage = "Failed to dispose Playwright context." };
            }
        }

        /// <summary>
        /// Creates and saves various JSON files and reports for the crawl test.
        /// This includes network traffic, screenshots, HTML, text, and other test-related data.
        /// </summary>
        /// <param name="request">The test request model containing user-specified settings.</param>
        /// <param name="context">The Playwright browser context used during the test.</param>
        private async Task createJsonFilesForCrawlTest(BaselineTestPostRequestModel request, IBrowserContext context)
        {
            //
            // Below
            // - Save Network Traffic
            // - Calculate and add totals to CrawlTest
            // - ReportWriter
            //   test-info.json
            //   urls.json
            //   app-html.json
            //   app-text.json
            //   page-screenshots.json
            //   app-screenshots.json
            //   pages-and-apps.json
            //   js-console-errors.json
            //   tests.json (update)

            try
            {
                // Save network traffic data if requested
                if (request.SaveNetworkTraffic)
                {
                    ReportWriter.SaveModelAsJsonFile(CrawlTest.NetworkData, CrawlTest.BaseSaveFolder, "networkData");
                    Logger.Info("Network log reports saved successfully.");
                }

                // Add totals to CrawlTest to be included in manifest and test-info.json files
                CrawlTest.UrlTotal = CrawlContext.VisitedUrls.Count;
                CrawlTest.AppsUniqueTotal = CrawlerCommon.GetUniqueAppTotal(CrawlContext.IcWebPages.ToList());
                CrawlTest.AppsTotal = CrawlerCommon.GetAllAppsTotal(CrawlContext.IcWebPages.ToList());

                CrawlTest.PageScreenshotsTotal = CrawlContext.PageScreenshots.Count;
                CrawlTest.AppScreenshotsTotal = CrawlContext.AppScreenshots.Count;

                // Save test metadata to test-info.json
                ReportWriter.SaveModelAsJsonFile(CrawlTest, CrawlTest.BaseSaveFolder, "test-info");

                // Save visited URLs to urls.json
                ReportWriter.SaveReport(CrawlContext.VisitedUrls, CrawlTest.BaseSaveFolder, "urls");

                // Save captured app HTML if requested
                if (request.CaptureAppHtml)
                {
                    ReportWriter.SaveReport(CrawlContext.AppMarkups, CrawlTest.BaseSaveFolder, "app-html");
                    Logger.Info("Captured App Html log reports saved successfully.");
                }

                // Save captured app text if requested
                if (request.CaptureAppText)
                {
                    ReportWriter.SaveReport(CrawlContext.AppTexts, CrawlTest.BaseSaveFolder, "app-text");
                    Logger.Info("Captured App Text log reports saved successfully.");
                }

                // Save page screenshots if requested
                if (request.TakePageScreenshots)
                {
                    ReportWriter.SaveReport(CrawlContext.PageScreenshots, CrawlTest.BaseSaveFolder, "page-screenshots");
                    Logger.Info("Captured Page screenshots log reports saved successfully.");
                }

                // Save app screenshots if requested
                if (request.TakeAppScreenshots)
                {
                    ReportWriter.SaveReport(CrawlContext.AppScreenshots, CrawlTest.BaseSaveFolder, "app-screenshots");
                    Logger.Info("Captured App screenshots log reports saved successfully.");
                }

                // Save pages and apps data to pages-and-apps.json
                ReportWriter.SaveReport(CrawlContext.IcWebPages, CrawlTest.BaseSaveFolder, "pages-and-apps");

                try
                {
                    // Save JavaScript console errors if any exist
                    if (CrawlContext.ConsoleJsErrors.Any())
                    {
                        // Convert the dictionary of console errors to a list of JsConsoleError objects
                        List<JsConsoleError> jsConsoleErrors = CrawlContext.ConsoleJsErrors
                            .Where(entry => !string.IsNullOrWhiteSpace(entry.Key)) // Ensure key is valid
                            .Select(entry =>
                            {
                                // Find the matching UrlModel by FullUrl
                                var urlModel = CrawlContext.VisitedUrls.FirstOrDefault(url => url.FullUrl == entry.Key);

                                if (urlModel != null)
                                {
                                    // Convert List<string> to List<JsError>
                                    var convertedErrors = entry.Value
                                        .Select(err => new JsError { ErrorMsg = err })
                                        .ToList();

                                    return new JsConsoleError
                                    {
                                        UrlModel = urlModel,
                                        Errors = convertedErrors
                                    };
                                }

                                return null; // Ignore if no match found
                            })
                            .Where(error => error is not null) // Remove nulls
                            .ToList()!;

                        if (jsConsoleErrors.Any()) // Ensure there are errors before saving
                        {
                            ReportWriter.SaveReport(jsConsoleErrors, CrawlTest.BaseSaveFolder, "js-console-errors");
                            Logger.Info("Captured JavaScript console error reports saved successfully.");
                        }
                    }
                } catch (Exception ex)
                {
                    // Log any errors that occur while saving JavaScript console errors
                    Logger.Error(ex, "Something went wrong trying to convert and save js console error dictionary");
                }

                // Save grouped XHR timings if available
                if (CrawlTest.GroupedXhrTimings?.Any() == true)
                {
                    //ReportWriter.SaveModelAsJsonFile(CrawlTest.GroupedXhrTimings, CrawlTest.BaseSaveFolder, "grouped-xhr-timings");
                    // Convert XHR timings to include UrlModels for better context
                    var xhrWithUrlModels = CrawlTest.GroupedXhrTimings.ToWithUrlModel(CrawlContext.VisitedUrls);
                    ReportWriter.SaveModelAsJsonFile(xhrWithUrlModels, CrawlTest.BaseSaveFolder, "xhr-timings");

                    Logger.Info("Grouped XHR timings report saved successfully.");
                }

                // Update the tests manifest file
                string testsManifestFile = Path.Combine(SiteArtifactsWinPath, "tests.json");

                // Update the manifest
                await WriterQueueService.EnqueueAsync(async () =>
                {
                    // Update the manifest with the current test
                    ReportWriter.UpdateJsonManifest(testsManifestFile, CrawlTest);

                    // Prune old entries from the manifest to keep it manageable
                    ReportWriter.PruneTestsManifest(testsManifestFile, Logger);
                });
            }
            catch (Exception ex)
            {
                // Log any errors that occur during the JSON file creation process
                Logger.Error(ex, "<<Error>> Failed to save reports.");
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");

                // Ensure the Playwright context is disposed of in case of an error
                if (context != null)
                {
                    await context.CloseAsync();
                    Logger.Info("Playwright context disposed successfully.");
                }

                // Rethrow the exception to propagate the error
                throw;
                //return new TestResult { Success = false, ErrorMessage = "Failed to save reports." };
            }
        }

        private void LogBaselineTestPostRequestModel(BaselineTestPostRequestModel model)
        {
            var logMessage = new StringBuilder();
            logMessage.AppendLine("Logging initially submitted 'BaselineTestPostRequestModel' properties:");

            AppendProperties(logMessage, model, 0); // Call helper method to handle recursion

            Logger.Info(logMessage.ToString());
        }

        /// <summary>
        /// Recursively appends the properties of an object to a log message.
        /// Handles primitive types, strings, collections, and nested objects.
        /// </summary>
        /// <param name="logMessage">The StringBuilder to which the properties will be appended.</param>
        /// <param name="obj">The object whose properties are being logged.</param>
        /// <param name="indentLevel">The current level of indentation for nested properties.</param>
        private void AppendProperties(StringBuilder logMessage, object obj, int indentLevel)
        {
            // If the object is null, log "null" and return
            if (obj == null)
            {
                logMessage.AppendLine($"{new string(' ', indentLevel * 2)}null");
                return;
            }

            // Get the type of the object
            var type = obj.GetType();

            // If the object is a primitive type, string, DateTime, Guid, or decimal, log its value directly
            if (type.IsPrimitive || obj is string || obj is DateTime || obj is Guid || obj is decimal)
            {
                logMessage.AppendLine($"{new string(' ', indentLevel * 2)}{obj}");
                return;
            }

            // If the object is a collection, iterate through its items and log them recursively
            if (obj is IEnumerable<object> collection)
            {
                logMessage.AppendLine($"{new string(' ', indentLevel * 2)}Collection:");
                foreach (var item in collection)
                {
                    AppendProperties(logMessage, item, indentLevel + 1);
                }
                return;
            }

            // For other object types, log their properties
            foreach (var property in type.GetProperties())
            {
                // Get the value of the property
                var value = property.GetValue(obj);

                // Append the property name with the current indentation
                logMessage.Append($"{new string(' ', indentLevel * 2)}{property.Name}: ");

                if (value == null)
                {
                    // If the property value is null, log "null"
                    logMessage.AppendLine("null");
                }
                else if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                {
                    // If the property is a class (but not a string), log its nested properties recursively
                    logMessage.AppendLine();
                    AppendProperties(logMessage, value, indentLevel + 1); // Recursively log nested properties
                }
                else
                {
                    // For other types, log the property value directly
                    logMessage.AppendLine(value.ToString());
                }
            }
        }

    }
}
