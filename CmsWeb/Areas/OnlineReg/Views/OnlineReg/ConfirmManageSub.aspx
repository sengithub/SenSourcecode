<%@ Page Title="" Language="C#" Inherits="System.Web.Mvc.ViewPage" %>
    <p style="color: Black">
    Email sent to <%=Util.ObscureEmail((string)ViewData["email"]) %> (email obscured on purpose)
    </p>
    <% if ((bool?)ViewData["ManagingSubscriptions"] == true)
       { %>
<p>
<strong>One-Time Link</strong><br>
We have sent you a One-Time Link to manage your subscriptions which you should receive shortly.
</p>
    <% } %>
    <% if ((bool?)ViewData["CreatedAccount"] == true)
       { %>
<p>
<strong>Account Created</strong><br />
We have created an account for you on our church database. You should receive your credentials shortly.
</p>
    <% } %>
    <p>
    <%
       if (((string)Session["gobackurl"]).HasValue())
       { %>
    <p style="color: Blue"><a href='<%=Session["gobackurl"] %>'>Go back to your registration</a></p>
    <% } %>
