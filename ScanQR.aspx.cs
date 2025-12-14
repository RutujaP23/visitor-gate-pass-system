using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;

namespace gatepass
{
    public partial class ScanQR : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack && Request.QueryString["code"] != null)
            {
                string qrCodeData = Request.QueryString["code"];

                // Prevent duplicate fast scan within 5 seconds
                if (Session["LastQR"] != null && Session["LastQRTime"] != null)
                {
                    string lastQR = Session["LastQR"].ToString();
                    DateTime lastTime = (DateTime)Session["LastQRTime"];

                    if (lastQR == qrCodeData && (DateTime.Now - lastTime).TotalSeconds < 5)
                    {
                        Response.Write("⚠ This QR was just processed. Please wait a few seconds.");
                        return;
                    }
                }

                Session["LastQR"] = qrCodeData;
                Session["LastQRTime"] = DateTime.Now;

                MarkEntryExitTime(qrCodeData);
            }
        }

        private void MarkEntryExitTime(string qrCodeData)
        {
            int visitorId = ParseVisitorID(qrCodeData);
            if (visitorId <= 0)
            {
                Response.Write("Invalid QR code!");
                return;
            }

            string connStr = ConfigurationManager.ConnectionStrings["CartDB"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                string query = @"SELECT EntryTime, ExitTime, Status, Email_Id, GuestName, 
                                 CompanyName, DateOfVisit, TimeOfVisit, Address, HostName, 
                                 Department, ReceptionContact, MobileNumber 
                                 FROM VisitorRequests WHERE Id = @VisitorID";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@VisitorID", visitorId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            Response.Write("Visitor not found!");
                            return;
                        }

                        DateTime? entryTime = reader["EntryTime"] != DBNull.Value ? (DateTime?)reader["EntryTime"] : null;
                        DateTime? exitTime = reader["ExitTime"] != DBNull.Value ? (DateTime?)reader["ExitTime"] : null;
                        string status = reader["Status"]?.ToString() ?? "";
                        string visitorEmail = reader["Email_Id"].ToString();
                        string guestName = reader["GuestName"].ToString();
                        string companyName = reader["CompanyName"].ToString();
                        string locationName = reader["Address"].ToString();
                        string date = reader["DateOfVisit"].ToString();
                        string time = reader["TimeOfVisit"].ToString();
                        string hostName = reader["HostName"].ToString();
                        string department = reader["Department"].ToString();
                        string reception = reader["ReceptionContact"].ToString();
                        string mobileNumber = reader["MobileNumber"] != DBNull.Value ? reader["MobileNumber"].ToString() : "";
                        reader.Close();

                        // --------------------------
                        // ENTRY LOGIC
                        // --------------------------
                        if (status == "Approved" || string.IsNullOrEmpty(status))
                        {
                            string updateEntry = "UPDATE VisitorRequests SET EntryTime = @EntryTime, Status = 'Entered' WHERE Id = @VisitorID";
                            using (SqlCommand cmdEntry = new SqlCommand(updateEntry, conn))
                            {
                                cmdEntry.Parameters.AddWithValue("@EntryTime", DateTime.Now);
                                cmdEntry.Parameters.AddWithValue("@VisitorID", visitorId);
                                cmdEntry.ExecuteNonQuery();
                            }

                            // EMAIL
                            if (!string.IsNullOrEmpty(visitorEmail))
                            {
                                SendCheckInEmail(visitorEmail, guestName, companyName, hostName, department, locationName,
                                    reception, DateTime.Now.ToString("dd-MM-yyyy"), DateTime.Now.ToString("hh:mm tt"), visitorId.ToString());
                            }

                            // WHATSAPP MESSAGE
                            if (!string.IsNullOrEmpty(mobileNumber))
                            {
                                string wMsg = $"Visitor Check-In Confirmed\nHi {guestName},\nYou have checked in at Zanvar.\n" +
                                              $"Date: {DateTime.Now:dd-MM-yyyy}\nTime: {DateTime.Now:hh:mm tt}\nHost: {hostName}\n" +
                                              $"Department: {department}\nLocation: {locationName}\nVisit ID: {visitorId}\n" +
                                              $"Reception: {reception}";

                                SendWhatsApp(mobileNumber, wMsg);
                            }

                            Response.Write("Entry marked successfully! Email + WhatsApp sent.");
                        }
                        // --------------------------
                        // EXIT LOGIC
                        // --------------------------
                        else if (status == "Entered" && exitTime == null)
                        {
                            string updateExit = "UPDATE VisitorRequests SET ExitTime = @ExitTime, Status = 'Completed' WHERE Id = @VisitorID";
                            using (SqlCommand cmdExit = new SqlCommand(updateExit, conn))
                            {
                                cmdExit.Parameters.AddWithValue("@ExitTime", DateTime.Now);
                                cmdExit.Parameters.AddWithValue("@VisitorID", visitorId);
                                cmdExit.ExecuteNonQuery();
                            }

                            // EMAIL
                            if (!string.IsNullOrEmpty(visitorEmail))
                            {
                                SendExitConfirmationEmail(visitorEmail, guestName, companyName,
                                    DateTime.Now.ToString("dd-MM-yyyy"),
                                    DateTime.Now.ToString("hh:mm tt"),
                                    locationName, visitorId.ToString());
                            }

                            // WHATSAPP
                            if (!string.IsNullOrEmpty(mobileNumber))
                            {
                                string wMsg = $"Exit Confirmation\nHi {guestName},\nYour visit to {companyName} is completed.\n" +
                                              $"Date: {DateTime.Now:dd-MM-yyyy}\nTime: {DateTime.Now:hh:mm tt}\nLocation: {locationName}\nVisit ID: {visitorId}";

                                SendWhatsApp(mobileNumber, wMsg);
                            }

                            Response.Write("Exit marked successfully! Email + WhatsApp sent.");
                        }
                        else
                        {
                            Response.Write("Visitor already exited!");
                        }
                    }
                }
            }
        }

        // ------------------------------
        // WHATSAPP SENDER
        // ------------------------------
        private string SendWhatsApp(string mobileNumber, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(mobileNumber))
                    return "Invalid Number";

                string digits = new string(mobileNumber.Where(char.IsDigit).ToArray());
                if (!digits.StartsWith("91")) digits = "91" + digits;

                string encodedMsg = Uri.EscapeDataString(message);

                string apiUrl =
                    $"http://backup.smsinsta.com/api/send-text?number={digits}&msg={encodedMsg}&apikey=382f94ceaef2287120ef1cf4d17969612b71ac246dfde2f3ec4831b946576bbd&instance=6910889F88782";

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(apiUrl);
                req.Method = "GET";

                using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(res.GetResponseStream()))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        // ------------------------------
        // EMAIL FUNCTIONS
        // ------------------------------
        private void SendCheckInEmail(string toEmail, string guestName, string companyName,
            string hostName, string department, string locationName, string reception,
            string date, string time, string visitId)
        {
            try
            {
                string fromEmail = ConfigurationManager.AppSettings["EmailUser"];
                string appPassword = ConfigurationManager.AppSettings["EmailPassword"];

                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(fromEmail, "Gatepass System");
                mail.To.Add(toEmail);
                mail.Subject = "Visitor Entry / Gate Check-in Confirmed";
                mail.IsBodyHtml = true;

                mail.Body = $@"
                <html>
                <body style='font-family: Arial;'>
                    <h4>Visitor Entry / Gate Check-in Confirmed</h4>
                    <p>Hi <strong>{guestName}</strong>,</p>
                    <p>You have successfully checked in at <strong>Zanvar</strong> on {date} at {time}.</p>
                    <p><strong>Host:</strong> {hostName}<br>
                    <strong>Destination:</strong> {department}<br>
                    <strong>Location:</strong> {locationName}</p>
                    <p>Your host has been informed. Contact Reception: <strong>{reception}</strong></p>
                    <hr>
                    <p><strong>Visit ID:</strong> {visitId}</p>
                </body>
                </html>";

                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.EnableSsl = true;
                smtp.Credentials = new NetworkCredential(fromEmail, appPassword);
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                Response.Write("Email error: " + ex.Message);
            }
        }

        private void SendExitConfirmationEmail(string toEmail, string guestName, string companyName,
            string date, string time, string locationName, string visitId)
        {
            try
            {
                string fromEmail = ConfigurationManager.AppSettings["EmailUser"];
                string appPassword = ConfigurationManager.AppSettings["EmailPassword"];

                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(fromEmail, "Gatepass System");
                mail.To.Add(toEmail);
                mail.Subject = "Visitor Pass Closed / Exit Confirmed";
                mail.IsBodyHtml = true;

                mail.Body = $@"
                <html>
                <body style='font-family: Arial;'>
                    <h4>Visitor Exit Confirmed</h4>
                    <p>Hi <strong>{guestName}</strong>,</p>
                    <p>Your visit to <strong>{companyName}</strong> has been completed.</p>
                    <p><strong>Date:</strong> {date}<br>
                    <strong>Exit Time:</strong> {time}<br>
                    <strong>Location:</strong> {locationName}<br>
                    <strong>Visit ID:</strong> {visitId}</p>
                </body>
                </html>";

                SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587);
                smtp.EnableSsl = true;
                smtp.Credentials = new NetworkCredential(fromEmail, appPassword);
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                Response.Write("Email error: " + ex.Message);
            }
        }

        // ------------------------------
        // QR PARSER
        // ------------------------------
        private int ParseVisitorID(string qrCodeData)
        {
            try
            {
                foreach (string part in qrCodeData.Split(';'))
                {
                    if (part.StartsWith("VisitorRequestID:", StringComparison.OrdinalIgnoreCase))
                    {
                        return int.Parse(part.Split(':')[1]);
                    }
                }
            }
            catch { }

            return -1;
        }
    }
}
