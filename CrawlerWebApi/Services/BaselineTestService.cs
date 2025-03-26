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

namespace CrawlerWebApi.Services
{
    public class BaselineTestService : IBaselineTestService
    {
        private readonly PlaywrightContext PlaywrightContext;
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

        public BaselineTestService(
            PlaywrightContext PlaywrightContext,
            LoginDriver LoginDriver,
            CrawlTest CrawlTest,
            CrawlDriver CrawlDriver,
            CrawlContext CrawlContext,
            CrawlerCommon CrawlerCommon,
            IConfiguration AppConfiguration,
            WriterQueueService WriterQueueService,
            ILoggingProvider loggingProvider
        )
        {
            this.PlaywrightContext = PlaywrightContext;
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
        }

        public async Task<TestResult> RunBaselineTestAsync(BaselineTestPostRequestModel request)
        {
            try
            {
                Logger.Info("<<TestStarted>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestStarted, "The crawl test has started");
                Logger.Info("TestId: " + CrawlTest.Id);

                LogBaselineTestPostRequestModel(request);

                // lets set relavant flags in CrawlContext
                CrawlContext.TakePageScreenshots = request.TakePageScreenshots;
                CrawlContext.TakeAppScreenshots = request.TakeAppScreenshots;
                CrawlContext.CaptureAppHtml = request.CaptureAppHtml;
                CrawlContext.CaptureAppText = request.CaptureAppText;
                CrawlContext.GenerateAxeReports = request.GenerateAxeReports;
                CrawlContext.SaveNetworkTraffic = request.SaveNetworkTraffic;

                string _harFileName = $"{CrawlTest.Id}.har";

                // Unique crawl duration timer name
                string CrawlDurationTimerName = $"CrawlDuration_{Guid.NewGuid()}";

                // Set up test model
                try
                {
                    TimerUtil.StartTimer(CrawlTest.Timers, CrawlDurationTimerName);
                    CrawlTest.Name = "";
                    CrawlTest.Description = "";
                    CrawlTest.DateTime = DateTime.Now;
                    CrawlTest.Browser.Width = request.WindowWidth;
                    CrawlTest.Browser.Height = request.WindowHeight;
                    string projectNameSubdomain = UrlUtil.GetSubdomainFromUrl(request.Url).ToLower();
                    CrawlTest.BaseSaveFolder = PathUtil.CreateSavePath("crawl-tests", projectNameSubdomain, request.WindowWidth, request.WindowHeight, CrawlTest.Id.ToString());
                    CrawlTest.ExtraUrls = request.ExtraUrls;
                    CrawlTest.OnlyCrawlExtraUrls = request.OnlyCrawlExtraUrls;
                    CrawlTest.PtierVersion = request.PtierVersion;
                    CrawlTest.TaffieUser = request.TaffieUser;
                    Logger.Info("Test model set up successfully.");
                    Logger.Info($"BaseSaveFolder: {CrawlTest.BaseSaveFolder}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to set up test model.");
                    Logger.Info("<<TestEnded>>");
                    Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                    await PlaywrightContext.DisposeAsync();
                    Logger.Info("Playwright context disposed successfully.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to set up test model." };
                }

                // Initialize PlaywrightContext
                try
                {
                    PlaywrightContext._testId = CrawlTest.Id;
                    PlaywrightContext.SetBrowserTypeByName(request.Browser);
                    PlaywrightContext._headless = CrawlTest.Browser.Headless = request.Headless;
                    PlaywrightContext._browserWidth = request.WindowWidth;
                    PlaywrightContext._browserHeight = request.WindowHeight;

                    PlaywrightContext._recordVideo = request.RecordVideo;
                    if (request.RecordVideo)
                    {
                        PlaywrightContext._videoSavePath = Path.Combine(CrawlTest.BaseSaveFolder, "videos");
                    }
                    
                    PlaywrightContext._saveHar = request.SaveHar;

                    if (request.SaveHar)
                    {
                        PlaywrightContext._harPath = CrawlTest.BaseSaveFolder;
                    }

                    await PlaywrightContext.InitializeAsync();
                    CrawlTest.Browser.Name = PlaywrightContext.BrowserName;
                    Logger.Info("Playwright context initialized successfully.");

                    await CrawlerCommon.SetViewPortSizeAsync(CrawlTest.Browser.Width, CrawlTest.Browser.Height);
                    Logger.Info($"Set browser viewport to {CrawlTest.Browser.Width}px by {CrawlTest.Browser.Height}px (width x height)");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to initialize Playwright context.");
                    Logger.Info("<<TestEnded>>");
                    Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                    await PlaywrightContext.DisposeAsync();
                    Logger.Info("Playwright context disposed successfully.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to initialize Playwright context." };
                }

                // Setup network interception
                CrawlTest.NetworkData = new List<NetworkData>();
                var page = PlaywrightContext.Page;
                string _currentPageUrl = page.Url;

                try
                {
                    page.FrameNavigated += (_, frame) =>
                    {
                        if (frame == page.MainFrame)
                        {
                            _currentPageUrl = frame.Url;
                        }
                    };

                    page.Request += (_, request) =>
                    {
                        //Logger.Info($"Request intercepted: {request.Url}");
                        CrawlTest.NetworkData.Add(new NetworkData
                        {
                            Url = request.Url,
                            Method = request.Method,
                            Headers = request.Headers,
                            PostData = request.PostData,
                            PageUrl = _currentPageUrl
                        });
                    };

                    // At test end, when it's time to write this to a json file, will convert to a different collection
                    // so that with get urlModel instead of just a string representing the url (UrlModel will have page title etc)
                    // because UrlModel is not available here it comes from ictf.js execution on pages
                    page.Console += (_, msg) =>
                    {
                        if (msg.Type == "error")
                        {
                            string url = page.Url;
                            string errorMessage = $"[Console Javascript Error] {msg.Text} (URL: {url})";

                            // Ensure the key exists
                            if (!CrawlContext.ConsoleJsErrors.ContainsKey(url))
                            {
                                CrawlContext.ConsoleJsErrors[url] = new List<string>();
                            }

                            // Add error only if it's not already present
                            if (!CrawlContext.ConsoleJsErrors[url].Contains(msg.Text))
                            {
                                CrawlContext.ConsoleJsErrors[url].Add(msg.Text);
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
                            // Update the response information
                            matchingRequest.StatusCode = response.Status;

                            // Capture response headers
                            matchingRequest.ResponseHeaders = response.Headers;

                            try
                            {
                                // Capture the response body as bytes
                                matchingRequest.ResponseBody = await response.BodyAsync();

                                // Optionally capture the response body as a string (if it's text-based)
                                matchingRequest.ResponseBodyAsString = await response.TextAsync();
                            }
                            catch (Exception ex)
                            {
                                //Logger.Warn($"Failed to capture response body for {response.Url}: {ex.Message}");
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to set up network interception.");
                    Logger.Info("<<TestEnded>>");
                    Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                    await PlaywrightContext.DisposeAsync();
                    Logger.Info("Playwright context disposed successfully.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to set up network interception." };
                }

                // Perform login
                if (request.RequiresLogin)
                {
                    try
                    {
                        await LoginDriver.LoginToApplication(request.Url, request.Username, request.Password);
                        Logger.Info("Login performed successfully.");

                        // if CrawlType.LoginOnly then return test
                        if (CrawlTest.CrawlType.Equals(CrawlType.LoginOnly))
                        {
                            Logger.Info($"<<TestEnded>> This was a '{CrawlTest.CrawlType}' test");
                            Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                            await PlaywrightContext.DisposeAsync();
                            Logger.Info("Playwright context disposed successfully.");
                            return new TestResult { Success = true};
                        }
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
                        await PlaywrightContext.DisposeAsync();
                        Logger.Info("Playwright context disposed successfully.");
                        return new TestResult { Success = false, ErrorMessage = errMsg };
                    }
                } 
                else
                {
                    await CrawlerCommon.NavigateToUrlAsync(request.Url);
                    CrawlTest.BaseUrl = CrawlerCommon.ExtractSchemeAndTLD(request.Url);
                }

                // Disable any unsupported v1 operations in case user requested via crawl test form
                // We are doing this after login because that's where we determine site version
                if (CrawlTest.SiteVersion is SiteVersion.V1)
                {
                    StringBuilder logMsg = new StringBuilder();
                    if (CrawlContext.TakeAppScreenshots)
                    {
                        CrawlContext.TakeAppScreenshots = false;
                        logMsg.AppendLine("V1 crawl currently does not support taking app screenshots, will skip this operation. ");
                    }
                    if (CrawlContext.CaptureAppHtml)
                    {
                        CrawlContext.CaptureAppHtml = false;
                        logMsg.AppendLine("V1 crawl currently does not support capturing app html, will skip this operation. ");

                    }
                    if (CrawlContext.CaptureAppText)
                    {
                        logMsg.AppendLine("V1 crawl currently does not support capturing app text, will skip this operation. ");
                        CrawlContext.CaptureAppText = false;
                    }

                    if (logMsg.Length != 0)
                    {
                        Logger.Info(logMsg.ToString());
                    }
                }

                // Perform crawl
                try
                {
                    // slight wait for the forwards to complete to dash after login
                    await CrawlerCommon.WaitInMilliseconds(3000);
                    await CrawlDriver.Crawl();
                    Logger.Info("Crawl completed successfully.");
                }
                catch (Exception ex)
                {
                    string errMsg = "<<Error>> " + ex.Message;
                    Logger.Error(ex, errMsg);
                    Logger.Info("<<TestEnded>>");
                    Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                    await PlaywrightContext.DisposeAsync();
                    Logger.Info("Playwright context disposed successfully.");
                    return new TestResult { Success = false, ErrorMessage = errMsg };
                }

                // Stop timer and assign duration
                try
                {
                    TimerUtil.StopTimer(CrawlTest.Timers, CrawlDurationTimerName);
                    CrawlTest.Duration = TimerUtil.GetElapsedTime(CrawlTest.Timers, CrawlDurationTimerName);
                    CrawlTest.BaseUrl = request.Url;
                    Logger.Info("Timer stopped and duration assigned successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to stop timer and assign duration.");
                    Logger.Info("<<TestEnded>>");
                    Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                    await PlaywrightContext.DisposeAsync();
                    Logger.Info("Playwright context disposed successfully.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to stop timer and assign duration." };
                }

                // Create json files
                try
                {
                    await createJsonFilesForCrawlTest(request);
                }
                catch (Exception ex)
                {
                    return new TestResult { Success = false, ErrorMessage = "Failed to save reports." };
                }

                // Dispose of Playwright context
                try
                {
                    await PlaywrightContext.DisposeAsync();
                    Logger.Info("Playwright context disposed successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to dispose Playwright context.");
                    Logger.Info("<<TestEnded>>");
                    Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                    await PlaywrightContext.DisposeAsync();
                    Logger.Info("Playwright context disposed successfully.");
                    return new TestResult { Success = false, ErrorMessage = "Failed to dispose Playwright context." };
                }

                // If everything is successful
                Logger.Info("Baseline test executed successfully.");

                // move log to save path
                await FileUtil.MoveFileAsync(SourceFilePath, CrawlTest.BaseSaveFolder, CrawlTest.LogFileName, Logger);
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                await PlaywrightContext.DisposeAsync();
                Logger.Info("Playwright context disposed successfully.");
                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                Logger.Info("<<TestError>>, <<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                Logger.Error(ex, "<<Error>> Unexpected error during baseline test execution.");
                await PlaywrightContext.DisposeAsync();
                Logger.Info("Playwright context disposed successfully.");
                return new TestResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task createJsonFilesForCrawlTest(BaselineTestPostRequestModel request)
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
                if (request.SaveNetworkTraffic)
                {
                    ReportWriter.SaveModelAsJsonFile(CrawlTest.NetworkData, CrawlTest.BaseSaveFolder, "networkData");
                    Logger.Info("Network log reports saved successfully.");
                }

                // Add totals to CrawlTest to be included in manifest and test-info.json files
                CrawlTest.UrlTotal = CrawlContext.VisitedUrls.Count;
                CrawlTest.AppsUniqueTotal = CrawlerCommon.GetUniqueAppTotal(CrawlContext.IcWebPages);
                CrawlTest.AppsTotal = CrawlerCommon.GetAllAppsTotal(CrawlContext.IcWebPages);
                CrawlTest.PageScreenshotsTotal = CrawlContext.PageScreenshots.Count;
                CrawlTest.AppScreenshotsTotal = CrawlContext.AppScreenshots.Count;

                ReportWriter.SaveModelAsJsonFile(CrawlTest, CrawlTest.BaseSaveFolder, "test-info");
                ReportWriter.SaveReport(CrawlContext.VisitedUrls, CrawlTest.BaseSaveFolder, "urls");

                if (request.CaptureAppHtml)
                {
                    ReportWriter.SaveReport(CrawlContext.AppMarkups, CrawlTest.BaseSaveFolder, "app-html");
                    Logger.Info("Captured App Html log reports saved successfully.");
                }

                if (request.CaptureAppText)
                {
                    ReportWriter.SaveReport(CrawlContext.AppTexts, CrawlTest.BaseSaveFolder, "app-text");
                    Logger.Info("Captured App Text log reports saved successfully.");
                }

                if (request.TakePageScreenshots)
                {
                    ReportWriter.SaveReport(CrawlContext.PageScreenshots, CrawlTest.BaseSaveFolder, "page-screenshots");
                    Logger.Info("Captured Page screenshots log reports saved successfully.");
                }

                if (request.TakeAppScreenshots)
                {
                    ReportWriter.SaveReport(CrawlContext.AppScreenshots, CrawlTest.BaseSaveFolder, "app-screenshots");
                    Logger.Info("Captured App screenshots log reports saved successfully.");
                }

                ReportWriter.SaveReport(CrawlContext.IcWebPages, CrawlTest.BaseSaveFolder, "pages-and-apps");

                try
                {
                    if (CrawlContext.ConsoleJsErrors.Any()) // Ensure there are errors before saving
                    {
                        // Convert Dictionary<string, List<string>> to List<JsConsoleError>
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
                    Logger.Error(ex, "Something went wrong trying to convert and save js console error dictionary");
                }

                string testsManifestFile = Path.Combine(SiteArtifactsWinPath, "tests.json");

                // Update the manifest
                await WriterQueueService.EnqueueAsync(async () =>
                {
                    ReportWriter.UpdateJsonManifest(testsManifestFile, CrawlTest);
                    ReportWriter.PruneTestsManifest(testsManifestFile, Logger);
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "<<Error>> Failed to save reports.");
                Logger.Info("<<TestEnded>>");
                Logger.RaiseEvent(TaffieEventType.CrawlTestEnded, "Crawl test has ended");
                await PlaywrightContext.DisposeAsync();
                Logger.Info("Playwright context disposed successfully.");
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

        /**
         * Recursively appends properties to the log message.
         */
        private void AppendProperties(StringBuilder logMessage, object obj, int indentLevel)
        {
            if (obj == null)
            {
                logMessage.AppendLine($"{new string(' ', indentLevel * 2)}null");
                return;
            }

            var type = obj.GetType();

            if (type.IsPrimitive || obj is string || obj is DateTime || obj is Guid || obj is decimal)
            {
                logMessage.AppendLine($"{new string(' ', indentLevel * 2)}{obj}");
                return;
            }

            if (obj is IEnumerable<object> collection) // Handle collections
            {
                logMessage.AppendLine($"{new string(' ', indentLevel * 2)}Collection:");
                foreach (var item in collection)
                {
                    AppendProperties(logMessage, item, indentLevel + 1);
                }
                return;
            }

            // Log properties of objects (excluding value types, strings, and collections)
            foreach (var property in type.GetProperties())
            {
                var value = property.GetValue(obj);

                logMessage.Append($"{new string(' ', indentLevel * 2)}{property.Name}: ");

                if (value == null)
                {
                    logMessage.AppendLine("null");
                }
                else if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
                {
                    logMessage.AppendLine();
                    AppendProperties(logMessage, value, indentLevel + 1); // Recursively log nested properties
                }
                else
                {
                    logMessage.AppendLine(value.ToString());
                }
            }
        }

    }
}
