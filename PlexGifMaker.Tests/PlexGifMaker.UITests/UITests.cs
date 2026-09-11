using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;

namespace PlexGifMaker.Tests.PlexGifMaker.UITests
{
    public class UITests : IDisposable
    {
        private readonly IWebDriver _driver;

        public UITests()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless=new");
            _driver = new ChromeDriver(options);
        }

        [Fact]
        public void HomePage_LoadsCorrectly()
        {
            _driver.Navigate().GoToUrl("http://localhost:9000");
            Assert.Contains("PlexGifMaker", _driver.Title);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _driver.Quit();
            _driver.Dispose();
        }
    }
}