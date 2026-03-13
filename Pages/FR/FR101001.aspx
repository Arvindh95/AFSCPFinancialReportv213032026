<%@ Page Language="C#" MasterPageFile="~/MasterPages/ListView.master" AutoEventWireup="true" ValidateRequest="false" CodeFile="FR101001.aspx.cs" Inherits="Page_FR101001" Title="Untitled Page" %>
<%@ MasterType VirtualPath="~/MasterPages/ListView.master" %>

<asp:Content ID="cont1" ContentPlaceHolderID="phDS" Runat="Server">
	<px:PXDataSource ID="ds" runat="server" Visible="True" Width="100%"
        TypeName="FinancialReport.FLRTTenantCredentialsMaint"
        PrimaryView="TenantCredentials"
        >
		<CallbackCommands>

		</CallbackCommands>
	</px:PXDataSource>
</asp:Content>
<asp:Content ID="cont2" ContentPlaceHolderID="phL" runat="Server">
	<px:PXGrid AutoGenerateColumns="None" AutoAdjustColumns="True" ID="grid" runat="server" DataSourceID="ds" Width="100%" Height="150px" SkinID="Primary" AllowAutoHide="false">
		<Levels>
			<px:PXGridLevel DataMember="TenantCredentials">
			    <Columns>
				<px:PXGridColumn DataField="TenantName" Width="180" ></px:PXGridColumn>
				<px:PXGridColumn DataField="BaseURL" Width="280" />
				<px:PXGridColumn CommitChanges="True" DataField="CompanyNum" Width="70" ></px:PXGridColumn>
				<px:PXGridColumn CommitChanges="True" DataField="ClientIDNew" Width="150" ></px:PXGridColumn>
				<px:PXGridColumn DataField="ClientSecretNew" Width="150" ></px:PXGridColumn>
				<px:PXGridColumn CommitChanges="True" DataField="UsernameNew" Width="150" ></px:PXGridColumn>
				<px:PXGridColumn DataField="PasswordNew" Width="150" ></px:PXGridColumn>
			<px:PXGridColumn DataField="GammaApiKey"     Width="250" ></px:PXGridColumn></Columns>
			</px:PXGridLevel>
		</Levels>
		<AutoSize Container="Window" Enabled="True" MinHeight="150" ></AutoSize>
		<ActionBar >
		</ActionBar>
	</px:PXGrid>
</asp:Content>
