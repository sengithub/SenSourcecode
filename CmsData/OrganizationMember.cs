﻿/* Author: David Carroll
 * Copyright (c) 2008, 2009 Bellevue Baptist Church 
 * Licensed under the GNU General Public License (GPL v2)
 * you may not use this code except in compliance with the License.
 * You may obtain a copy of the License at http://bvcms.codeplex.com/license 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UtilityExtensions;
using System.Web;
using System.Data.SqlClient;

namespace CmsData
{
    public partial class OrganizationMember
    {
        private const string STR_MeetingsToUpdate = "MeetingsToUpdate";
        public enum MemberTypeCode
        {
            Administrator = 100,
            President = 101,
            Leader = 140,
            AssistantLeader = 142,
            Teacher = 160,
            AssistantTeacher = 161,
            Member = 220,
            InActive = 230,
            VisitingMember = 300,
            Visitor = 310,
            InServiceMember = 500,
            VIP = 700,
            Drop = -1,
        }
        public EnrollmentTransaction Drop(CMSDataContext Db)
        {
            Db.SubmitChanges();
            int ntries = 2;
            while (true)
            {
                try
                {
                    var q = from o in Db.Organizations
                            where o.OrganizationId == OrganizationId
                            let count = Db.Attends.Count(a => a.PeopleId == PeopleId
                                && a.OrganizationId == OrganizationId
                                && (a.MeetingDate < DateTime.Today || a.AttendanceFlag == true))
                            select new { count, Organization.DaysToIgnoreHistory };
                    var i = q.Single();
                    EnrollmentTransaction droptrans = null;
                    if (Util.Now.Subtract(this.EnrollmentDate.Value).TotalDays
                        < (i.DaysToIgnoreHistory ?? 60) 
                        && i.count == 0)
                    {
                        var qe = from et in Db.EnrollmentTransactions
                                 where et.PeopleId == PeopleId
                                    && et.OrganizationId == OrganizationId
                                    && et.EnrollmentDate == this.EnrollmentDate
                                    && et.TransactionTypeId == 1
                                 orderby et.TransactionId
                                 select et.TransactionId;
                        var enrollid = qe.FirstOrDefault();

                        var qt = from et in Db.EnrollmentTransactions
                                 where et.PeopleId == PeopleId && et.OrganizationId == OrganizationId
                                 where et.TransactionId >= enrollid
                                 select et;
                        Db.EnrollmentTransactions.DeleteAllOnSubmit(qt);
                        var qa = from et in Db.Attends
                                 where et.PeopleId == PeopleId && et.OrganizationId == OrganizationId
                                 select et;
                        if (HttpContext.Current != null)
                        {
                            var smids = HttpContext.Current.Items[STR_MeetingsToUpdate] as List<int>;
                            var mids = qa.Select(a => a.MeetingId).ToList();
                            if (smids != null)
                                smids.AddRange(mids);
                            else
                                HttpContext.Current.Items[STR_MeetingsToUpdate] = mids;
                        }
                        Db.Attends.DeleteAllOnSubmit(qa);
                    }
                    else
                    {
                        droptrans = new EnrollmentTransaction
                        {
                            OrganizationId = OrganizationId,
                            PeopleId = PeopleId,
                            MemberTypeId = MemberTypeId,
                            OrganizationName = Organization.OrganizationName,
                            TransactionDate = Util.Now,
                            TransactionTypeId = 5, // drop
                            CreatedBy = Util.UserId1,
                            CreatedDate = Util.Now,
                            Pending = Pending,
                            AttendancePercentage = AttendPct,
                        };
                        Db.EnrollmentTransactions.InsertOnSubmit(droptrans);
                    }
                    Db.OrgMemMemTags.DeleteAllOnSubmit(this.OrgMemMemTags);
                    Db.OrganizationMembers.DeleteOnSubmit(this);
                    return droptrans;
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 1205)
                        if (--ntries > 0)
                        {
                            Db.Dispose();
                            System.Threading.Thread.Sleep(500);
                            continue;
                        }
                    throw;
                }
            }
        }
        public static void UpdateMeetingsToUpdate()
        {
            UpdateMeetingsToUpdate(DbUtil.Db);
        }
        public static void UpdateMeetingsToUpdate(CMSDataContext Db)
        {
            var mids = HttpContext.Current.Items[STR_MeetingsToUpdate] as List<int>;
            if (mids != null)
                foreach (var mid in mids)
                    Db.UpdateMeetingCounters(mid);
        }
        public bool ToggleGroup(CMSDataContext Db, int groupid)
        {
            var group = OrgMemMemTags.SingleOrDefault(g => 
                g.OrgId == OrganizationId && g.PeopleId == PeopleId && g.MemberTagId == groupid);
            if (group == null)
            {
                OrgMemMemTags.Add(new OrgMemMemTag { MemberTagId = groupid });
                return true;
            }
            OrgMemMemTags.Remove(group);
            Db.OrgMemMemTags.DeleteOnSubmit(group);
            return false;
        }
        public void AddToGroup(CMSDataContext Db, string name)
        {
            int? n = null;
            AddToGroup(Db, name, n);
        }
        public void AddToGroup(CMSDataContext Db, string name, int? n)
        {
            if (!name.HasValue())
                return;
            var mt = Db.MemberTags.SingleOrDefault(t => t.Name == name.Trim() && t.OrgId == OrganizationId);
            if (mt == null)
            {
                mt = new MemberTag { Name = name.Trim(), OrgId = OrganizationId };
                Db.MemberTags.InsertOnSubmit(mt);
                Db.SubmitChanges();
            }
            var omt = Db.OrgMemMemTags.SingleOrDefault(t =>
                t.PeopleId == PeopleId
                && t.MemberTagId == mt.Id
                && t.OrgId == OrganizationId);
            if (omt == null)
                mt.OrgMemMemTags.Add(new OrgMemMemTag
                {
                    PeopleId = PeopleId,
                    OrgId = OrganizationId,
                    Number = n
                });
            Db.SubmitChanges();
        }
        public void RemoveFromGroup(CMSDataContext Db, string name)
        {
            var mt = Db.MemberTags.SingleOrDefault(t => t.Name == name && t.OrgId == OrganizationId);
            if (mt == null)
                return;
            var omt = Db.OrgMemMemTags.SingleOrDefault(t => t.PeopleId == PeopleId && t.MemberTagId == mt.Id && t.OrgId == OrganizationId);
            if (omt != null)
            {
                OrgMemMemTags.Remove(omt);
                Db.OrgMemMemTags.DeleteOnSubmit(omt);
                Db.SubmitChanges();
            }
        }
        public void AddToMemberData(string s)
        {
            if (UserData.HasValue())
                UserData += "\n";
            UserData += s;
        }

        public static OrganizationMember InsertOrgMembers
            (int OrganizationId,
            int PeopleId,
            int MemberTypeId,
            DateTime EnrollmentDate,
            DateTime? InactiveDate, bool pending
            )
        {
            return OrganizationMember.InsertOrgMembers(DbUtil.Db, OrganizationId, PeopleId, MemberTypeId, EnrollmentDate, InactiveDate, pending);
        }
        public static OrganizationMember InsertOrgMembers
            (CMSDataContext Db, 
            int OrganizationId,
            int PeopleId,
            int MemberTypeId,
            DateTime EnrollmentDate,
            DateTime? InactiveDate, bool pending
            )
        {
            Db.SubmitChanges();
            int ntries = 2;
            while (true)
            {
                try
                {
                    var m = Db.OrganizationMembers.SingleOrDefault(m2 => m2.PeopleId == PeopleId && m2.OrganizationId == OrganizationId);
                    if (m != null)
                    {
                        m.AddToMemberData("insert: {0}".Fmt(EnrollmentDate.ToString()));
                        return m;
                    }
                    var org = Db.Organizations.SingleOrDefault(oo => oo.OrganizationId == OrganizationId);
                    if (org == null)
                        return null;
                    var om = new OrganizationMember
                    {
                        OrganizationId = OrganizationId,
                        PeopleId = PeopleId,
                        MemberTypeId = MemberTypeId,
                        EnrollmentDate = EnrollmentDate,
                        InactiveDate = InactiveDate,
                        CreatedDate = Util.Now,
                        Pending = pending,
                    };
                    var name = (from o in Db.Organizations
                                where o.OrganizationId == OrganizationId
                                select o.OrganizationName).SingleOrDefault();
                    var et = new EnrollmentTransaction
                    {
                        OrganizationId = om.OrganizationId,
                        PeopleId = om.PeopleId,
                        MemberTypeId = om.MemberTypeId,
                        OrganizationName = name,
                        TransactionDate = Util.Now,
                        EnrollmentDate = om.EnrollmentDate,
                        TransactionTypeId = 1, // join
                        CreatedBy = Util.UserId1,
                        CreatedDate = Util.Now,
                        Pending = pending,
                        AttendancePercentage = om.AttendPct
                    };
                    Db.OrganizationMembers.InsertOnSubmit(om);
                    Db.EnrollmentTransactions.InsertOnSubmit(et);

                    Db.SubmitChanges();
                    return om;
                }
                catch (SqlException ex)
                {
                    if (ex.Number == 1205)
                        if (--ntries > 0)
                        {
                            System.Threading.Thread.Sleep(500);
                            continue;
                        }
                    throw;
                }
            }
        }
    }
}
