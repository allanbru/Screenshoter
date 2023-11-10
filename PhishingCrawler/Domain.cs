using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Text;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Numerics;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;
using Newtonsoft.Json;
using OpenQA.Selenium.DevTools.V117.Page;
using Whois;
using Whois.Net;
using JsonConverter = System.Text.Json.Serialization.JsonConverter;

namespace PhishingCrawler
{

    public struct DNSRecords
    {
        public string Domain;
        public List<string> A;
        public List<string> AAAA;
        public List<string> CNAME;
        public List<string> MX;
        public List<string> TXT;
        public List<DnsResourceRecord> OTHER;

        public DNSRecords(string Domain)
        {
            this.Domain = Domain;
            A = new List<string>();
            AAAA = new List<string>();
            CNAME = new List<string>();
            MX = new List<string>();
            TXT = new List<string>();
            OTHER = new List<DnsResourceRecord>();
        }
    }

    public struct Certificate
    {
        public string Issuer;
        public string Subject;
    }

    public class Domain
    {

        internal struct DomainData
        {
            public string url;
            public Certificate? cert;
            public DNSRecords? records;
            public WhoisResponse? response;
            public Image? screenshot;
            
            public DomainData(string url, Certificate? cert, DNSRecords? records, WhoisResponse? response, Image? screenshot)
            {
                this.url = url;
                this.cert = cert;
                this.records = records;
                this.response = response;
                this.screenshot = screenshot;
            }
        
        }

        private DNSResolver dnsResolver = new();

        private Certificate? certificate;
        private DNSRecords records;
        private WhoisResponse? whoisResponse;
        private Image? screenshot;

        private string url;

        public string GetUrl() => url;

        public Domain(string url) {
            this.url = url;
            records = dnsResolver.GetRecords(url);
            //certificate = new Certificate();
            if (records.A.Count > 0 || records.CNAME.Count > 0)
            {
                whoisResponse = WhoisDataFetcher.GetWhoisData(url);
                TakeScreenshot();
            }
            else
            {
                Dump();
            }
        }

        private void TakeScreenshot()
        {
            BrowserManager.AddToQueue(this);
        }

        public void Dump()
        {
            DomainData data = new DomainData(url, certificate ?? new Certificate(), records, whoisResponse, screenshot);
            string dataStr = JsonConvert.SerializeObject(data);
            Console.WriteLine(dataStr);
        }

        public void ScreenshotTaken(Image img)
        {
            screenshot = img;
        }

    }

    public class SSLCatcher
    {
        public static X509Certificate2 GetSSLCertificate(string host)
        {
            using (TcpClient client = new TcpClient())
            {
                client.Connect(host, 443);
                SslStream sslStream = new SslStream(client.GetStream(), false, ValidateServerCertificate, null);
                sslStream.AuthenticateAsClient(host);
                X509Certificate certificate = sslStream.RemoteCertificate;
                return new X509Certificate2(certificate);
            }
        }
        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return (certificate as X509Certificate2).Verify(); // Always accept the certificate (you can implement custom validation logic here)
        }
        
    }

    public class DNSResolver
    {

        public static string[] NS = { "8.8.8.8", "4.4.4.4" };
        private LookupClient lookup = new LookupClient();
        public Collection<NameServer> nameServers = new Collection<NameServer>();

        public DNSResolver()
        {
            nameServers = new Collection<NameServer>();
            IPAddress ip1 = IPAddress.Parse(NS[0]);
            IPAddress ip2 = IPAddress.Parse(NS[1]);

            nameServers.Add(new NameServer(ip1));
            nameServers.Add(new NameServer(ip2));
        }

        public DNSRecords GetRecords(string domain)
        {
            DNSRecords records = new DNSRecords(domain);
            try 
            {
                var result = lookup.QueryServer(nameServers, domain, QueryType.ANY);
                foreach (var line in result.Answers)
                {
                    switch(line.RecordType)
                    {
                        default:
                            records.OTHER.Add(line);
                            break;
                        case ResourceRecordType.A:
                            records.A.Add((line as ARecord).Address.ToString());
                            break;
                        case ResourceRecordType.AAAA:
                            records.AAAA.Add((line as AaaaRecord).Address.ToString());
                            break;
                        case ResourceRecordType.CNAME:
                            records.CNAME.Add((line as CNameRecord).CanonicalName.ToString());
                            break;
                        case ResourceRecordType.TXT:
                            records.TXT.Add((line as TxtRecord).Text.First());
                            break;
                        case ResourceRecordType.MX:
                            records.MX.Add((line as MxRecord).Exchange.ToString());
                            break;
                    }
                }
            }
            catch(Exception)
            {
                // pass
            }
            return records;
        }

        public List<string> GetARecords(string domain)
        {
            try
            {
                var result = lookup.QueryServer(nameServers, domain, QueryType.A);
                var ips = new List<string>();
                foreach (var record in result.Answers.ARecords())
                {
                    ips.Add(record.Address.ToString());
                }
                return ips;
            }
            catch (Exception)
            {
                return new List<string>();
            }

        }

        public List<string> GetCNAMERecords(string domain)
        {
            try
            { 
                var result = lookup.QueryServer(nameServers, domain, QueryType.ANY);
                var aliases = new List<string>();
                foreach (var record in result.Answers.CnameRecords())
                {
                    aliases.Add(record.ToString());
                }
                return aliases;
            }
            catch (Exception)
            {
                return new List<string>();
            }
}

        public List<string> GetMXRecords(string domain)
        {
            try
            {
                var result = lookup.QueryServer(nameServers, domain, QueryType.MX);
                var mxs = new List<string>();
                foreach (var record in result.Answers.MxRecords())
                {
                    mxs.Add(record.ToString());
                }
                return mxs;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }
    }

    public class WhoisDataFetcher
    {
        public static WhoisResponse GetWhoisData(string domain)
        {
            var lookup = new WhoisLookup();
            try
            {
                return lookup.Lookup(domain);
            }
            catch (Exception)
            {
                return new WhoisResponse();
            }
        }
    }

}
