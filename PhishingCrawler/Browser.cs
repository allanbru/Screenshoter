using OpenQA.Selenium.Chrome;
using OpenQA.Selenium;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using System.Runtime.ExceptionServices;
using OpenQA.Selenium.Support.Extensions;

namespace PhishingCrawler
{
    public class Browser
    {

        public static string OUTPUT_DIR = Program.OUTPUT_DIR;
        public static int TIMEOUT_MS = 15000;
        private ChromeDriver? browser;
        private Mutex _mutex = new();

        private int debugPort = 9000;

        public Browser(int debugPort) 
        {
            this.debugPort = debugPort;
            browser = StartBrowser();
        }

        private ChromeDriver StartBrowser()
        {
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArguments("--headless");
            chromeOptions.AddArguments("--no-sandbox");
            chromeOptions.AddArguments("--disable-setuid-sandbox");
            chromeOptions.AddArguments("--remote-debugging-port=" + debugPort.ToString());
            chromeOptions.AddArguments("--disable-dev-shm-using");
            chromeOptions.AddArguments("--disable-extensions");
            chromeOptions.AddArguments("--disable-gpu");
            chromeOptions.AddArguments("--disable-infobars");
            chromeOptions.AddArguments("--log-level=3");
            chromeOptions.AddArguments("--disable-3d-apis");
            chromeOptions.AddArguments("--silent");
            var svc = ChromeDriverService.CreateDefaultService();
            svc.SuppressInitialDiagnosticInformation = true;
            svc.LogPath = OUTPUT_DIR + "/chrome.log";
            return new ChromeDriver(svc, chromeOptions);
        }

        public Image? TakeScreenshot(string domain)
        {
            Image? i = null;
            
            _mutex.WaitOne();

            try
            {
                browser.Navigate().GoToUrl("http://" + domain); //url is a string variable
                var path = $"{OUTPUT_DIR}/{DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")} - {domain}.jpeg";
                browser.GetScreenshot().SaveAsFile(path);
                //i = GetEntireScreenshot();
                //i.Save(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to screenshot {domain}: {ex.Message}");
            }

            if (browser != null)
            {
                browser.Navigate().GoToUrl("about:blank");
            }

            _mutex.ReleaseMutex();

            BrowserManager.CheckQueue();

            return i;
        }

        public Image GetEntireScreenshot()
        {
            // Get the total size of the page
            var totalWidth = (int)(long)((IJavaScriptExecutor)browser).ExecuteScript("return document.body.offsetWidth"); //documentElement.scrollWidth");
            var totalHeight = (int)(long)((IJavaScriptExecutor)browser).ExecuteScript("return  document.body.parentNode.scrollHeight");
            // Get the size of the viewport
            var viewportWidth = (int)(long)((IJavaScriptExecutor)browser).ExecuteScript("return document.body.clientWidth"); //documentElement.scrollWidth");
            var viewportHeight = (int)(long)((IJavaScriptExecutor)browser).ExecuteScript("return window.innerHeight"); //documentElement.scrollWidth");

            // We only care about taking multiple images together if it doesn't already fit
            if (totalWidth <= viewportWidth && totalHeight <= viewportHeight)
            {
                var screenshot = browser.GetScreenshot();
                return ScreenshotToImage(screenshot);
            }
            // Split the screen in multiple Rectangles
            var rectangles = new List<Rectangle>();
            // Loop until the totalHeight is reached
            for (var y = 0; y < totalHeight; y += viewportHeight)
            {
                var newHeight = viewportHeight;
                // Fix if the height of the element is too big
                if (y + viewportHeight > totalHeight)
                {
                    newHeight = totalHeight - y;
                }
                // Loop until the totalWidth is reached
                for (var x = 0; x < totalWidth; x += viewportWidth)
                {
                    var newWidth = viewportWidth;
                    // Fix if the Width of the Element is too big
                    if (x + viewportWidth > totalWidth)
                    {
                        newWidth = totalWidth - x;
                    }
                    // Create and add the Rectangle
                    var currRect = new Rectangle(x, y, newWidth, newHeight);
                    rectangles.Add(currRect);
                }
            }
            // Build the Image
            var stitchedImage = new Image<Rgba32>(totalWidth, totalHeight);
            // Get all Screenshots and stitch them together
            var previous = Rectangle.Empty;
            foreach (var rectangle in rectangles)
            {
                // Calculate the scrolling (if needed)
                if (previous != Rectangle.Empty)
                {
                    var xDiff = rectangle.Right - previous.Right;
                    var yDiff = rectangle.Bottom - previous.Bottom;
                    // Scroll
                    ((IJavaScriptExecutor)browser).ExecuteScript(String.Format("window.scrollBy({0}, {1})", xDiff, yDiff));
                }
                // Take Screenshot
                var screenshot = browser.GetScreenshot();
                // Build an Image out of the Screenshot
                var screenshotImage = ScreenshotToImage(screenshot);
                // Calculate the source Rectangle
                var sourceRectangle = new Rectangle(viewportWidth - rectangle.Width, viewportHeight - rectangle.Height, rectangle.Width, rectangle.Height);
                // Copy the Image
                stitchedImage.Mutate(o => o
                       .DrawImage(stitchedImage, new Point(0,0), 1f) // draw the first one top left
                       .DrawImage(screenshotImage, new Point(rectangle.X, rectangle.Y), 1f) // draw the second next to it
                   );
                // Set the Previous Rectangle
                previous = rectangle;
            }
            return stitchedImage;
        }

        private static Image ScreenshotToImage(Screenshot screenshot)
        {
            Image screenshotImage;
            using (var memStream = new MemoryStream(screenshot.AsByteArray))
            {
                screenshotImage = Image.Load(memStream);
            }
            return screenshotImage;
        }

    }
}
