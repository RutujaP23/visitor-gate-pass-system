<%@ Page Title="Inside Visitors" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="InsideVisitors.aspx.cs" Inherits="gatepass.InsideVisitors" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Visitors Currently Inside Premises</h2>

    <asp:GridView ID="gvInsideVisitors" runat="server" 
        AutoGenerateColumns="False" 
        CssClass="table"
        DataKeyNames="Id">
        <Columns>

            <asp:TemplateField HeaderText="Sr. No.">
                <ItemTemplate>
                    <%# Container.DataItemIndex + 1 %>
                </ItemTemplate>
            </asp:TemplateField>

            <asp:BoundField DataField="GuestName" HeaderText="Guest Name" />
            <asp:BoundField DataField="CompanyName" HeaderText="Company" />
            <asp:BoundField DataField="HostName" HeaderText="Host" />
            <asp:BoundField DataField="VisitPurpose" HeaderText="Purpose" />
            <asp:BoundField DataField="DateOfVisit" HeaderText="Visit Date" DataFormatString="{0:dd/MM/yyyy}" />
            <asp:BoundField DataField="EntryTime" HeaderText="Entry Time" DataFormatString="{0:dd/MM/yyyy hh:mm tt}" />
            <asp:BoundField DataField="ExitTime" HeaderText="Exit Time" DataFormatString="{0:dd/MM/yyyy hh:mm tt}" />
            <asp:BoundField DataField="Department" HeaderText="Department" />
            <asp:BoundField DataField="ReceptionContact" HeaderText="Reception Contact" />

        </Columns>
    </asp:GridView>

    <style>
        h2 { color: #333; margin-bottom: 15px; }
        .table {
            border-collapse: collapse;
            width: 100%;
            margin-top: 20px;
        }
        .table th, .table td {
            border: 1px solid #ccc;
            padding: 8px;
            text-align: left;
        }
        .table th {
            background-color: #f2f2f2;
        }
    </style>
</asp:Content>
