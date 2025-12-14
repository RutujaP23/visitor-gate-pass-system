using System;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

namespace gatepass
{
    public partial class InsideVisitors : System.Web.UI.Page
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
                string query = @"
                    SELECT 
                        Id,
                        GuestName,
                        CompanyName,
                        HostName,
                        VisitPurpose,
                        DateOfVisit,
                        EntryTime,
                        ExitTime,
                        Department,
                        ReceptionContact
                    FROM VisitorRequests
                    WHERE Status = 'Entered'
                    ORDER BY EntryTime DESC";

                SqlDataAdapter da = new SqlDataAdapter(query, con);
                DataTable dt = new DataTable();
                da.Fill(dt);

                gvInsideVisitors.DataSource = dt;
                gvInsideVisitors.DataBind();
            }
        }
    }
}
