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
        // Set the ContentType property of the WebRequest.
        request.ContentType = "application/x-www-form-urlencoded";
        // Set the ContentLength property of the WebRequest.
        request.ContentLength = byteArray.Length;
        // Get the request stream.
        Stream dataStream = request.GetRequestStream();
        // Write the data to the request stream.
        dataStream.Write(byteArray, 0, byteArray.Length);
        // Close the Stream object.
        dataStream.Close();
        // Get the response.
        WebResponse response = request.GetResponse();
        // Display the status.
   //     Console.WriteLine(((HttpWebResponse)response).StatusDescription);
        // Get the stream containing content returned by the server.
        dataStream = response.GetResponseStream();
        // Open the stream using a StreamReader for easy access.
        StreamReader reader = new StreamReader(dataStream);
        // Read the content.
        string responseFromServer = reader.ReadToEnd();
        // Display the content.
      //  Console.WriteLine(responseFromServer);
        // Clean up the streams.
        reader.Close();
        dataStream.Close();
        response.Close();

    }

    protected void CommentTextBox_TextChanged(object sender, EventArgs e)
    {

    }
}