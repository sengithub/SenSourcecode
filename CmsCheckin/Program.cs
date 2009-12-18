﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace CmsCheckin
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var f = new StartUp();
            var r = f.ShowDialog();
            if (r == DialogResult.Cancel)
                return;
            CampusId = f.CampusId;
            ThisDay = f.DayOfWeek;
            HideCursor = f.HideCursor.Checked;
            TestMode = f.TestMode.Checked;
            LeadTime = int.Parse(f.LeadTime.Text);
            f.Dispose();
            Application.Run(new Form1());
        }
        public static int CampusId { get; set; }
        public static int ThisDay { get; set; }
        public static int LeadTime { get; set; }
        public static bool HideCursor { get; set; }
        public static bool TestMode{ get; set; }
        public static string QueryString
        {
            get
            {
                return string.Format("?campus={0}&thisday={1}", CampusId, ThisDay);
            }
        }
    }
}
