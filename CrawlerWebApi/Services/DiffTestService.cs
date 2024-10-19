using CrawlerWebApi.Interfaces;
using CrawlerWebApi.Models;
using IC.Test.Playwright.Crawler.Drivers;
using IC.Test.Playwright.Crawler.Models;
using IC.Test.Playwright.Crawler.Utility;
using NLog;
using System.Text;

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
                _logger.Info("<<TestStarted>>");

                TimerUtil.StartTimer(_testModel.Timers, "DiffDuration");

                _diffContext.BaseTestSavePath = PathUtil.ReplaceDoubleBackslashes(request.BaseTestPath);
                _diffContext.NewTestSavePath = PathUtil.ReplaceDoubleBackslashes(request.NewTestPath);

                // Set values for test model
                _testModel.Name = "Diff test";
                string diffPathPartial = PathUtil.CreateSavePath("diff-tests");
                _testModel.BaseSaveFolder = $"{diffPathPartial}__{_testModel.Id}";
                _testModel.Description = $"Comparing baseline test '{_diffContext.BaseTestSavePath}' and new test '{_diffContext.NewTestSavePath}'";
                _testModel.DateTime = DateTime.Now;
                
                // ----------------------------------------------------
                // Compare Page screenshots and create diffs
                // ----------------------------------------------------
                string imageDir = "page-screenshots";
                string BaseTestImgPath = Path.Combine(_diffContext.BaseTestSavePath, imageDir);
                string NewTestImgPath = Path.Combine(_diffContext.NewTestSavePath, imageDir);
                string DiffTestImgPath = Path.Combine(_testModel.BaseSaveFolder, imageDir);
                _diffDriver.DiffImgDirs(BaseTestImgPath, NewTestImgPath, DiffTestImgPath);
                string pageScreenshotPath = Path.Combine(_testModel.BaseSaveFolder, imageDir);
                ReportWriter.SaveReport(_diffContext.ScreenshotDiffs, pageScreenshotPath, "screenshot-diffs");

                // ----------------------------------------------------
                // Compare App screenshots and create diffs
                // ----------------------------------------------------
                imageDir = "app-screenshots";
                BaseTestImgPath = Path.Combine(_diffContext.BaseTestSavePath, imageDir);
                NewTestImgPath = Path.Combine(_diffContext.NewTestSavePath, imageDir);
                DiffTestImgPath = Path.Combine(_testModel.BaseSaveFolder, imageDir);
                _diffDriver.DiffImgDirs(BaseTestImgPath, NewTestImgPath, DiffTestImgPath);
                string appScreenshotPath = Path.Combine(_testModel.BaseSaveFolder, imageDir);
                ReportWriter.SaveReport(_diffContext.ScreenshotDiffs, appScreenshotPath, "screenshot-diffs");
                
                // ----------------------------------------------------
                // Compare App Html markup and create reports
                // ----------------------------------------------------
                string dotFileType = ".html";
                string subDirPath = "app-html";
                string jsonFile = "app-markup.json";
                bool ignoreTextContent = true;
                string baseJsonFilePath = Path.Combine(_diffContext.BaseTestSavePath, jsonFile);
                string newJsonFilePath = Path.Combine(_diffContext.NewTestSavePath, jsonFile);
                string DiffSavePath = Path.Combine(_testModel.BaseSaveFolder, subDirPath);
                FileInfo _baseJsonFile = new FileInfo(baseJsonFilePath);
                FileInfo _newJsonFile = new FileInfo(newJsonFilePath);

                await _diffDriver.DiffHtmlFiles(_baseJsonFile, _newJsonFile, DiffSavePath, dotFileType, ignoreTextContent);

                // ----------------------------------------------------
                // Compare App text and create reports
                // ----------------------------------------------------
                dotFileType = ".txt";
                subDirPath = "app-innertext";
                jsonFile = "app-text.json";
                ignoreTextContent = false;
                baseJsonFilePath = Path.Combine(_diffContext.BaseTestSavePath, jsonFile);
                newJsonFilePath = Path.Combine(_diffContext.NewTestSavePath, jsonFile);
                DiffSavePath = Path.Combine(_testModel.BaseSaveFolder, subDirPath);
                _baseJsonFile = new FileInfo(baseJsonFilePath);
                _newJsonFile = new FileInfo(newJsonFilePath);
                await _diffDriver.DiffTextFiles(_baseJsonFile, _newJsonFile, DiffSavePath, ignoreTextContent);

                // stop timer
                TimerUtil.StopTimer(_testModel.Timers, "DiffDuration");
                _testModel.Duration = TimerUtil.GetElapsedTime(_testModel.Timers, "DiffDuration");

                // Update manifest file
                ReportWriter.UpdateJsonManifest(@"C:\ictf\diff-tests\tests.json", _testModel);

                // copy baseline and newtest manifest files to diff base save for easy access
                string sourceInfoFile = "test-info.json";
                await FileUtil.CopyFileAsync(_diffContext.BaseTestSavePath, _testModel.BaseSaveFolder, sourceInfoFile, "baseline-test-info.json");
                await FileUtil.CopyFileAsync(_diffContext.NewTestSavePath, _testModel.BaseSaveFolder, sourceInfoFile, "new-test-info.json");

                // log all timers
                var allTimings = TimerUtil.GetAllTimings(_testModel.Timers);
                StringBuilder sb = new StringBuilder();
                sb.Append("\r\n==== Time Reports ====\r\n");
                foreach (var timing in allTimings)
                {
                    sb.Append($"- {timing.Key} took {timing.Value.TotalSeconds} seconds.\r\n");
                }
                _logger.Info(sb.ToString());

                // copy log to save path
                CopySpecflowLogToSavePath(_testModel.BaseSaveFolder);

                // ***************************************
                _logger.Info("<<TestEnded>>");
                return new TestResult { Success = true, ErrorMessage = "Successfully completed diff test" };

            } catch (Exception ex)
            {
                _logger.Info("<<TestError>>, <<TestEnded>>");
                _logger.Error(ex, "<<Error>> Unexpected error during diff test execution.");
                return new TestResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private void CopySpecflowLogToSavePath(string savePath)
        {
            try
            {
                // copy specflow log file
                string LogFilePath = @"C:\temp";
                //string LogFileName = "specflow-console.log";
                string LogFileFullPath = Path.Combine(LogFilePath, _testModel.LogFileName);
                string LogFileDestFullPath = Path.Combine(savePath, _testModel.LogFileName);
                int maxRetries = 5;
                int delay = 1000; // milliseconds

                //await FileUtil.CopyFileAsync(LogFilePath, savePath, LogFileName);
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        // Attempt to copy the log file
                        _logger.Info($"Will try to copy log file from '{LogFileFullPath}' to '{LogFileDestFullPath}'");
                        File.Copy(LogFileFullPath, LogFileDestFullPath, true);
                        _logger.Info("Log file was copied successfully");
                        break; // Exit loop if copy is successful
                    }
                    catch (IOException ex)
                    {
                        // Log the exception if needed
                        _logger.Error($"<<Error>> Failed to copy log file: {ex.Message}");

                        // Wait for the specified delay before retrying
                        Thread.Sleep(delay);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"<<Error>> Something went wrong trying to copy the log file. Error: {ex.Message}");
            }
        }
    }
}
