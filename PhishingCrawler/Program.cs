using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using OpenQA.Selenium.DevTools;
using System;
using System.Data;
using System.IO;
using System.Net.Http.Headers;
using System.Security.AccessControl;
using Tokens.Extensions;

namespace PhishingCrawler
{

    class Program
    {

        public static string OUTPUT_DIR = Directory.CreateDirectory("output").FullName;
        public const bool DEBUG = true;
        public static HashSet<string> DEBUG_DOMAINS = new();

        private static HashSet<string> GetDomains(string path)
        {
            HashSet<string> allDomains = new HashSet<string>();
            using (TextFieldParser csvReader = new TextFieldParser(path))
            {
                csvReader.SetDelimiters(new string[] { "," });
                csvReader.HasFieldsEnclosedInQuotes = true;
                csvReader.ReadFields(); // First line is header

                while (!csvReader.EndOfData)
                {
                    string[] fieldData = csvReader.ReadFields() ?? new List<string>().ToArray();
                    if (fieldData.Length >= 3 && !fieldData[1].IsNullOrWhiteSpace() && !fieldData[2].IsNullOrWhiteSpace())
                    {
                        if (DEBUG && !DEBUG_DOMAINS.Contains(fieldData[1]))
                        {
                            continue;
                        }
                        List<string> relatedDomains = JsonConvert.DeserializeObject<List<string>>(fieldData[2]);
                        if(relatedDomains != null) relatedDomains.ForEach(x => allDomains.Add(x));
                    }
                }
            }
            return allDomains;
        }

        public static void ProcessDomains(object? domains)
        {
            List<string>? urls = domains as List<string>;
            if (urls == null) return;

            foreach (string url in urls)
            {
                Domain d = new Domain(url);
            }
        }

        static void Main(string[] args)
        {
            var path = $"{OUTPUT_DIR}/output.ndjson";
            FileStream filestream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            var streamwriter = new StreamWriter(filestream);
            streamwriter.AutoFlush = true;
            Console.SetOut(streamwriter);

            //DEBUG_DOMAINS.Add("amazon");
            //DEBUG_DOMAINS.Add("ebay");
            DEBUG_DOMAINS.Add("facebook");
            //DEBUG_DOMAINS.Add("google");
            DEBUG_DOMAINS.Add("twitter");
            DEBUG_DOMAINS.Add("bestbuy");
            DEBUG_DOMAINS.Add("paypal");

            string inputPath = (args.Length > 0) ? args[0] : @"E:\Thesis\thesis-repo-master\input.csv";
            List<string> urls = GetDomains(inputPath).ToList<string>();

            int numThreads = (args.Length > 2) ? int.Parse(args[2]) : 8;
            List<Thread> threads = new List<Thread>();

            BrowserManager bm = new BrowserManager(numThreads);
                       
            int domainsPerThread = (int) Math.Ceiling((urls.Count - 1)/ (decimal)numThreads);

            for (int i=0; i < numThreads; i++)
            {
                threads.Add(new Thread(ProcessDomains));
            }         

            for (int i=0; i<numThreads; i++)
            {
                if (i == numThreads - 1)
                {
                    threads[i].Start(urls.GetRange(i * domainsPerThread, urls.Count - i*domainsPerThread));
                }
                else
                {
                    threads[i].Start(urls.GetRange(i * domainsPerThread, domainsPerThread));
                }
            }
        }
    }
}

