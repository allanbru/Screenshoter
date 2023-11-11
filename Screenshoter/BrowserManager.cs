using OpenQA.Selenium.DevTools.V117.Animation;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using System.Collections.Concurrent;
using System.Drawing.Imaging;
using System.Runtime.Intrinsics.Arm;

namespace Screenshoter
{

    public static class BrowserManager
    {

        public delegate Task OnAnyQueueChanged();

        static ConcurrentQueue<Browser> freeBrowsers = new();
        static ConcurrentQueue<DomainEnvelope> queue = new();

        public static OnAnyQueueChanged anyQueueChanged = CheckQueue;

        private static bool started = false;

        public static void StartBrowserManager(int numThreads)
        {
            started = true;
            for (int i = 0; i < numThreads; i++)
            {
                Browser b = new(Program.PORT + i + 1);
                freeBrowsers.Enqueue(b);
            }
        }

        public static async Task AddToQueue(DomainEnvelope envelope)
        {
            queue.Enqueue(envelope);
            Console.WriteLine($"Queued {envelope.url}, Queue Size = {queue.Count}");
            anyQueueChanged.Invoke();
        }

        public static void FinishedScreenshot(Browser browser, DomainEnvelope domain, string path)
        {
            freeBrowsers.Enqueue(browser);
            string outMsg = $"Success: {path}";
            byte[] text = System.Text.Encoding.UTF8.GetBytes(outMsg);
            try
            {
                domain.client.GetStream().Write(text);
            }
            catch (Exception)
            {
                return;
            }
        }

        public static void FailedScreenshot(Browser browser, DomainEnvelope domain, string message)
        {
            freeBrowsers.Enqueue(browser);
            Console.WriteLine($"[MANAGER] Browser {browser.GetId()} is now free");
            string outMsg = $"Failed: {message}";
            byte[] text = System.Text.Encoding.UTF8.GetBytes(outMsg);
            try
            {
                domain.client.GetStream().Write(text);
            }
            catch(Exception)
            {
                return;
            }
        }

        public static async Task CheckQueue()
        {
            await Task.Delay(100);
            if (queue.Count == 0)
            {
                if (Program.IsFinished())
                {
                    await Program.finishedExecution.Invoke();
                }
                return;
            }

            if (freeBrowsers.TryDequeue(out var browser))
            {
                bool gotEnvelope = queue.TryDequeue(out var envelope);
                if(gotEnvelope)
                {
                    try
                    {
                        Console.WriteLine($"[MANAGER] Queue size: {queue.Count}");
                        await browser.TakeScreenshot(envelope);
                    }
                    catch (Exception)
                    {
                        // Ignore
                    }
                }
                else 
                {
                    freeBrowsers.Enqueue(browser);
                }
            }
        }

        public static bool HasStarted() => started;
    }

}
