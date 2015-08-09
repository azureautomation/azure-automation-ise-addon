<%@ Page Language="C#" AutoEventWireup="true" CodeFile="FeedbackForm.aspx.cs" Inherits="Feedback_FeedbackForm" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
    <div>
    
    </div>
        <asp:Label ID="CommentLabel" runat="server" Text="Please add feedback below."></asp:Label>
        <p>
            <asp:TextBox ID="CommentTextBox" runat="server" Height="200px" TextMode="MultiLine" Width="100%"></asp:TextBox>
        </p>
        <p>
            <asp:Label ID="EmailLabel" runat="server" Text="Email (Optional, but lets us find more details)"></asp:Label>
        </p>
        <p>
            <asp:TextBox ID="EmailTextBox" runat="server" Width="100%">youremail@yourserver.com</asp:TextBox>
        </p>
        <p>
        <asp:Button ID="FeedbackSubmitButton" runat="server" OnClick="FeedbackSubmitButton_Click" Text="Submit" Height="25px" />
        </p>
    </form>
 </body>
</html>
