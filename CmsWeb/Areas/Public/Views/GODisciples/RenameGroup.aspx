<%@ Page Title="" Language="C#" MasterPageFile="~/Views/Shared/Site2.Master" Inherits="System.Web.Mvc.ViewPage" %>

<asp:Content ID="registerHead" ContentPlaceHolderID="TitleContent" runat="server">
    <title><%=ViewData["header"] %></title>
</asp:Content>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <h2>RenameGroup</h2>
        <% using (Html.BeginForm("RenameGroup", "GODisciples")) { %>
        <div>
            <fieldset>
                <table style="empty-cells:show">
                <col style="width: 13em; text-align:right" />
                <col />
                <col />
                <tr>
                    <td><label for="first">Current Group Name</label></td>
                    <td><%= Html.TextBox("oldname")%></td>
                </tr>
                <tr>
                    <td><label for="last">New Group Name</label></td>
                    <td><%= Html.TextBox("newname")%></td>
                </tr>
                <tr>
                    <td>&nbsp;</td><td><input type="submit" value="Submit" /></td>
                </tr>
                </table>
            </fieldset>
        </div>
    <% } %>


</asp:Content>