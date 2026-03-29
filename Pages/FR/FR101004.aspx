<%@ Page Language="C#" MasterPageFile="~/MasterPages/FormDetail.master" AutoEventWireup="true" ValidateRequest="false" CodeFile="FR101004.aspx.cs" Inherits="Page_FR101004" Title="GI Data Source" %>
<%@ MasterType VirtualPath="~/MasterPages/FormDetail.master" %>

<asp:Content ID="cont1" ContentPlaceHolderID="phDS" runat="Server">
    <px:PXDataSource ID="ds" runat="server" Visible="True" Width="100%"
        TypeName="FinancialReport.FLRTGIDataSourceMaint"
        PrimaryView="DataSource">
        <CallbackCommands>
            <px:PXDSCallbackCommand Name="detectColumns" CommitChanges="True" />
            <px:PXDSCallbackCommand Name="testFetch" CommitChanges="True" />
        </CallbackCommands>
    </px:PXDataSource>
</asp:Content>

<asp:Content ID="cont2" ContentPlaceHolderID="phF" runat="Server">
    <px:PXFormView ID="form" runat="server" DataSourceID="ds" DataMember="DataSource"
        Width="100%" Caption="GI Data Source">
        <Template>
            <%-- Row 1: Identity --%>
            <px:PXLayoutRule runat="server" StartRow="True" LabelsWidth="SM" ControlSize="M" />
            <px:PXSelector  ID="edDataSourceCD" runat="server" DataField="DataSourceCD" />
            <px:PXTextEdit  ID="edPrefix"       runat="server" DataField="Prefix" />
            <px:PXCheckBox  ID="edIsActive"     runat="server" DataField="IsActive" />
            <px:PXLayoutRule runat="server" StartColumn="True" LabelsWidth="SM" ControlSize="XL" />
            <px:PXTextEdit  ID="edDescription"  runat="server" DataField="Description" />

            <%-- Row 2: GI Configuration --%>
            <px:PXLayoutRule runat="server" StartRow="True" StartGroup="True" GroupCaption="Generic Inquiry" LabelsWidth="SM" ControlSize="M" />
            <px:PXSelector  ID="edGIName"    runat="server" DataField="GIName" CommitChanges="True" AutoRefresh="True" />
            <px:PXSelector  ID="edKeyColumn" runat="server" DataField="KeyColumn" AutoRefresh="True" />

            <%-- Row 3: Filter Columns --%>
            <px:PXLayoutRule runat="server" StartRow="True" StartGroup="True" GroupCaption="Filter Columns" LabelsWidth="SM" ControlSize="M" />

            <%-- Period filter --%>
            <px:PXSelector  ID="edPeriodFilterColumn"   runat="server" DataField="PeriodFilterColumn"   AutoRefresh="True" />
            <px:PXDropDown  ID="edPeriodFilterType"     runat="server" DataField="PeriodFilterType" />
            <px:PXDropDown  ID="edPeriodScope"         runat="server" DataField="PeriodScope" />
            <px:PXTextEdit  ID="edPeriodFilterTemplate" runat="server" DataField="PeriodFilterTemplate" />

            <%-- Branch / Org --%>
            <px:PXLayoutRule runat="server" StartColumn="True" LabelsWidth="SM" ControlSize="M" />
            <px:PXSelector  ID="edBranchFilterColumn" runat="server" DataField="BranchFilterColumn" AutoRefresh="True" />
            <px:PXDropDown  ID="edBranchFilterType"   runat="server" DataField="BranchFilterType" />
            <px:PXSelector  ID="edOrgFilterColumn"    runat="server" DataField="OrgFilterColumn"    AutoRefresh="True" />
            <px:PXDropDown  ID="edOrgFilterType"      runat="server" DataField="OrgFilterType" />

            <%-- Ledger --%>
            <px:PXLayoutRule runat="server" StartColumn="True" LabelsWidth="SM" ControlSize="M" />
            <px:PXSelector  ID="edLedgerFilterColumn" runat="server" DataField="LedgerFilterColumn" AutoRefresh="True" />
            <px:PXDropDown  ID="edLedgerFilterType"   runat="server" DataField="LedgerFilterType" />
        </Template>
    </px:PXFormView>

    <%-- Test Fetch Dialog --%>
    <px:PXSmartPanel ID="pnlTestFetch" runat="server" Caption="Test Fetch Parameters"
        CaptionVisible="True" Key="TestFilter"
        AutoRepaint="True" Width="400px" Height="250px"
        LoadOnDemand="True" ShowAfterLoad="True"
        AcceptButtonID="btnTestOK" CancelButtonID="btnTestCancel">
        <px:PXFormView ID="frmTestFilter" runat="server" DataSourceID="ds"
            DataMember="TestFilter" Width="100%">
            <Template>
                <px:PXLayoutRule runat="server" StartRow="True" LabelsWidth="SM" ControlSize="M" />
                <px:PXTextEdit  ID="edTestYear"         runat="server" DataField="TestYear" />
                <px:PXDropDown  ID="edTestMonth"        runat="server" DataField="TestMonth" />
                <px:PXTextEdit  ID="edTestBranch"       runat="server" DataField="TestBranch" />
                <px:PXTextEdit  ID="edTestOrganization" runat="server" DataField="TestOrganization" />
                <px:PXTextEdit  ID="edTestLedger"       runat="server" DataField="TestLedger" />
            </Template>
        </px:PXFormView>
        <px:PXPanel ID="pnlTestButtons" runat="server" SkinID="Buttons">
            <px:PXButton ID="btnTestOK" runat="server" DialogResult="OK" Text="Fetch" />
            <px:PXButton ID="btnTestCancel" runat="server" DialogResult="Cancel" Text="Cancel" />
        </px:PXPanel>
    </px:PXSmartPanel>
</asp:Content>

<asp:Content ID="cont3" ContentPlaceHolderID="phG" runat="Server">
    <px:PXGrid ID="ColumnsGrid" runat="server" DataSourceID="ds"
        Width="100%" Height="400px"
        SkinID="Details"
        AutoGenerateColumns="None"
        AutoAdjustColumns="True"
        AllowPaging="False"
        Caption="Columns">
        <Levels>
            <px:PXGridLevel DataMember="Columns">
                <Columns>
                    <px:PXGridColumn DataField="SortOrder"         Width="70"  CommitChanges="True" />
                    <px:PXGridColumn DataField="ColumnAlias"       Width="140" CommitChanges="True" />
                    <px:PXGridColumn DataField="Description"       Width="220" />
                    <px:PXGridColumn DataField="LineType"          Width="140" CommitChanges="True" Type="DropDownList" />
                    <px:PXGridColumn DataField="GIColumn"          Width="160" CommitChanges="True" />
                    <px:PXGridColumn DataField="ColumnType"        Width="100" CommitChanges="True" Type="DropDownList" />
                    <px:PXGridColumn DataField="AggregateFunction" Width="100" CommitChanges="True" Type="DropDownList" />
                    <px:PXGridColumn DataField="KeyFrom"           Width="110" CommitChanges="True" />
                    <px:PXGridColumn DataField="KeyTo"             Width="110" CommitChanges="True" />
                    <px:PXGridColumn DataField="RowFilter"         Width="220" />
                    <px:PXGridColumn DataField="OrderByColumn"     Width="160" CommitChanges="True" />
                    <px:PXGridColumn DataField="OrderByDirection"  Width="120" CommitChanges="True" Type="DropDownList" />
                    <px:PXGridColumn DataField="RowLimit"          Width="90"  CommitChanges="True" />
                    <px:PXGridColumn DataField="DisplayColumns"    Width="200" />
                    <px:PXGridColumn DataField="Formula"           Width="220" CommitChanges="True" />
                    <px:PXGridColumn DataField="FormatString"      Width="110" />
                    <px:PXGridColumn DataField="IsVisible"         Width="70"  Type="CheckBox" CommitChanges="True" />
                </Columns>
                <RowTemplate>
                    <px:PXLayoutRule runat="server" StartRow="True" LabelsWidth="SM" ControlSize="M" />
                    <px:PXNumberEdit ID="edSortOrder"    runat="server" DataField="SortOrder" />
                    <px:PXTextEdit   ID="edColumnAlias"  runat="server" DataField="ColumnAlias" />
                    <px:PXTextEdit   ID="edDescription2" runat="server" DataField="Description" />
                    <px:PXDropDown   ID="edLineType"     runat="server" DataField="LineType" CommitChanges="True" AllowEdit="True" />

                    <px:PXLayoutRule runat="server" StartRow="True" StartGroup="True" GroupCaption="Value Settings" LabelsWidth="SM" ControlSize="M" />
                    <px:PXSelector   ID="edGIColumn"          runat="server" DataField="GIColumn"          AutoRefresh="True" />
                    <px:PXDropDown   ID="edColumnType"        runat="server" DataField="ColumnType"        AllowEdit="True" />
                    <px:PXDropDown   ID="edAggregateFunction" runat="server" DataField="AggregateFunction" AllowEdit="True" />
                    <px:PXTextEdit   ID="edKeyFrom"           runat="server" DataField="KeyFrom" />
                    <px:PXTextEdit   ID="edKeyTo"             runat="server" DataField="KeyTo" />
                    <px:PXTextEdit   ID="edRowFilter"         runat="server" DataField="RowFilter" />

                    <px:PXLayoutRule runat="server" StartRow="True" StartGroup="True" GroupCaption="Multi-Row Expand Settings" LabelsWidth="SM" ControlSize="M" />
                    <px:PXSelector   ID="edOrderByColumn"    runat="server" DataField="OrderByColumn" AutoRefresh="True" />
                    <px:PXDropDown   ID="edOrderByDirection" runat="server" DataField="OrderByDirection" AllowEdit="True" />
                    <px:PXNumberEdit ID="edRowLimit"         runat="server" DataField="RowLimit" />
                    <px:PXTextEdit   ID="edDisplayColumns"   runat="server" DataField="DisplayColumns" />

                    <px:PXLayoutRule runat="server" StartRow="True" StartGroup="True" GroupCaption="Formula (Calculated lines only)" LabelsWidth="SM" ControlSize="XL" />
                    <px:PXTextEdit   ID="edFormula"       runat="server" DataField="Formula" />

                    <px:PXLayoutRule runat="server" StartRow="True" StartGroup="True" GroupCaption="Display" LabelsWidth="SM" ControlSize="M" />
                    <px:PXTextEdit   ID="edFormatString"  runat="server" DataField="FormatString" />
                    <px:PXCheckBox   ID="edIsVisible2"    runat="server" DataField="IsVisible" />
                </RowTemplate>
            </px:PXGridLevel>
        </Levels>
        <AutoSize Container="Window" Enabled="True" MinHeight="200" />
        <ActionBar>
            <CustomItems>
                <px:PXToolBarButton Text="Detect Columns" Key="detectColumns">
                    <AutoCallBack Command="detectColumns" Target="ds" />
                </px:PXToolBarButton>
                <px:PXToolBarButton Text="Test Fetch" Key="testFetch">
                    <AutoCallBack Command="testFetch" Target="ds" />
                </px:PXToolBarButton>
            </CustomItems>
        </ActionBar>
    </px:PXGrid>
</asp:Content>
