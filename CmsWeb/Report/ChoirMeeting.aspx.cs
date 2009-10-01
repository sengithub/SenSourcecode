﻿/* Author: David Carroll
 * Copyright (c) 2008, 2009 Bellevue Baptist Church 
 * Licensed under the GNU General Public License (GPL v2)
 * you may not use this code except in compliance with the License.
 * You may obtain a copy of the License at http://bvcms.codeplex.com/license 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using System.Collections;
using CmsData;
using UtilityExtensions;
using CMSPresenter;

namespace CMSWeb.Reports
{
    public partial class ChoirMeeting : System.Web.UI.Page
    {
        protected float H = 1.0f;
        protected float W = 2.625f;
        protected float GAP = .125f;

        protected PdfContentByte dc;
        private Font font = FontFactory.GetFont(FontFactory.HELVETICA, 20);
        private Font smallfont = FontFactory.GetFont(FontFactory.HELVETICA, 8);

        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentType = "application/pdf";
            Response.AddHeader("content-disposition", "filename=foo.pdf");

            var id = this.QueryString<int?>("id");

            var document = new Document(PageSize.LETTER);
            document.SetMargins(36f, 36f, 33f, 36f);
            var w = PdfWriter.GetInstance(document, Response.OutputStream);
            document.Open();
            dc = w.DirectContent;

            var ctl = new RollsheetController();

            var cols = new float[3 * 2 - 1];
            var twid = 0f;
            var t = new PdfPTable(cols.Length);
            for (var i = 0; i < cols.Length; i++)
                if (i % 2 == 1)
                    cols[i] = GAP * 72f;
                else
                    cols[i] = W * 72f;
            foreach (var wid in cols)
                twid += wid;

            t.TotalWidth = twid;
            t.SetWidths(cols);
            t.HorizontalAlignment = Element.ALIGN_CENTER;
            t.LockedWidth = true;
            t.DefaultCell.Border = PdfPCell.NO_BORDER;

            var qB = DbUtil.Db.LoadQueryById(id.Value);
            var q = from p in DbUtil.Db.People.Where(qB.Predicate())
                    orderby p.Name2
                    select new
                    {
                        First = p.NickName ?? p.FirstName,
                        Last = p.LastName,
                        PeopleId = p.PeopleId,
                        Phone = p.CellPhone ?? p.HomePhone
                    };
            foreach (var m in q)
                AddRow(t, m.First, m.Last, m.Phone, m.PeopleId);
            document.Add(t);

            document.Close();
            Response.End();
        }
        public void AddRow(PdfPTable t, string fname, string lname, string phone, int pid)
        {
            var bc = new Barcode39();
            bc.X = 1.2f;
            bc.Font = null;
            bc.Code = pid.ToString();
            var img = bc.CreateImageWithBarcode(dc, null, null);
            var p1 = new Phrase();
            p1.Add(new Chunk(img, 0, 0));
            p1.Add(new Phrase("\n\n" + fname + " " + lname, smallfont));
            var c = new PdfPCell( p1);
            c.HorizontalAlignment = PdfPCell.ALIGN_CENTER;
            c.VerticalAlignment = PdfPCell.ALIGN_MIDDLE;
            c.Border = PdfPCell.NO_BORDER;
            c.FixedHeight = H * 72f;

            var t2 = new PdfPTable(1);
            t2.WidthPercentage = 100f;
            t2.DefaultCell.Border = PdfPCell.NO_BORDER;
            t2.AddCell(new Phrase(fname, font));
            t2.AddCell(new Phrase(lname, font));
            var pcell = new PdfPCell(new Phrase(phone.FmtFone(), smallfont));
            pcell.Border = PdfPCell.NO_BORDER;
            pcell.HorizontalAlignment = Element.ALIGN_RIGHT;
            t2.AddCell(pcell);
            var cell = new PdfPCell(t2);
            cell.VerticalAlignment = PdfPCell.ALIGN_MIDDLE;
            cell.PaddingLeft = 8f;
            cell.PaddingRight = 8f;
            cell.Border = PdfPCell.NO_BORDER;
            cell.FixedHeight = H * 72f;

            t.AddCell(c);
            t.AddCell("");
            t.AddCell(cell);
            t.AddCell("");
            t.AddCell(cell);
        }
    }
}