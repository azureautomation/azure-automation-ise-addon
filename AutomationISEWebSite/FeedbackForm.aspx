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
            <asp:TextBox ID="CommentTextBox" runat="server" Height="173px" OnTextChanged="CommentTextBox_TextChanged" TextMode="MultiLine" Width="275px"></asp:TextBox>
        </p>
        <asp:Button ID="FeedbackSubmitButton" runat="server" OnClick="FeedbackSubmitButton_Click" Text="Submit" />
    </form>
</body>
</html>
