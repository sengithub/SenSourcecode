﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Linq;
using System.Web;
using System.Web.Mvc;
using CmsData;
using System.Configuration;
using CMSWeb.Models;
using UtilityExtensions;

namespace CMSWeb.Controllers
{
    [HandleError]
    public class VolunteerController : Controller
    {
        public VolunteerController()
        {
            ViewData["header"] = DbUtil.Settings("VolHeader");
            ViewData["logoimg"] = DbUtil.Settings("VolLogo");
        }
        public ActionResult Start(string id)
        {
            var vol = DbUtil.Db.VolOpportunities.SingleOrDefault(v => v.UrlKey == id);
            if (vol == null)
                return View("Unknown");
            return RedirectToAction("Index", new { id = vol.Id });
        }
        public ActionResult Index(int id)
        {
            var m = new Models.VolunteerModel { OpportunityId = id };
            if (Request.HttpMethod.ToUpper() == "GET")
                return View(m);

            UpdateModel(m);
            m.ValidateModel(ModelState);
            if (ModelState.IsValid)
            {
                var count = m.FindMember();
                if (count > 1)
                    ModelState.AddModelError("find", "More than one match, sorry");
                else if (count == 0)
                    ModelState.AddModelError("find", "Cannot find your church record");
                else
                {
                    if (m.person.MemberStatusId != 10)
                        ModelState.AddModelError("find", "You must be a member of the church");
                    else if (m.person.Age < 16)
                        ModelState.AddModelError("find", "You must be a at least 16");
                }
            }
            if (!ModelState.IsValid)
                return View(m);
            var v = DbUtil.Db.VolInterests.SingleOrDefault(vi =>
                vi.PeopleId == m.person.PeopleId && vi.OpportunityCode == m.OpportunityId);
            if (v == null)
            {
                v = new VolInterest
                {
                    Created = DateTime.Now,
                    PeopleId = m.person.PeopleId,
                };
                m.person.EmailAddress = m.email;
                m.Opportunity.VolInterests.Add(v);
                DbUtil.Db.VolInterests.InsertOnSubmit(v);
                DbUtil.Db.SubmitChanges();
            }
            return RedirectToAction("PickList", new { id = v.Id });
        }

        public ActionResult PickList(int id)
        {
            var m = new Models.VolunteerModel { VolInterestId = id };
            if (Request.HttpMethod.ToUpper() == "GET")
                return View(m);

            UpdateModel(m);
            m.ValidateModel2(ModelState);
            if (!ModelState.IsValid)
                return View(m);

            m.VolInterest.Question = m.question;

            var qd = from vi in m.VolInterest.VolInterestInterestCodes
                    join i in m.interests on vi.InterestCodeId equals i.ToInt() into j
                    from i in j.DefaultIfEmpty()
                    where string.IsNullOrEmpty(i)
                    select vi;
            DbUtil.Db.VolInterestInterestCodes.DeleteAllOnSubmit(qd);

            var qa = from i in m.interests
                     join vi in m.VolInterest.VolInterestInterestCodes 
                        on i.ToInt() equals vi.InterestCodeId into j
                     from vi in j.DefaultIfEmpty()
                     where vi == null
                     select i.ToInt();

            foreach (var i in qa)
                m.VolInterest.VolInterestInterestCodes.Add(new VolInterestInterestCode { InterestCodeId = i });

            var cva = m.person.Volunteers.OrderByDescending(vo => vo.ProcessedDate).FirstOrDefault();
            DbUtil.Db.SubmitChanges();
            if (DateTime.Now.Subtract(m.VolInterest.Created.Value).TotalMinutes < 30)
            {
                string body;
                if (cva != null && cva.StatusId == 10)
                    body = m.Opportunity.EmailYesCva;
                else
                    body = m.Opportunity.EmailNoCva;
                Util.Email(m.Opportunity.Email, m.person.Name, m.person.EmailAddress,
                     m.Opportunity.Description, Util.SafeFormat(body));
                return RedirectToAction("Confirm");
            }
            ViewData["saved"] = "Changes Saved";
            return View(m);
        }
        public ActionResult Confirm()
        {
            return View();
        }
    }
}