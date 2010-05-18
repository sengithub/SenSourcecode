<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/bvorg.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="registerHead" ContentPlaceHolderID="TitleContent" runat="server">
	<title><%=ViewData["orgname"] %> Event Registration</title>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="server">
    <script src="/Content/js/jquery-1.4.2.min.js" type="text/javascript"></script>
    <script src="/Content/js/jquery.idle-timer.js" type="text/javascript"></script>
    <script type="text/javascript">
        $(function() {
            $(document).bind("idle.idleTimer", function() {
                window.location.href = '<%=ViewData["URL"] %>';
            });
            var tmout = parseInt('<%=ViewData["timeout"] %>');

            $(document).bind("keydown", function() {
                $(document).unbind("keydown");
                $.idleTimer(tmout);
            });
            $.idleTimer(tmout);
        });
    </script>

    <h2>Event Registration Received</h2>
    <p>
        Thank you for registering for the <%=ViewData["orgname"] %> event.  
        You should receive a confirmation email at <%=ViewData["email"] %> shortly.
    </p>
    <p><a href='<%=ViewData["URL"] %>'>Start a New Registration</a></p>

</asp:Content>