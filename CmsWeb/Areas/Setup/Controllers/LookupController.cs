using System.Data.SqlClient;
using System.Web.Mvc;
using CmsData;
using UtilityExtensions;

namespace CmsWeb.Areas.Setup.Controllers
{
    [Authorize(Roles = "Admin,Finance")]
    [RouteArea("Setup", AreaPrefix = "Lookup"), Route("{action}/{id?}")]
    public class LookupController : CmsStaffController
    {
        public class Row
        {
            public int Id { get; set; }
            public string Code { get; set; }
            public string Description { get; set; }
            public bool? Hardwired { get; set; }
        }
        [Route("~/Lookups")]
        [Route("~/Lookup/{id}")]
        public ActionResult Index(string id)
        {
            if (!id.HasValue())
                return View("list");
            if (!User.IsInRole("Admin") && string.Compare(id, "funds", ignoreCase: true) != 0)
                return Content("must be admin");
            ViewData["type"] = id;
            var q = DbUtil.Db.ExecuteQuery<Row>("select * from lookup." + id);
            return View(q);
        }

        [HttpPost]
        public ActionResult Create(int? pk, string type)
        {
            if (!pk.HasValue)
                return Content("need an integer id");
            DbUtil.Db.ExecuteCommand("insert lookup." + type + " (id, code, description) values ({0}, '', '')", pk);
            return RedirectToAction("Index", new { id = type });
        }

        [HttpPost]
        public ContentResult Edit(string id, string value)
        {
            var a = id.SplitStr(".");
            var iid = a[0].Substring(1).ToInt();
            if (id.StartsWith("t"))
                DbUtil.Db.ExecuteCommand(
                    "update lookup." + a[1] + " set Description = {0} where id = {1}", 
                    value, iid);
            else if (id.StartsWith("c"))
                DbUtil.Db.ExecuteCommand(
                    "update lookup." + a[1] + " set Code = {0} where id = {1}",
                    value, iid);
            return Content(value);
        }

        [HttpPost]
        public ActionResult Delete(string id, string type)
        {
            try
            {
                var iid = id.Substring(1).ToInt();
                DbUtil.Db.ExecuteCommand("delete lookup." + type + " where id = {0}", iid);
                return new EmptyResult();
            }
            catch (SqlException)
            {
                return Json(new { error = "Cannot delete {0} because it is in use".Fmt(type)});
            }
        }
    }
}
