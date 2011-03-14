﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Data.SqlClient;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using UtilityExtensions;
using CmsData;
using System.Threading;
using System.Net.Mail;
using System.Web.Configuration;
using System.Web;
using System.Text.RegularExpressions;
using Amazon.SimpleEmail.Model;
using Amazon.SimpleEmail;
using System.Web.Caching;
using System.IO;
using System.Net.Mime;
using System.Reflection;

namespace MassEmailer
{
    public partial class MassEmailer : ServiceBase
    {

        private bool serviceStarted = false;
        private Thread listener;
        private string ConnStr;

        public MassEmailer()
        {
            InitializeComponent();

            var sSource = "MassEmailer2";
            var sLog = "Application";
            if (!EventLog.SourceExists(sSource))
                EventLog.CreateEventSource(sSource, sLog);
            eventLog1.Source = "MassEmailer2";

            ConnStr = ConfigurationManager.ConnectionStrings["CMSEmailQueue"].ConnectionString;

            string awscreds = Path.Combine(GetApplicationPath(), "awscreds.txt");
            if (File.Exists(awscreds))
            {
                var a = File.ReadAllText(awscreds).Split(',');
                Util.InsertCacheNotRemovable("awscreds", a);
            }
            else if (ConfigurationManager.AppSettings["awscreds"].HasValue())
            {
                var a = ConfigurationManager.AppSettings["awscreds"].Split(',');
                Util.InsertCacheNotRemovable("awscreds", a);
            }
        }
        private static string GetApplicationPath()
        {
            return System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase).Substring(6);
        }
        internal class WorkData
        {
            public DateTime[] dailyruns = new DateTime[] {
                new DateTime(10, 10, 1, 4, 0, 0), // 4:00 AM
                new DateTime(10, 10, 1, 6, 0, 0), // 6:00 AM
                new DateTime(10, 10, 1, 8, 0, 0), // 8:00 AM
                new DateTime(10, 10, 1, 10, 0, 0), // 10:00 AM
                new DateTime(10, 10, 1, 12, 0, 0), // 12:00 AM
                new DateTime(10, 10, 1, 14, 0, 0), // 2:00 PM
                new DateTime(10, 10, 1, 16, 0, 0), // 2:00 PM
                new DateTime(10, 10, 1, 18, 0, 0), // 2:00 PM
                new DateTime(10, 10, 1, 20, 0, 0), // 2:00 PM
                new DateTime(10, 10, 1, 22, 0, 0), // 2:00 PM
            };
            public string connstr { get; set; }
            public DateTime lastrun { get; set; }
            public DateTime lastSES { get; set; }
        }

        protected override void OnStart(string[] args)
        {
            WriteLog("MassEmailer service started");
            StartListening();

        }
        public void StartListening()
        {
            listener = new Thread(Listen);
            serviceStarted = true;
            listener.Start();
        }
        private void Listen()
        {
            string Host = "";
            var data = new WorkData { connstr = ConnStr };
            while (serviceStarted)
            {
                try
                {
                    foreach (var thisrun in data.dailyruns)
                    {
                        if (DateTime.Now > thisrun && data.lastrun.Date < thisrun)
                        {
                            WriteLog("Check for scheduled emails {0}".Fmt(thisrun));
                            var t = DateTime.Now;
                            var cb = new SqlConnectionStringBuilder(data.connstr);
                            cb.InitialCatalog = "BlogData";
                            using (var cn2 = new SqlConnection(cb.ConnectionString))
                            {
                                cn2.Open();
                                using (var cmd = new SqlCommand("SendScheduledEmails", cn2))
                                    cmd.ExecuteNonQuery();
                                var s = DateTime.Now - t;
                                WriteLog("Check Complete for scheduled emails {0:n1} sec".Fmt(s.TotalSeconds));
                                data.lastrun = DateTime.Now;
                            }
                        }
                    }
                    using (var cn = new SqlConnection(data.connstr))
                    {
                        cn.Open();
                        const string sql = @"
WAITFOR(
    RECEIVE TOP(20) CONVERT(VARCHAR(max), message_body) AS message 
    FROM EmailQueue
), TIMEOUT 10000";
                        using (var cmdr = new SqlCommand(sql, cn))
                        {
                            cmdr.CommandTimeout = 0;
                            var reader = cmdr.ExecuteReader();
                            while (reader.Read())
                            {
                                var s = reader.GetString(0);
                                if (!s.HasValue())
                                    continue;
                                var a = s.Split('|');
                                Host = a[1];
                                var CmsHost = a[2];
                                var id = a[3].ToInt();
                                using (var Db = new CMSDataContext(GetConnectionString(Host, data.connstr)))
                                {
                                    Db.Host = Host;
                                    var equeue = Db.EmailQueues.Single(ee => ee.Id == id);
                                    switch (a[0])
                                    {
                                        case "SEND":
                                            SendPersonEmail(Db, data, CmsHost, id, a[4].ToInt());
                                            break;
                                        case "START":
                                            equeue.Started = DateTime.Now;
                                            Db.SubmitChanges();
                                            var nitems = equeue.EmailQueueTos.Count();
                                            if (nitems > 5)
                                                WriteLog("Processing Queue for " + nitems);
                                            break;
                                        case "END":
                                            equeue.Sent = DateTime.Now;
                                            if (equeue.Redacted ?? false)
                                                equeue.Body = "redacted";
                                            else
                                            {
                                                nitems = equeue.EmailQueueTos.Count();
                                                if (nitems > 1)
                                                    NotifySentEmails(Db, data, CmsHost, equeue.FromAddr, equeue.FromName, equeue.Subject, nitems, id);
                                            }
                                            Db.SubmitChanges();
                                            EndQueue(data, new Guid(a[4]));
                                            break;
                                        default:
                                            continue;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    WriteLog("Error sending emails ", EventLogEntryType.Error);
                    var smtp = Util.Smtp();
                    var erroremails = ConfigurationManager.AppSettings["senderrorsto"];
                    var msg = new MailMessage(Util.FirstAddress(erroremails).Address, erroremails, 
                        "Mass Emailer Error " + Host, 
                        Util.SafeFormat(ex.Message + "\n\n" + ex.StackTrace));
                    smtp.Send(msg);
                }
            }
            Thread.CurrentThread.Abort();
        }
        private void SendPersonEmail(CMSDataContext Db, WorkData data, string CmsHost, int id, int pid)
        {
            var useSES = HttpRuntime.Cache["awscreds"] != null;

            var SysFromEmail = Db.Setting("SysFromEmail", ConfigurationManager.AppSettings["sysfromemail"]);
            var emailqueue = Db.EmailQueues.Single(eq => eq.Id == id);
            var emailqueueto = Db.EmailQueueTos.Single(eq => eq.Id == id && eq.PeopleId == pid);
            var From = Util.FirstAddress(emailqueue.FromAddr, emailqueue.FromName);
            var Message = emailqueue.Body;

            var q = from p in Db.People
                    where p.PeopleId == emailqueueto.PeopleId
                    select p;
            var qp = q.Single();
            string text = emailqueue.Body;
            var aa = Db.DoReplacements(ref text, CmsHost, qp, emailqueueto);
            foreach (var ad in aa)
            {
                if (Util.ValidEmail(ad))
                {
                    var qs = "OptOut/UnSubscribe/?enc=" + Util.EncryptForUrl("{0}|{1}".Fmt(emailqueueto.PeopleId, From.Address));
                    var url = Util.URLCombine(CmsHost, qs);
                    var link = @"<a href=""{0}"">Unsubscribe</a>".Fmt(url);
                    text = text.Replace("{unsubscribe}", link);
                    text = text.Replace("{Unsubscribe}", link);
                    text = text.Replace("{toemail}", ad);
                    text = text.Replace("%7Btoemail%7D", ad);
                    text = text.Replace("{fromemail}", From.Address);
                    text = text.Replace("%7Bfromemail%7D", From.Address);

                    EmailRoute(SysFromEmail, data, From, ad, qp.Name, emailqueue.Subject, text, CmsHost, id);
                    emailqueueto.Sent = DateTime.Now;
                    Db.SubmitChanges();
                }
            }
        }
        private Boolean SESCanSend(WorkData data)
        {
            var cansend = false;
            var sendrate = 0D;

            var o = HttpRuntime.Cache["awscreds"];
            if (o != null)
            {
                var resp = (GetSendQuotaResponse)HttpRuntime.Cache["ses_quota"];
                if (resp == null)
                {
                    string[] a = (string[])o;
                    var cfg = new AmazonSimpleEmailServiceConfig();
                    cfg.UseSecureStringForAwsSecretKey = false;
                    var ses = new AmazonSimpleEmailServiceClient(a[0], a[1], cfg);
                    var req = new GetSendQuotaRequest();
                    resp = ses.GetSendQuota(req);
                    HttpRuntime.Cache.Insert("ses_quota", resp, null,
                                DateTime.Now.AddMinutes(15), Cache.NoSlidingExpiration);
                }
                sendrate = resp.GetSendQuotaResult.MaxSendRate.ToInt();
                if (resp.GetSendQuotaResult.SentLast24Hours < (resp.GetSendQuotaResult.Max24HourSend * 90 / 100))
                    cansend = true;
            }
            if (cansend)
            {
                var ms = 1000 / sendrate;
                var elapsed = DateTime.Now.Subtract(data.lastSES).TotalMilliseconds;
                if (elapsed >= ms)
                    return true;
            }
            return false;
        }
        public Boolean SendAmazonSESRawEmail(
            MailAddress from, string to, string nameto, string Subject, string body, string host, int id)
        {
            Util.RecordEmailSent(host, from, Subject, nameto, to, id, true);
            var awsfrom = ConfigurationManager.AppSettings["awsfromemail"];
            var fromname = from.DisplayName;
            if (!fromname.HasValue())
                fromname = from.Address;
            var msg = new MailMessage();
            msg.From = new MailAddress(awsfrom, fromname);
#if DEBUG2
            msg.To.Add(new MailAddress("davcar@pobox.com", nameto));
#else
            var aa = to.SplitStr(",;");
            foreach (var ad in aa)
            {
                if (nameto.HasValue() && nameto.Contains("?"))
                    nameto = null;
                var ma = Util.TryGetMailAddress(ad, nameto);
                if (ma != null)
                    msg.To.Add(ma);
            }
            if (msg.To.Count == 0)
            {
                msg.To.Add(msg.From);
                msg.Subject = "(bad addr:{0}) {1}".Fmt(to, Subject);
            }
#endif
            if (Subject.HasValue())
                msg.Subject = Subject;
            else
                msg.Subject = "no subject";
            msg.ReplyToList.Add(from);
            msg.Headers.Add("X-bvcms-host", host);
            msg.Headers.Add("X-bvcms-mail-id", id.ToString());

            var regex = new Regex("</?([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Multiline);

            var text = regex.Replace(body, string.Empty);

            var bytes1 = Encoding.UTF8.GetBytes(text);
            var htmlStream1 = new MemoryStream(bytes1);
            var htmlView1 = new AlternateView(htmlStream1, MediaTypeNames.Text.Plain);
            var lines = Regex.Split(text, @"\r?\n|\r");
            if (lines.Any(li => li.Length > 990))
                htmlView1.TransferEncoding = TransferEncoding.QuotedPrintable;
            else
                htmlView1.TransferEncoding = TransferEncoding.SevenBit;
            msg.AlternateViews.Add(htmlView1);

            var bytes = Encoding.UTF8.GetBytes(body);
            var htmlStream = new MemoryStream(bytes);
            var htmlView = new AlternateView(htmlStream, MediaTypeNames.Text.Html);
            lines = Regex.Split(body, @"\r?\n|\r");
            if (lines.Any(li => li.Length > 990))
                htmlView.TransferEncoding = TransferEncoding.QuotedPrintable;
            else
                htmlView.TransferEncoding = TransferEncoding.SevenBit;
            msg.AlternateViews.Add(htmlView);

            var rawMessage = new RawMessage();
            using (var memoryStream = ConvertMailMessageToMemoryStream(msg))
                rawMessage.WithData(memoryStream);

            var request = new SendRawEmailRequest();
            request.WithRawMessage(rawMessage);
#if DEBUG2
            var fullto = new MailAddress("davcar@pobox.com", nameto);
            request.WithDestinations(fullto.ToString());
#else
            var toarray = msg.To.Select(tt => tt.ToString()).ToArray();
            request.WithDestinations(toarray);
#endif
            var fullsys = new MailAddress(awsfrom, fromname);
            request.WithSource(fullsys.ToString());

            string[] a = (string[])HttpRuntime.Cache["awscreds"];
            try
            {
                var cfg = new AmazonSimpleEmailServiceConfig();
                cfg.UseSecureStringForAwsSecretKey = false;
                var ses = new AmazonSimpleEmailServiceClient(a[0], a[1], cfg);
                var response = ses.SendRawEmail(request);
                var result = response.SendRawEmailResult;
                return true;
            }
            catch (Exception ex)
            {
                if (!msg.Subject.StartsWith("(sending error)"))
                    return SendAmazonSESRawEmail(from, ConfigurationManager.AppSettings["senderrorsto"], nameto, "(sending error) " + Subject, "<p>(to: {0})</p><pre>{1}</pre>{2}".Fmt(to, ex.Message, body), host, id);
                return false;
            }
        }
        public static MemoryStream ConvertMailMessageToMemoryStream(MailMessage message)
        {
            var assembly = typeof(SmtpClient).Assembly;
            var mailWriterType = assembly.GetType("System.Net.Mail.MailWriter");
            var fileStream = new MemoryStream();
            var mailWriterContructor = mailWriterType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Stream) }, null);
            var mailWriter = mailWriterContructor.Invoke(new object[] { fileStream });
            var sendMethod = typeof(MailMessage).GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic);
            sendMethod.Invoke(message, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { mailWriter, true }, null);
            var closeMethod = mailWriter.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic);
            closeMethod.Invoke(mailWriter, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { }, null);
            return fileStream;
        }
        private void EmailRoute(string SysFrom, WorkData data, MailAddress From, string To, string Name, string subject, string body, string CmsHost, int id)
        {
            var useSES = HttpRuntime.Cache["awscreds"] != null;
            var SendErrorsTo = ConfigurationManager.AppSettings["senderrorsto"];
            if (useSES && SESCanSend(data))
            {
                SendAmazonSESRawEmail(From, To, Name, subject, body, CmsHost, id);
                data.lastSES = DateTime.Now;
            }
            else
                Util.SendMsg(SysFrom, CmsHost, From, subject, body, Name, To, id);
        }
        private void NotifySentEmails(CMSDataContext Db, WorkData data, string CmsHost, string From, string FromName, string subject, int count, int id)
        {
            if (Db.Setting("sendemail", "true") != "false")
            {
                var from = new MailAddress(From, FromName);
                string subj = "sent emails: " + subject;
                string body = @"<a href=""{0}Manage/Emails/Details/{1}"">{2} emails sent</a>".Fmt(CmsHost, id, count);
                var SysFromEmail = Db.Setting("SysFromEmail", ConfigurationManager.AppSettings["sysfromemail"]);
                var SendErrorsTo = ConfigurationManager.AppSettings["senderrorsto"];
                EmailRoute(SysFromEmail, data, from, From, FromName, subj, body, CmsHost, id);
                var uri = new Uri(CmsHost);
                var host = uri.Host;
                EmailRoute(SysFromEmail, data, from, SendErrorsTo, null, host + " " + subj, body, CmsHost, id);
            }
        }
        private static void EndQueue(WorkData data, Guid guid)
        {
            using (var cn = new SqlConnection(data.connstr))
            {
                cn.Open();
                using (var cmd = new SqlCommand("EndConversation", cn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    var conv = new SqlParameter("@conversationID", SqlDbType.UniqueIdentifier);
                    conv.Value = guid;
                    cmd.Parameters.Add(conv);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void WriteLog(string message)
        {
            WriteLog(message, EventLogEntryType.Information);
        }
        private void WriteLog(string message, EventLogEntryType err)
        {
            eventLog1.WriteEntry(message, err);
        }
        protected override void OnStop()
        {
            WriteLog("MassEmailer2 service stopped");
            serviceStarted = false;
            // give it a little time to finish any pending work
            listener.Join(new TimeSpan(0, 0, 20));
        }
        protected string GetConnectionString(string Host, string cs)
        {
            var cb = new SqlConnectionStringBuilder(cs);
            var a = Host.SplitStr(".:");
            cb.InitialCatalog = "CMS_{0}".Fmt(a[0]);
            return cb.ConnectionString;
        }
    }
}