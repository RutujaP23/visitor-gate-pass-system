using QRCoder;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace gatepass
{
    public partial class PendingRequests : System.Web.UI.Page
    {
        string connStr = ConfigurationManager.ConnectionStrings["CartDB"].ConnectionString;


    protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindGrid();
            }
        }

        private void BindGrid()
        {
            using (SqlConnection con = new SqlConnection(connStr))
            {
                SqlDataAdapter da = new SqlDataAdapter("SELECT * FROM VisitorRequests WHERE Status='Pending'", con);
                DataTable dt = new DataTable();
                da.Fill(dt);
                gvPendingRequests.DataSource = dt;
                gvPendingRequests.DataBind();
            }
        }

        protected void btnTest_Click(object sender, EventArgs e)
        {
            try
            {
                string url = "http://backup.smsinsta.com/api/send-text?number=919890786722&msg=HI&apikey=382f94ceaef2287120ef1cf4d17969612b71ac246dfde2f3ec4831b946576bbd&instance=6910889F88782";
                using (WebClient client = new WebClient())
                {
                    string response = client.DownloadString(url);
                    Response.Write("API Response: " + response);
                }
            }
            catch (Exception ex)
            {
                Response.Write("Error: " + ex.Message);
            }
        }

        private string GenerateQrCode(int requestId)
        {
            string qrData = "VisitorRequestID:" + requestId + ";Status:Approved";

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCode qrCode = new QRCode(qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q)))
            using (Bitmap qrCodeImage = qrCode.GetGraphic(20))
            {
                string folderPath = Server.MapPath("~/QRCodes/");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string fileName = "Request_" + requestId + ".png";
                string filePath = Path.Combine(folderPath, fileName);

                if (File.Exists(filePath))
                    File.Delete(filePath);

                qrCodeImage.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                return "~/QRCodes/" + fileName;
            }
        }

        private void SendQrEmail(string toEmail, string qrFilePath, string hostName, string companyName, string timeOfVisit, string endTime, string visitId)
        {
            try
            {
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress("sifaecs1234@gmail.com", "GatePass System");
                mail.To.Add(toEmail);
                mail.Subject = "Visit Scheduled / Pass Generated";

                LinkedResource qrImage = new LinkedResource(qrFilePath, System.Net.Mime.MediaTypeNames.Image.Jpeg);
                qrImage.ContentId = "QrCodeImage";

                string body = $@"


<html>
<body style='font-family: Arial;'>
<h3>Visit Scheduled / Pass Generated</h3>
<p>Your visit to <b>{hostName}</b> at <b>Zanvar Group</b> is confirmed.</p>
<p><b>Time Slot:</b> {timeOfVisit} – {endTime}</p>
<p><b>Visit ID:</b> {visitId}</p>
<p>Show this QR code at gate:</p>
<p><img src='cid:QrCodeImage' width='150' height='150'/></p>
</body>
</html>";


            AlternateView htmlView = AlternateView.CreateAlternateViewFromString(body, null, "text/html");
                htmlView.LinkedResources.Add(qrImage);
                mail.AlternateViews.Add(htmlView);

                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
                string emailPassword = ConfigurationManager.AppSettings["EmailPassword"];
                smtp.Credentials = new NetworkCredential("sifaecs1234@gmail.com", emailPassword);
                smtp.EnableSsl = true;

                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Email Error: " + ex.Message);
            }
        }

        private string SendWhatsAppMessageWithResponse(string mobileNumber, string message)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                if (string.IsNullOrEmpty(mobileNumber))
                    return "Error: empty number";

                // Keep only digits
                string digits = new string(mobileNumber.Where(char.IsDigit).ToArray());

                // Ensure Indian format
                if (!digits.StartsWith("91"))
                    digits = "91" + digits;

                // Encode message
                string encodedMsg = Uri.EscapeDataString(message);

                // Static API Key & Instance ID
                //string apiKey = "382f94ceaef2287120ef1cf4d17969612b71ac246dfde2f3ec4831b946576bbd";
                //string instance = "6910889F88782";

                

                string apiUrl =
                    $"http://backup.smsinsta.com/api/send-text?number={digits}&msg={encodedMsg}&apikey=382f94ceaef2287120ef1cf4d17969612b71ac246dfde2f3ec4831b946576bbd&instance=6910889F88782";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                request.Method = "GET";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }


        protected void gvPendingRequests_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int rowIndex = -1;
            if (!int.TryParse(e.CommandArgument?.ToString(), out rowIndex))
            {
                Response.Write("Invalid CommandArgument: " + e.CommandArgument);
                return;
            }

            if (rowIndex < 0 || rowIndex >= gvPendingRequests.DataKeys.Count)
            {
                Response.Write("Row index out of range: " + rowIndex);
                return;
            }

            int requestId = Convert.ToInt32(gvPendingRequests.DataKeys[rowIndex].Value);
            string newStatus = "";
            if (e.CommandName == "Approve") newStatus = "Approved";
            if (e.CommandName == "Reject") newStatus = "Rejected";

            if (string.IsNullOrEmpty(newStatus)) return;

            using (SqlConnection con = new SqlConnection(connStr))
            {
                SqlCommand cmd = new SqlCommand("UPDATE VisitorRequests SET Status=@S WHERE Id=@I", con);
                cmd.Parameters.AddWithValue("@S", newStatus);
                cmd.Parameters.AddWithValue("@I", requestId);
                con.Open();
                cmd.ExecuteNonQuery();
            }

            if (newStatus == "Approved")
            {
                string qrUrl = GenerateQrCode(requestId);

                using (SqlConnection con = new SqlConnection(connStr))
                {
                    SqlCommand cmd = new SqlCommand("UPDATE VisitorRequests SET QrCodePath=@P WHERE Id=@I", con);
                    cmd.Parameters.AddWithValue("@P", qrUrl);
                    cmd.Parameters.AddWithValue("@I", requestId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }

                string email = "", host = "", comp = "", start = "", end = "", visitId = "", mobile = "", waResponse = "";

                using (SqlConnection con = new SqlConnection(connStr))
                {
                    SqlCommand cmd = new SqlCommand(
                    @"SELECT Email_Id, HostName, CompanyName, TimeOfVisit, EndTime, Id, MobileNumber 
                  FROM VisitorRequests WHERE Id=@I", con);
                    cmd.Parameters.AddWithValue("@I", requestId);
                    con.Open();
                    SqlDataReader dr = cmd.ExecuteReader();
                    if (dr.Read())
                    {
                        email = dr["Email_Id"].ToString();
                        host = dr["HostName"].ToString();
                        comp = dr["CompanyName"].ToString();
                        start = dr["TimeOfVisit"].ToString();
                        end = dr["EndTime"].ToString();
                        visitId = dr["Id"].ToString();
                        mobile = dr["MobileNumber"].ToString();
                    }
                }

                if (!string.IsNullOrEmpty(email))
                {
                    string qrFilePath = Server.MapPath(qrUrl);
                    SendQrEmail(email, qrFilePath, host, comp, start, end, visitId);
                }

                if (!string.IsNullOrEmpty(mobile))
                {
                    string msg = $"Your visit to Zanvar Group with host {host} is approved.\nVisit ID: {visitId}\nTime: {start} - {end}";
                    waResponse = SendWhatsAppMessageWithResponse(mobile, msg);

                  /*  using (SqlConnection con = new SqlConnection(connStr))
                    {
                        SqlCommand cmd = new SqlCommand("UPDATE VisitorRequests SET WhatsAppResponse=@R WHERE Id=@I", con);
                        cmd.Parameters.AddWithValue("@R", waResponse);
                        cmd.Parameters.AddWithValue("@I", requestId);
                        con.Open();
                        cmd.ExecuteNonQuery();
                    }*/
                }
            }

            BindGrid();
        }
    }


}
