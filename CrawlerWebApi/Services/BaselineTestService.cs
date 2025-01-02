using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using NLog;
using Microsoft.Extensions.Configuration;
using System.Text;
using AngleSharp.Dom;

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
        private readonly Logger Logger;
        private readonly string ApiRootWinPath;
        private readonly string SiteArtifactsWinPath;
        private readonly WriterQueueService WriterQueueService;

        public BaselineTestService(
            PlaywrightContext PlaywrightContext,
            LoginDriver LoginDriver,
            CrawlTest CrawlTest,
            CrawlDriver CrawlDriver,
            CrawlContext CrawlContext,
            CrawlerCommon CrawlerCommon,
            IConfiguration AppConfiguration,
            WriterQueueService WriterQueueService
        )
        {
            this.PlaywrightContext = PlaywrightContext;
            this.LoginDriver = LoginDriver;
            this.CrawlTest = CrawlTest;
            this.CrawlDriver = CrawlDriver;
            this.CrawlContext = CrawlContext;
            this.CrawlerCommon = CrawlerCommon;
            Logger = LogManager.GetCurrentClassLogger();
            ApiRootWinPath = AppConfiguration["ApiRootWinPath"];
            SiteArtifactsWinPath = AppConfiguration["SiteArtifactsWinPath"];
            this.WriterQueueService = WriterQueueService;
        }

        public async Task<TestResult> RunBaselineTestAsync(BaselineTestPostRequestModel request)
        {
            try
            {
                Logger.Info("<<TestStarted>>");
                Logger.Info("TestId: " + CrawlTest.Id);

                LogBaselineTestPostRequestModel(request);

                // lets set relavant flags in CrawlContext
                CrawlContext.TakePageScreenshots = request.TakePageScreenshots;
                CrawlContext.TakeAppScreenshots = request.TakeAppScreenshots;
                CrawlContext.CaptureAppHtml = request.CaptureAppHtml;
                CrawlContext.CaptureAppText = request.CaptureAppText;
                CrawlContext.GenerateAxeReports = request.GenerateAxeReports;
                CrawlContext.CaptureNetworkTraffic = request.CaptureNetworkTraffic;

                string _harFileName = $"{CrawlTest.Id}.har";

                // Set up test model
                try
                {
                    TimerUtil.StartTimer(CrawlTest.Timers, "ScenarioDuration");
                    CrawlTest.Name = "";
                    CrawlTest.Description = "";
                    CrawlTest.DateTime = DateTime.Now;
                    CrawlTest.Browser.Width = request.WindowWidth;
                    CrawlTest.Browser.Height = request.WindowHeight;
                    string projectNameSubdomain = UrlUtil.GetSubdomainFromUrl(request.Url).ToLower();
                    CrawlTest.BaseSaveFolder = PathUtil.CreateSavePath("crawl-tests", projectNameSubdomain, request.WindowWidth, request.WindowHeight, CrawlTest.Id.ToString());
                    CrawlTest.ExtraUrls = request.ExtraUrls;
                    CrawlTest.PtierVersion = request.PtierVersion;
                    Logger.Info("Test model set up successfully.");
                    Logger.Info($"BaseSaveFolder: {CrawlTest.BaseSaveFolder}");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to set up test model.");
                    Logger.Info("<<TestEnded>>");
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
                    return new TestResult { Success = false, ErrorMessage = "Failed to initialize Playwright context." };
                }

                // Setup network interception
                CrawlTest.NetworkData = new List<NetworkData>();
                if (request.CaptureNetworkTraffic)
                {
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
                        return new TestResult { Success = false, ErrorMessage = "Failed to set up network interception." };
                    }
                }

                // Perform login
                if (request.RequiresLogin)
                {
                    try
                    {
                        await LoginDriver.LoginToApplication(request.Url, request.Username, request.Password);
                        Logger.Info("Login performed successfully.");
                    }
                    catch (Exception ex)
                    {
                        string errMsg = "<<Error>> Failed during login process.";
                        Logger.Error(ex, errMsg);
                        Logger.Info("<<TestEnded>>");
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
                    await CrawlDriver.Crawl();
                    Logger.Info("Crawl completed successfully.");
                }
                catch (Exception ex)
                {
                    string errMsg = "<<Error>> " + ex.Message;
                    Logger.Error(ex, errMsg);
                    Logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = errMsg };
                }

                // Stop timer and assign duration
                try
                {
                    TimerUtil.StopTimer(CrawlTest.Timers, "ScenarioDuration");
                    CrawlTest.Duration = TimerUtil.GetElapsedTime(CrawlTest.Timers, "ScenarioDuration");
                    CrawlTest.BaseUrl = request.Url;
                    Logger.Info("Timer stopped and duration assigned successfully.");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to stop timer and assign duration.");
                    Logger.Info("<<TestEnded>>");
                    return new TestResult { Success = false, ErrorMessage = "Failed to stop timer and assign duration." };
                }

                // Save reports
                try
                {
                    // For now lets not save the network traffic because logs get too large
                    // I still monitor it for login errors at beginning so the front end baseline launch form option to 'Capture Network Traffic' needs to be checked
                    /*
                    if (request.CaptureNetworkTraffic)
                    {
                        ReportWriter.SaveModelAsJsonFile(CrawlTest.NetworkData, CrawlTest.BaseSaveFolder, "networkData");
                        Logger.Info("Network log reports saved successfully.");
                    }
                    */

                    // Add totals to CrawlTest to be included in manifest and test-info.json files
                    CrawlTest.UrlTotal = CrawlContext.VisitedUrls.Count;
                    CrawlTest.AppsUniqueTotal = ReportWriter.GetUniqueAppTotal(CrawlContext.IcWebPages);
                    CrawlTest.AppsTotal = ReportWriter.GetAllAppsTotal(CrawlContext.IcWebPages);
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
                    string testsManifestFile = Path.Combine(SiteArtifactsWinPath, "tests.json");

                    // Update the manifest
                    await WriterQueueService.EnqueueAsync(async () =>
                    {
                        ReportWriter.UpdateJsonManifest(testsManifestFile, CrawlTest);
                        ReportWriter.PruneTestsManifest(testsManifestFile);
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "<<Error>> Failed to save reports.");
                    Logger.Info("<<TestEnded>>");
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
                    return new TestResult { Success = false, ErrorMessage = "Failed to dispose Playwright context." };
                }

                // If everything is successful
                Logger.Info("Baseline test executed successfully.");

                // move log to save path
                FileUtil.MoveFileAsync(@"c:\Temp", CrawlTest.BaseSaveFolder,CrawlTest.LogFileName, Logger);
                Logger.Info("<<TestEnded>>");

                return new TestResult { Success = true };
            }
            catch (Exception ex)
            {
                Logger.Info("<<TestError>>, <<TestEnded>>");
                Logger.Error(ex, "<<Error>> Unexpected error during baseline test execution.");
                return new TestResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private void LogBaselineTestPostRequestModel(BaselineTestPostRequestModel model)
        {
            var properties = model.GetType().GetProperties();
            var logMessage = new StringBuilder();

            logMessage.AppendLine("Logging initially submitted 'BaselineTestPostRequestModel' properties:");

            foreach (var property in properties)
            {
                var value = property.Name.Equals("Password", StringComparison.OrdinalIgnoreCase)
                    ? "" // Mask the password value
                    : property.GetValue(model) ?? "null"; // Log the actual value or "null" if not set

                logMessage.AppendLine($"{property.Name}: {value}");
            }

            Logger.Info(logMessage.ToString());
        }
    }
}
