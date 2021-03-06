using System.Linq;
using System.Web.Mvc;
using UtilityExtensions;
using CmsData;

namespace CmsWeb.Areas.Main.Controllers
{
    [RouteArea("Main", AreaPrefix="UserPref")]
    public class UserPrefController : CmsStaffController
    {
        [Route("Set/{id}/{value}")]
        public ActionResult Set(string id, string value)
        {
            DbUtil.Db.SetUserPreference(id, value);
            return Content("set {0}: {1}".Fmt(id, value));
        }
        [Route("UnSet/{id}/{value}")]
        public ActionResult UnSet(string id)
        {
            var p = DbUtil.Db.CurrentUser.Preferences.SingleOrDefault(up => up.PreferenceX == id);
            DbUtil.Db.Preferences.DeleteOnSubmit(p);
            DbUtil.Db.SubmitChanges();
            return Content("unset " + id);
        }
    }
}
