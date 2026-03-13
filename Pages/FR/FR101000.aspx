<%@ Page Language="C#" MasterPageFile="~/MasterPages/TabView.master" AutoEventWireup="true" ValidateRequest="false" CodeFile="FR101000.aspx.cs" Inherits="Page_FR101000" Title="Financial Report" %>
<%@ MasterType VirtualPath="~/MasterPages/TabView.master" %>

<asp:Content ID="cont1" ContentPlaceHolderID="phDS" runat="Server">
	<px:PXDataSource ID="ds" runat="server" Visible="True" Width="100%"
		TypeName="FinancialReport.FLRTFinancialReportMaint"
		PrimaryView="FinancialReport">
		<CallbackCommands>
			<px:PXDSCallbackCommand Name="generateReport"       CommitChanges="True" />
			<px:PXDSCallbackCommand Name="downloadReport"       CommitChanges="True" />
			<px:PXDSCallbackCommand Name="resetStatus"          CommitChanges="True" />
			<px:PXDSCallbackCommand Name="downloadPresentation" CommitChanges="True" />
			<px:PXDSCallbackCommand Name="previewMarkdown"     CommitChanges="True" />
			<px:PXDSCallbackCommand Name="generateGamma"       CommitChanges="True" />
		</CallbackCommands>
	</px:PXDataSource>
</asp:Content>

<asp:Content ID="cont2" ContentPlaceHolderID="phF" runat="Server">
	<px:PXFormView Width="100%" DataMember="FinancialReport" runat="server" ID="FinancialReportForm" DataSourceID="ds"
		NoteID="Noteid" ActivityIndicator="True">
		<Template>
			<px:PXLayoutRule runat="server" ID="rl_Row1" StartRow="True" />

			<%-- Column 1: Report Info --%>
			<px:PXLayoutRule runat="server" ID="rl_Col1" StartColumn="True" LabelsWidth="S" ControlSize="M" />
			<px:PXTextEdit  runat="server" ID="edReportCD"       DataField="ReportCD" />
			<px:PXTextEdit  runat="server" ID="edDescription"    DataField="Description" />
			<px:PXSelector  runat="server" ID="edCurrYear"       DataField="CurrYear"       CommitChanges="True" />
			<px:PXDropDown  runat="server" ID="edFinancialMonth" DataField="FinancialMonth"  CommitChanges="True" />

			<%-- Column 2: Organisation / Ledger / Status --%>
			<px:PXLayoutRule runat="server" ID="rl_Col2" StartColumn="True" LabelsWidth="S" ControlSize="M" />
			<px:PXSelector  runat="server" ID="edOrganization"   DataField="Organization"   CommitChanges="True" />
			<px:PXSelector  runat="server" ID="edBranch"         DataField="Branch"         CommitChanges="True" />
			<px:PXSelector  runat="server" ID="edLedger"         DataField="Ledger"         CommitChanges="True" />
			<px:PXTextEdit  runat="server" ID="edStatus"         DataField="Status"         Enabled="False" />

			<%-- Column 3: Presentation / Alai --%>
			<px:PXLayoutRule runat="server" ID="rl_Col3" StartColumn="True" LabelsWidth="S" ControlSize="M" />
			<px:PXTextEdit  runat="server" ID="edPresentationTitle"       DataField="PresentationTitle" />
			<px:PXTextEdit  runat="server" ID="edPresentationDescription" DataField="PresentationDescription" TextMode="MultiLine" Height="60px" />
			<px:PXTextEdit  runat="server" ID="edGammaTemplateId"         DataField="GammaTemplateId" />
			<px:PXTextEdit  runat="server" ID="edSlideStatus"             DataField="SlideStatus" Enabled="False" />
		</Template>
	</px:PXFormView>

	<px:PXTab runat="server" ID="tabDetails" DataSourceID="ds" Width="100%">
		<AutoSize Enabled="True" Container="Window" MinHeight="250" />
		<Items>
			<px:PXTabItem Text="REPORT DEFINITIONS">
				<Template>
					<px:PXGrid Width="100%" SkinID="Details" runat="server" ID="gridDefinitionLinks"
						DataSourceID="ds" AutoAdjustColumns="True">
						<Levels>
							<px:PXGridLevel DataMember="DefinitionLinks">
								<Columns>
									<px:PXGridColumn DataField="DefinitionID"     Width="220" CommitChanges="True" />
									<px:PXGridColumn DataField="DefinitionPrefix" Width="80" />
									<px:PXGridColumn DataField="DisplayOrder"     Width="100" CommitChanges="True" />
								</Columns>
							</px:PXGridLevel>
						</Levels>
						<AutoSize Enabled="True" />
					</px:PXGrid>
				</Template>
			</px:PXTabItem>
			<px:PXTabItem Text="PRESENTATION MARKDOWN">
				<Template>
					<px:PXFormView runat="server" ID="fvMarkdown" DataMember="FinancialReport" DataSourceID="ds" Width="100%">
						<Template>
							<px:PXTextEdit runat="server" ID="edPresentationMarkdown" DataField="PresentationMarkdown"
								TextMode="MultiLine" Height="400px" Width="100%" />
						</Template>
					</px:PXFormView>
				</Template>
			</px:PXTabItem>
		</Items>
	</px:PXTab>
</asp:Content>
