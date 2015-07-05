using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

public partial class Feedback_FeedbackForm : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {

    }

    protected void FeedbackSubmitButton_Click(object sender, EventArgs e)
    {
        var postData = CommentTextBox.Text;
        WebRequest request = WebRequest.Create("https://s1events.azure-automation.net/webhooks?token=hoNRgU%2f9WKCvxjYRNl3dYVMuFaTmJSf3orECpa%2btz%2bY%3d");
        request.Method = "POST";
        byte[] byteArray = Encoding.UTF8.GetBytes(postData);
        request.ContentType = "application/x-www-form-urlencoded";
        request.ContentLength = byteArray.Length;
        Stream dataStream = request.GetRequestStream();
        dataStream.Write(byteArray, 0, byteArray.Length);
        dataStream.Close();

        WebResponse response = request.GetResponse();

        dataStream = response.GetResponseStream();
        StreamReader reader = new StreamReader(dataStream);
        string responseFromServer = reader.ReadToEnd();
        reader.Close();
        dataStream.Close();
        response.Close();

        Response.Redirect("ThanksForm.aspx");
    }

    protected void CommentTextBox_TextChanged(object sender, EventArgs e)
    {

    }
}