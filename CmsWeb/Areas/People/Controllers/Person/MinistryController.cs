using System.Web.Mvc;
using CmsData;
using CmsWeb.Areas.People.Models;
using UtilityExtensions;

namespace CmsWeb.Areas.People.Controllers
{
    public partial class PersonController
    {
        [HttpPost, Route("ContactsMade/{id}/{page?}/{size?}/{sort?}/{dir?}")]
        public ActionResult ContactsMade(int id, int? page, int? size, string sort, string dir)
        {
            var m = new ContactsMadeModel(id);
            m.Pager.Set("/Person2/ContactsMade/" + id, page, size, sort, dir);
            return View("Ministry/Contacts", m);
        }
        [HttpPost]
        public ActionResult AddContactMade(int id)
        {
            var p = DbUtil.Db.LoadPersonById(id);
            DbUtil.LogActivity("Adding contact from: {0}".Fmt(p.Name));
            var c = new Contact
            {
                CreatedDate = Util.Now,
                CreatedBy = Util.UserId1,
                ContactDate = Util.Now.Date,
            };

            DbUtil.Db.Contacts.InsertOnSubmit(c);
            DbUtil.Db.SubmitChanges();

            var cp = new Contactor
            {
                PeopleId = p.PeopleId,
                ContactId = c.ContactId
            };

            DbUtil.Db.Contactors.InsertOnSubmit(cp);
            DbUtil.Db.SubmitChanges();

            TempData["ContactEdit"] = true;
            return Content("/Contact2/" + c.ContactId);
        }
        [HttpPost, Route("ContactsReceived/{id}/{page?}/{size?}/{sort?}/{dir?}")]
        public ActionResult ContactsReceived(int id, int? page, int? size, string sort, string dir)
        {
            var m = new ContactsReceivedModel(id);
            m.Pager.Set("/Person2/ContactsReceived/" + id, page, size, sort, dir);
            return View("Ministry/Contacts", m);
        }
        [HttpPost]
        public ActionResult AddContactReceived(int id)
        {
            var p = DbUtil.Db.LoadPersonById(id);
            DbUtil.LogActivity("Adding contact to: {0}".Fmt(p.Name));
            var c = new Contact
            {
                CreatedDate = Util.Now,
                CreatedBy = Util.UserId1,
                ContactDate = Util.Now.Date,
            };

            DbUtil.Db.Contacts.InsertOnSubmit(c);
            DbUtil.Db.SubmitChanges();

            c.contactees.Add(new Contactee { PeopleId = p.PeopleId });
            c.contactsMakers.Add(new Contactor { PeopleId = Util.UserPeopleId.Value });
            DbUtil.Db.SubmitChanges();

            TempData["ContactEdit"] = true;
            return Content("/Contact2/{0}".Fmt(c.ContactId));
        }
        [HttpPost, Route("TasksAbout/{id}/{page?}/{size?}/{sort?}/{dir?}")]
        public ActionResult TasksAbout(int id, int? page, int? size, string sort, string dir)
        {
            var m = new TasksAboutModel(id);
            m.Pager.Set("/Person2/TasksAbout/" + id, page, size, sort, dir);
            return View("Ministry/Tasks", m);
        }
        [HttpPost]
        public ActionResult AddTaskAbout(int id)
        {
            var p = DbUtil.Db.LoadPersonById(id);
            if (p == null || !Util.UserPeopleId.HasValue)
                return Content("no id");
            var t = p.AddTaskAbout(DbUtil.Db, Util.UserPeopleId.Value, "Please Contact");
            DbUtil.Db.SubmitChanges();
            return Content("/Task/List/{0}".Fmt(t.Id));
        }
        [HttpPost, Route("TasksAssigned/{id}/{page?}/{size?}/{sort?}/{dir?}")]
        public ActionResult TasksAssigned(int id, int? page, int? size, string sort, string dir)
        {
            var m = new TasksAssignedModel(id);
            m.Pager.Set("/Person2/TasksAssigned/" + id, page, size, sort, dir);
            return View("Ministry/Tasks", m);
        }
        [HttpPost]
        public ActionResult VolunteerApprovals(int id)
        {
            var m = new Main.Models.Other.VolunteerModel(id);
            return View("Ministry/Volunteer", m);
        }
    }
}
