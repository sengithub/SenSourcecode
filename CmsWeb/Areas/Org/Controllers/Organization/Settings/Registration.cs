using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CmsData;
using CmsData.Registration;
using CmsWeb.Areas.Org.Models;
using UtilityExtensions;
using CmsData.Codes;

namespace CmsWeb.Areas.Org.Controllers
{
    public partial class OrganizationController
    {
        [HttpPost]
        public ActionResult Registration(int id)
        {
            return PartialView("Settings/Registration", getRegSettings(id));
        }
        [HttpPost]
        [Authorize(Roles = "Edit")]
        public ActionResult RegistrationEdit(int id)
        {
            return PartialView("Settings/RegistrationEdit", getRegSettings(id));
        }
        [HttpPost]
        public ActionResult RegistrationUpdate(int id)
        {
            var m = getRegSettings(id);
            m.AgeGroups.Clear();
            DbUtil.LogActivity("Update Registration {0}".Fmt(m.org.OrganizationName));
            try
            {
                UpdateModel(m);
                if (m.org.OrgPickList.HasValue() && m.org.RegistrationTypeId == RegistrationTypeCode.JoinOrganization)
                    m.org.OrgPickList = null;

                var os = new Settings(m.ToString(), DbUtil.Db, id);
                m.org.RegSetting = os.ToString();
                DbUtil.Db.SubmitChanges();
                if (!m.org.NotifyIds.HasValue())
                    ModelState.AddModelError("Form", needNotify);
                return PartialView("Settings/Registration", m);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Form", ex.Message);
                return PartialView("Settings/RegistrationEdit", m);
            }
        }
        public ActionResult OrgPickList(int id)
        {
            if (Util.SessionTimedOut())
                return Content("<script type='text/javascript'>window.onload = function() { parent.location = '/'; }</script>");
            Response.NoCache();
            DbUtil.Db.CurrentOrg.Id = id;
            var o = DbUtil.Db.LoadOrganizationById(id);
            Session["orgPickList"] = (o.OrgPickList ?? "").Split(',').Select(oo => oo.ToInt()).ToList();
            return Redirect("/SearchOrgs/" + id);
        }
        [HttpPost]
        public ActionResult UpdateOrgIds(int id, string list)
        {
            var o = DbUtil.Db.LoadOrganizationById(id);
            DbUtil.Db.CurrentOrg.Id = id;
            var m = new Settings(o.RegSetting, DbUtil.Db, id);
            m.org = o;
            o.OrgPickList = list;
            DbUtil.Db.SubmitChanges();
            return PartialView("Other/OrgPickList2", m);
        }
        private static Settings getRegSettings(int id)
        {
            var org = DbUtil.Db.LoadOrganizationById(id);
            var m = new Settings(org.RegSetting, DbUtil.Db, id);
            return m;
        }
    }
}