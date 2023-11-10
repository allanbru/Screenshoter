namespace PhishingCrawler
{
    public class BrowserManager
    { 
        static List<Browser> browsers = new();
        static List<Browser> freeBrowsers = new();
        static List<Domain> queue = new();

        private static Mutex _mutex = new();

        public BrowserManager(int numThreads)
        {
            for (int i = 0; i < numThreads; i++)
            {
                browsers.Add(new Browser(9220 + i));
                freeBrowsers.Add(browsers[i]);
            }
        }

        public static void AddToQueue(Domain domain)
        {
            queue.Add(domain);
            CheckQueue();
        }

        public static void CheckQueue()
        {
            if (queue.Count <= 0) return;

            if (freeBrowsers.Count > 0)
            {
                _mutex.WaitOne();
                var domain = queue.First();
                queue.Remove(domain);
                var browser = freeBrowsers.First();
                try
                {
                    Image? img = browser.TakeScreenshot(domain.GetUrl());
                    if (img != null)
                    {
                        domain.ScreenshotTaken(img);
                    }
                }
                catch (Exception)
                {
                    domain.Dump();
                }
                _mutex.ReleaseMutex();
            }
        }
    }

}
