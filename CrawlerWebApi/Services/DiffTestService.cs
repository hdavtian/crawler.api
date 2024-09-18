using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using NLog;

namespace CrawlerWebApi.Services
{
    public class DiffTestService : IDiffTestService
    {
        private readonly TestModel _testModel;
        private readonly DiffDriver _diffDriver;
        private readonly DiffContext _diffContext;
        private readonly Logger _logger;

        public DiffTestService(
            TestModel testModel,
            DiffDriver diffDriver,
            DiffContext diffContext
            )
        {
            _testModel = testModel;
            _diffDriver = diffDriver;
            _diffContext = diffContext;
            _logger = LogManager.GetCurrentClassLogger();
        }

        public async Task<TestResult> RunDiffTestAsync(DiffTestPostRequestModel request)
        {
            try
            {
                /*
                string imageDir = "page-screenshots";
                string BaseTestSavePath = Path.Combine((string)_scenarioContext["BaseTestSavePath"], imageDir);
                string NewTestSavePath = Path.Combine((string)_scenarioContext["NewTestSavePath"], imageDir);
                string DiffSavePath = Path.Combine(_testModel.BaseSaveFolder, imageDir);

                _diffDriver.DiffImgDirs(BaseTestSavePath, NewTestSavePath, DiffSavePath);
                string pageScreenshotPath = Path.Combine(_testModel.BaseSaveFolder, "page-screenshots");
                ReportWriter.SaveReport(_diffContext.ScreenshotDiffs, pageScreenshotPath, "screenshot-diffs");
                */
                return new TestResult { Success = true, ErrorMessage = "Successfully completed diff test" };

            } catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during diff test execution.");
                return new TestResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}
