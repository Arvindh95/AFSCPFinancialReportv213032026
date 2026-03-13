<%@ Page Language="C#" MasterPageFile="~/MasterPages/FormDetail.master" AutoEventWireup="true" ValidateRequest="false" CodeFile="FR101002.aspx.cs" Inherits="Page_FR101002" Title="Report Definition" %>
<%@ MasterType VirtualPath="~/MasterPages/FormDetail.master" %>

<asp:Content ID="cont1" ContentPlaceHolderID="phDS" runat="Server">
    <px:PXDataSource ID="ds" runat="server" Visible="True" Width="100%"
        TypeName="FinancialReport.FLRTReportDefinitionMaint"
        PrimaryView="ReportDefinition">
        <CallbackCommands>
            <px:PXDSCallbackCommand Name="copyDefinition" CommitChanges="True" />
            <px:PXDSCallbackCommand Name="detectColumns" CommitChanges="True" />
        </CallbackCommands>
    </px:PXDataSource>
</asp:Content>

<asp:Content ID="cont2" ContentPlaceHolderID="phF" runat="Server">
    <px:PXFormView ID="form" runat="server" DataSourceID="ds" DataMember="ReportDefinition"
        Width="100%" Caption="Report Definition">
        <Template>
            <%-- Row 1: Definition Header --%>
            <px:PXLayoutRule runat="server" StartRow="True" LabelsWidth="SM" ControlSize="M" />
            <px:PXSelector ID="edDefinitionCD" runat="server" DataField="DefinitionCD" />
            <px:PXTextEdit ID="edDefinitionPrefix" runat="server" DataField="DefinitionPrefix" />
            <px:PXDropDown ID="edReportType" runat="server" DataField="ReportType" />
            <px:PXLayoutRule runat="server" StartColumn="True" LabelsWidth="SM" ControlSize="XL" />
            <px:PXTextEdit ID="edDescription" runat="server" DataField="Description" />
            <px:PXCheckBox ID="edIsActive" runat="server" DataField="IsActive" />

            <%-- Row 2: Data Source --%>
            <px:PXLayoutRule runat="server" StartRow="True" StartGroup="True" GroupCaption="Data Source" LabelsWidth="SM" ControlSize="M" />
            <px:PXSelector ID="edGIName" runat="server" DataField="GIName" CommitChanges="True" AutoRefresh="True" />

            <px:PXLayoutRule runat="server" StartColumn="True" LabelsWidth="SM" ControlSize="M" />
            <px:PXSelector ID="edAccountColumn" runat="server" DataField="AccountColumn" AutoRefresh="True" />
            <px:PXSelector ID="edTypeColumn" runat="server" DataField="TypeColumn" AutoRefresh="True" />
            <px:PXSelector ID="edBeginningBalColumn" runat="server" DataField="BeginningBalColumn" AutoRefresh="True" />

            <px:PXLayoutRule runat="server" StartColumn="True" LabelsWidth="SM" ControlSize="M" />
            <px:PXSelector ID="edEndingBalColumn" runat="server" DataField="EndingBalColumn" AutoRefresh="True" />
            <px:PXSelector ID="edDebitColumn" runat="server" DataField="DebitColumn" AutoRefresh="True" />
            <px:PXSelector ID="edCreditColumn" runat="server" DataField="CreditColumn" AutoRefresh="True" />

            <%-- Row 3: Formatting --%>
            <px:PXLayoutRule runat="server" StartRow="True" StartGroup="True" GroupCaption="Formatting" LabelsWidth="SM" ControlSize="M" />
            <px:PXDropDown ID="edRoundingLevel" runat="server" DataField="RoundingLevel" />
            <px:PXLayoutRule runat="server" StartColumn="True" LabelsWidth="SM" ControlSize="S" />
            <px:PXDropDown ID="edDecimalPlaces" runat="server" DataField="DecimalPlaces" />
        </Template>
    </px:PXFormView>
</asp:Content>

<asp:Content ID="cont3" ContentPlaceHolderID="phG" runat="Server">
    <px:PXGrid ID="LineItemsGrid" runat="server" DataSourceID="ds"
        Width="100%" Height="400px"
        SkinID="Details"
        AutoGenerateColumns="None"
        AutoAdjustColumns="True"
        AllowPaging="False"
        Caption="Report Line Items">
        <Levels>
            <px:PXGridLevel DataMember="LineItems">
                <Columns>
                    <px:PXGridColumn DataField="SortOrder"         Width="70"  CommitChanges="True" />
                    <px:PXGridColumn DataField="LineCode"          Width="140" CommitChanges="True" />
                    <px:PXGridColumn DataField="Description"       Width="220" />
                    <px:PXGridColumn DataField="LineType"          Width="130" CommitChanges="True" Type="DropDownList" />
                    <px:PXGridColumn DataField="AccountFrom"       Width="100" CommitChanges="True" />
                    <px:PXGridColumn DataField="AccountTo"         Width="100" CommitChanges="True" />
                    <px:PXGridColumn DataField="AccountTypeFilter" Width="110" CommitChanges="True" Type="DropDownList" />
                    <px:PXGridColumn DataField="BalanceType"       Width="130" CommitChanges="True" Type="DropDownList" />
                    <px:PXGridColumn DataField="SignRule"          Width="90"  CommitChanges="True" Type="DropDownList" />
                    <px:PXGridColumn DataField="ParentLineCode"    Width="140" CommitChanges="True" />
                    <px:PXGridColumn DataField="Formula"           Width="280" CommitChanges="True" />
                    <px:PXGridColumn DataField="IsVisible"          Width="80"  Type="CheckBox" CommitChanges="True" />
                    <px:PXGridColumn DataField="SubaccountFilter"  Width="120" />
                    <px:PXGridColumn DataField="BranchFilter"      Width="100" />
                    <px:PXGridColumn DataField="OrganizationFilter" Width="130" />
                    <px:PXGridColumn DataField="LedgerFilter"      Width="100" />
                </Columns>
                <RowTemplate>
                    <px:PXLayoutRule runat="server" StartRow="True" LabelsWidth="SM" ControlSize="M" />
                    <px:PXNumberEdit  ID="edSortOrder"      runat="server" DataField="SortOrder" />
                    <px:PXTextEdit    ID="edLineCode"       runat="server" DataField="LineCode" />
                    <px:PXTextEdit    ID="edDescription"    runat="server" DataField="Description" />
                    <px:PXDropDown    ID="edLineType"       runat="server" DataField="LineType" CommitChanges="True" AllowEdit="True" />
                    <px:PXLayoutRule runat="server" StartRow="True" LabelsWidth="SM" ControlSize="M" />
                    <px:PXTextEdit    ID="edAccountFrom"    runat="server" DataField="AccountFrom" />
                    <px:PXTextEdit    ID="edAccountTo"      runat="server" DataField="AccountTo" />
                    <px:PXDropDown    ID="edAccountType"    runat="server" DataField="AccountTypeFilter" AllowEdit="True" />
                    <px:PXDropDown    ID="edBalanceType"    runat="server" DataField="BalanceType" AllowEdit="True" />
                    <px:PXDropDown    ID="edSignRule"       runat="server" DataField="SignRule" AllowEdit="True" />
                    <px:PXLayoutRule runat="server" StartRow="True" LabelsWidth="SM" ControlSize="XL" />
                    <px:PXTextEdit    ID="edParentLine"     runat="server" DataField="ParentLineCode" />
                    <px:PXTextEdit    ID="edFormula"        runat="server" DataField="Formula" />
                    <px:PXCheckBox    ID="edIsVisible"      runat="server" DataField="IsVisible" />
                    <px:PXLayoutRule runat="server" StartRow="True" LabelsWidth="SM" ControlSize="M" StartGroup="True" GroupCaption="Dimension Filters (optional)" />
                    <px:PXTextEdit    ID="edSubaccountFilter"    runat="server" DataField="SubaccountFilter" />
                    <px:PXSelector    ID="edBranchFilter"        runat="server" DataField="BranchFilter"        AllowEdit="False" />
                    <px:PXSelector    ID="edOrganizationFilter"  runat="server" DataField="OrganizationFilter"  AllowEdit="False" />
                    <px:PXSelector    ID="edLedgerFilter"        runat="server" DataField="LedgerFilter"        AllowEdit="False" />
                </RowTemplate>
            </px:PXGridLevel>
        </Levels>
        <AutoSize Container="Window" Enabled="True" MinHeight="200" />
        <ActionBar>
            <CustomItems>
                <px:PXToolBarButton Text="Copy Definition" Key="copyDefinition">
                    <AutoCallBack Command="copyDefinition" Target="ds" />
                </px:PXToolBarButton>
                <px:PXToolBarButton Text="Detect Columns" Key="detectColumns">
                    <AutoCallBack Command="detectColumns" Target="ds" />
                </px:PXToolBarButton>
            </CustomItems>
        </ActionBar>
    </px:PXGrid>
</asp:Content>
