using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;

using Newtonsoft.Json;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;

namespace TransitcardGrabber
{
    class TransitcardGrabber
    {
        // All is very simple
        // Plugin Must return fileName in last line (or in single line)
        // if last line (or single) is empty - file is not exists
        static void Main(string[] args)
        {
            try
            {
                double left = -180.0;
                double right = 180.0;
                double bottom = -90.0;
                double top = 90.0;

                if(!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MAPLEFT")))
                    double.TryParse(System.Environment.GetEnvironmentVariable("MAPLEFT"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out left);
                if (!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MAPRIGHT")))
                    double.TryParse(System.Environment.GetEnvironmentVariable("MAPRIGHT"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out right);
                if (!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MAPBOTTOM")))
                    double.TryParse(System.Environment.GetEnvironmentVariable("MAPBOTTOM"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out bottom);
                if (!String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MAPTOP")))
                    double.TryParse(System.Environment.GetEnvironmentVariable("MAPTOP"), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out top);
    
                Console.OutputEncoding = Encoding.UTF8;                

                Console.WriteLine("*** locator.transitcard.ru grabber ***");
                Console.WriteLine("Getting gas stations from locator.transitcard.ru");
                Console.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "BOX [[{0}:{1}],[{2}:{3}]]", left, bottom, right, top));

                string url = "http://locator.transitcard.ru/web/v1/point/transpose-list?countryCode=ru";
                Console.WriteLine(url);
                Console.WriteLine("Grabbing ...");
                string res = HTTPCall.ViaTCP(url);
                Console.WriteLine(" ... {0} bytes OK", res.Length);

                Console.WriteLine("Parsing ... ");

                Response obj = JsonConvert.DeserializeObject<Response>(res);
                Console.WriteLine(" ... {0} objects OK", obj.size);

                Console.WriteLine("Saving to file ... ");

                res = obj.ToKMLFile();
                Console.WriteLine(" ... OK");
                
                bool SAVE_2_KMZ = true;
                if (SAVE_2_KMZ)
                {
                    string df = res;
                    Console.WriteLine("Preparing KMZ ... ");
                    res = Response.ToKMZFile();
                    File.Delete(df);
                    Console.WriteLine(" ... OK");
                };

                Console.WriteLine("Data saved to file: ");
                Console.WriteLine(res);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: ");
                Console.WriteLine(ex.ToString());
                Console.WriteLine();
            };
        }

        public static class HTTPCall
        {
            public static string ViaNet(string url)
            {
                string regexPattern = @"^(?<proto>[^:/\?#]+)://(?:(?<user>[^@:]*):?(?<pass>[^@]*)@)?(?<host>[^@/\?#:]*)?:?(?<port>\d*)(?<path>[^@\?#]*)(?<ask>\?(?<query>[^#]*))?(?<sharp>#(?<hash>.*))?";
                Match m = (new Regex(regexPattern, RegexOptions.IgnoreCase)).Match(url);

                HttpWebRequest wreq = (HttpWebRequest)WebRequest.Create(url);
                wreq.Referer = "http://" + m.Groups["host"].Value + "/";
                wreq.UserAgent = "Mozilla/5.0 (Windows NT 5.1; rv:52.0) Gecko/20100101 Firefox/52.0";
                HttpWebResponse wres = (HttpWebResponse)wreq.GetResponse();
                System.IO.Stream wr = wres.GetResponseStream();
                StreamReader sr = new StreamReader(wr, Encoding.GetEncoding(wres.ContentEncoding));
                string res = sr.ReadToEnd();
                sr.Close();
                wr.Close();
                wres.Close();
                return res;
            }

            public static string ViaTCP(string url)
            {
                string regexPattern = @"^(?<proto>[^:/\?#]+)://(?:(?<user>[^@:]*):?(?<pass>[^@]*)@)?(?<host>[^@/\?#:]*)?:?(?<port>\d*)(?<path>[^@\?#]*)(?<ask>\?(?<query>[^#]*))?(?<sharp>#(?<hash>.*))?";
                Match m = (new Regex(regexPattern, RegexOptions.IgnoreCase)).Match(url);

                string host = m.Groups["host"].Value;
                int port = 80;
                if (!String.IsNullOrEmpty((m.Groups["port"].Value))) port = int.Parse(m.Groups["port"].Value);
                string path = m.Groups["path"].Value + m.Groups["ask"].Value;

                System.Net.Sockets.TcpClient tcpc = new System.Net.Sockets.TcpClient();
                tcpc.Connect(host, port);
                System.IO.Stream tcps = tcpc.GetStream();

                //int unixTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

                string data = "GET " + path + " HTTP/1.0\r\n";
                data += "Accept: text/javascript, application/javascript, application/ecmascript, application/x-ecmascript, */*; q=0.01\r\n";
                data += "Accept-Language: ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3\r\n";
                data += "Connection: close\r\n";
                data += "Host: " + host + "\r\n";
                data += "Referer: http://" + host + "/\r\n";
                data += "User-Agent: Mozilla/5.0 (Windows NT 5.1; rv:52.0) Gecko/20100101 Firefox/52.0\r\n";
                data += "\r\n";
                byte[] buff = Encoding.ASCII.GetBytes(data);
                tcps.Write(buff, 0, buff.Length);

                List<byte> rcvd = new List<byte>();
                buff = new byte[4];
                tcps.Read(buff, 0, 4);
                rcvd.AddRange(buff);
                while (Encoding.ASCII.GetString(rcvd.ToArray(), rcvd.Count - 4, 4) != "\r\n\r\n")
                    rcvd.Add((byte)tcps.ReadByte());
                Encoding enc = Encoding.ASCII;
                string headers = enc.GetString(rcvd.ToArray());
                Regex rx = new Regex(@"Content-Type:\s{0,1}([^;]+)(;\s{0,1}charset=(.+)){0,}");
                Match mx = rx.Match(headers);
                if ((mx.Success) && (!String.IsNullOrEmpty((mx.Groups[3].Value.Trim('\r')))))
                    enc = Encoding.GetEncoding(mx.Groups[3].Value.Trim('\r'));
                StreamReader sr = new StreamReader(tcps, enc);
                string res = sr.ReadToEnd();
                sr.Close();
                tcps.Close();
                tcpc.Close();
                return res;
            }

        }

        public class Response
        {
            public string[] id;
            public double[] latitude;
            public double[] longitude;
            public string[] brand;
            public int[] brandId;
            public int[] typeNetwork;
            public bool[] brandIconExists;
            public bool[] fuelStation;
            public bool[] gasStation;
            public bool[] washing;
            public bool[] closed;
            public int[] propertiesType;
            public bool[] enabled;
            public int size;

            public AZS[] list
            {
                get
                {
                    AZS[] res = new AZS[this.size];
                    for (int i = 0; i < this.size; i++)
                        res[i] = AZS.FromResponse(this, i);
                    return res;
                }
            }

            public string ToKMLFile()
            {
                if (this.size < 1) return "";
                string fileName = System.AppDomain.CurrentDomain.BaseDirectory + @"\TransitcardGrabber.kml";
                FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                StreamWriter sb = new StreamWriter(fs, Encoding.UTF8);

                sb.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                sb.WriteLine("<kml>");
                sb.WriteLine("<Document>");
                sb.WriteLine("<name>locator.transitcard.ru</name>");
                sb.WriteLine("<createdby>locator.transitcard.ru grabber</createdby>");

                Dictionary<int, Brand> brandlist = new Dictionary<int, Brand>();
                for (int i = 0; i < this.size; i++)
                {
                    int bid = this.brandId[i];
                    if (brandlist.ContainsKey(bid))
                    {
                        Brand b = brandlist[bid];
                        b.count++;
                        brandlist[bid] = b;
                    }
                    else
                        brandlist.Add(bid, new Brand(bid, this.brand[i]));
                };
                foreach (KeyValuePair<int, Brand> b in brandlist)
                {
                    sb.WriteLine(String.Format("<Folder><name><![CDATA[{0} (Count: {1})]]></name>", b.Value.name, b.Value.count));
                    int cnt = 0;
                    Response.AZS[] alist = this.list;
                    foreach (Response.AZS a in alist)
                    {
                        if (a.brandId != b.Value.id) continue;
                        sb.WriteLine("<Placemark>");
                        sb.WriteLine(String.Format("<styleUrl>#brand{0}</styleUrl>", a.brandId));
                        sb.WriteLine(String.Format("<name><![CDATA[{0} - {1}]]></name>", a.brand, ++cnt));
                        sb.WriteLine(String.Format("<description><![CDATA[ID: {0},\r\nClosed: {1},\r\nFuel: {2},\r\nGaz: {3},\r\nWash: {4}\r\nBrand: {5} - {6}]]></description>", a.id, a.closed, a.fuelStation, a.gasStation, a.washing, a.brandId, a.brand));
                        sb.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "<Point><coordinates>{1},{0},0</coordinates></Point>", a.latitude, a.longitude));
                        sb.WriteLine("</Placemark>");
                    };
                    sb.WriteLine("</Folder>");
                };
                foreach (KeyValuePair<int, Brand> b in brandlist)
                    sb.WriteLine(String.Format("<Style id=\"brand{0}\"><IconStyle><Icon><href>images/brand{0}.png</href></Icon></IconStyle></Style>", b.Value.id));
                sb.WriteLine("</Document>");
                sb.WriteLine("</kml>");
                sb.Close();
                return fileName;
            }

            public class AZS
            {
                public string id;
                public double latitude;
                public double longitude;
                public string brand;
                public int brandId;
                public int typeNetwork;
                public bool brandIconExists;
                public bool fuelStation;
                public bool gasStation;
                public bool washing;
                public bool closed;
                public int propertiesType;
                public bool enabled;

                private AZS() { }

                public static AZS FromResponse(Response resp, int num)
                {
                    AZS res = new AZS();
                    res.brand = resp.brand[num];
                    res.brandIconExists = resp.brandIconExists[num];
                    res.brandId = resp.brandId[num];
                    res.closed = resp.closed[num];
                    res.enabled = resp.enabled[num];
                    res.fuelStation = resp.fuelStation[num];
                    res.gasStation = resp.gasStation[num];
                    res.id = resp.id[num];
                    res.latitude = resp.latitude[num];
                    res.longitude = resp.longitude[num];
                    if(resp.propertiesType != null) res.propertiesType = resp.propertiesType[num];
                    res.typeNetwork = resp.typeNetwork[num];
                    res.washing = resp.washing[num];
                    return res;
                }
            }

            public struct Brand
            {
                public int id;
                public string name;
                public int count;

                public Brand(int id, string name) { this.id = id; this.name = name; this.count = 1; }
            }

            public static string ToKMZFile()
            {
                string fileName = System.AppDomain.CurrentDomain.BaseDirectory + @"\TransitcardGrabber.kmz";
                FileStream fsOut = File.Create(fileName);
                ZipOutputStream zipStream = new ZipOutputStream(fsOut);
                zipStream.SetComment("Created by locator.transitcard.ru grabber");
                zipStream.SetLevel(3);
                // doc.kml
                {
                    FileInfo fi = new FileInfo(System.AppDomain.CurrentDomain.BaseDirectory + @"\TransitcardGrabber.kml");
                    ZipEntry newEntry = new ZipEntry("doc.kml");
                    newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity
                    newEntry.Size = fi.Length;
                    zipStream.PutNextEntry(newEntry);

                    byte[] buffer = new byte[4096];
                    using (FileStream streamReader = File.OpenRead(fi.FullName))
                        StreamUtils.Copy(streamReader, zipStream, buffer);
                    zipStream.CloseEntry();
                };
                // images
                {
                    string[] files = Directory.GetFiles(System.AppDomain.CurrentDomain.BaseDirectory + @"\images");
                    foreach (string filename in files)
                    {

                        FileInfo fi = new FileInfo(filename);

                        ZipEntry newEntry = new ZipEntry(@"images\" + fi.Name);
                        newEntry.DateTime = fi.LastWriteTime; // Note the zip format stores 2 second granularity
                        newEntry.Size = fi.Length;
                        zipStream.PutNextEntry(newEntry);

                        byte[] buffer = new byte[4096];
                        using (FileStream streamReader = File.OpenRead(filename))
                            StreamUtils.Copy(streamReader, zipStream, buffer);
                        zipStream.CloseEntry();
                    }
                };
                zipStream.IsStreamOwner = true; // Makes the Close also Close the underlying stream
                zipStream.Close();
                return fileName;
            }            
        }
    }
}
