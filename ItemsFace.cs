﻿using OxLibrary;
using OxLibrary.Panels;
using OxXMLEngine.ControlFactory.Filter;
using OxXMLEngine.Data;
using OxXMLEngine.Data.Filter;
using OxXMLEngine.Data.Types;
using OxXMLEngine.Grid;
using OxXMLEngine.Settings;
using OxXMLEngine.Statistic;
using OxXMLEngine.Summary;
using OxXMLEngine.View;

namespace OxXMLEngine
{
    public class ItemsFace<TField, TDAO> : OxPane, IDataReceiver
        where TField : notnull, Enum
        where TDAO : RootDAO<TField>, new()
    {
        public IListController<TField, TDAO> ListController =
            DataManager.ListController<TField, TDAO>();

        public ItemsFace()
        {
            DataReceivers.Register(this);
            Text = ListController.Name;
            Dock = DockStyle.Fill;
            Font = EngineStyles.DefaultFont;

            tabControlPanel.Parent = this;
            tabControlPanel.Dock = DockStyle.Fill;
            tabControl = CreateTabControl();
            tableView = CreateTableView();
            cardsView = CreateView(ItemsViewsType.Cards);
            iconsView = CreateView(ItemsViewsType.Icons);
            summaryView = CreateSummaryView();
            ActivateFirstPage();

            PrepareQuickFilter();
            PrepareLoadingPanel();
            PrepareCategoriesTree();
            //sortingPanel.Visible = false;

            statisticPanel = CreateStatisticPanel();
            ListController.ListChanged += ListChangedHandler;
            ListController.OnAfterLoad += RenewFilterControls;
            tabControl.ActivatePage += ActivatePageHandler;
            tabControl.DeactivatePage += DeactivatePageHandler;
        }

        private StatisticPanel<TField, TDAO> CreateStatisticPanel() =>
            new(tableView.Grid, quickFilter)
            {
                Dock = DockStyle.Bottom,
                Parent = tabControlPanel
            };

        private void ActivateFirstPage()
        {
            IOxPane? firstPage = tabControl.Pages.First;

            if (firstPage != null)
                tabControl.TabButtons[firstPage].Margins.LeftOx = OxSize.Medium;

            tabControl.ActivePage = firstPage;
        }

        private OxTabControl CreateTabControl()
        {
            OxTabControl result = new()
            {
                Parent = tabControlPanel,
                Dock = DockStyle.Fill,
                Font = EngineStyles.DefaultFont,
                TabHeaderSize = new Size(84, 24),
                TabPosition = OxDock.Bottom,
            };

            result.Margins.SetSize(OxSize.None);
            result.Margins.BottomOx = OxSize.Small;
            result.Borders[OxDock.Left].Visible = false;
            result.Borders[OxDock.Right].Visible = false;
            return result;
        }

        private ItemsView<TField, TDAO> CreateView(ItemsViewsType viewType)
        {
            ItemsView<TField, TDAO> itemsView =
                new(viewType)
                {
                    Text = itemsViewsTypeHelper.Name(viewType),
                    BaseColor = BaseColor
                };

            itemsView.LoadingStarted += LoadingStartedHandler;
            itemsView.LoadingEnded += LoadingEndedHandler;
            tabControl.AddPage(itemsView);
            return itemsView;
        }

        private TableView<TField, TDAO> CreateTableView()
        {
            TableView<TField, TDAO> result = new()
            {
                Parent = tabControl,
                Dock = DockStyle.Fill,
                Text = itemsViewsTypeHelper.Name(ItemsViewsType.Table),
                BaseColor = BaseColor,
                BatchUpdateCompleted = BatchUpdateCompletedHandler
            };
            result.Paddings.LeftOx = OxSize.Medium;
            result.GridFillCompleted += TableFillCompleteHandler;
            tabControl.AddPage(result);
            return result;
        }

        private SummaryView<TField, TDAO> CreateSummaryView()
        {
            SummaryView<TField, TDAO> result = new()
            {
                Dock = DockStyle.Fill,
                Text = itemsViewsTypeHelper.Name(ItemsViewsType.Summary),
                BaseColor = BaseColor
            };
            result.Paddings.LeftOx = OxSize.Medium;
            tabControl.AddPage(result);
            return result;
        }

        private readonly ItemsViewsTypeHelper itemsViewsTypeHelper = 
            TypeHelper.Helper<ItemsViewsTypeHelper>();

        private void BatchUpdateCompletedHandler(object? sender, EventArgs e)
        {
            SortList();
            categoriesTree.RefreshCategories();
            quickFilter.RenewFilterControls();
            statisticPanel.Renew();
            tableView.Renew();
            tableView.SelectFirstItem();
        }

        private void SortList()
        {
            StartLoading(tabControl);

            try
            {
                ListController.Sort();
                ApplyQuickFilter(true);
            }
            finally
            {
                EndLoading();
            }
        }

        private bool QuickFilterChanged()
        {
            RootListDAO<TField, TDAO> newActualItemList = ListController.VisibleItemsList
                .FilteredList(quickFilter?.ActiveFilter, Settings.Sortings.SortingsList);

            if (actualItemList != null
                && actualItemList.Equals(newActualItemList))
                return false;

            actualItemList = newActualItemList;
            return true;
        }

        private void ApplyQuickFilter(bool Force = false)
        {
            if (!QuickFilterChanged() && !Force)
                return;

            StartLoading(tabControl);

            try
            {
                tableView.ApplyQuickFilter(quickFilter.ActiveFilter);

                if (tabControl.ActivePage == cardsView)
                    cardsView.Fill(actualItemList);

                if (tabControl.ActivePage == iconsView)
                    iconsView.Fill(actualItemList);
            }
            finally
            {
                EndLoading();
            }
        }

        private static DAOSettings<TField, TDAO> Settings =>
            SettingsManager.DAOSettings<TField, TDAO>();

        public virtual void ApplySettings()
        {
            /*
            if (ItemsFace<TField, TDAO>.Settings.Observer.SortingFieldsChanged)
            {
                sortingPanel.Sortings = ItemsFace<TField, TDAO>.Settings.Sortings;
                SortList();
            }
            */

            if (ItemsFace<TField, TDAO>.Settings.Observer[DAOSetting.ShowCategories])
                categoriesTree.Visible = ItemsFace<TField, TDAO>.Settings.ShowCategories;

            if (categoriesTree.Expanded != ItemsFace<TField, TDAO>.Settings.CategoryPanelExpanded)
                categoriesTree.Expanded = ItemsFace<TField, TDAO>.Settings.CategoryPanelExpanded;

            tableView.ApplySettings();
            //sortingPanel.ApplySettings();
            quickFilter.ApplySettings();
            categoriesTree.ApplySettings();

            if (Settings.Observer.QuickFilterFieldsChanged)
                quickFilter.RecalcPaddings();

            if (Settings.Observer[DAOSetting.ShowIcons])
            {
                tabControl.TabButtons[iconsView].Visible = ItemsFace<TField, TDAO>.Settings.ShowIcons;
                iconsView.Visible = ItemsFace<TField, TDAO>.Settings.ShowIcons;
            }

            iconsView.ApplySettings();

            if (Settings.Observer[DAOSetting.ShowCards])
            {
                tabControl.TabButtons[cardsView].Visible = ItemsFace<TField, TDAO>.Settings.ShowCards;
                cardsView.Visible = ItemsFace<TField, TDAO>.Settings.ShowCards;
            }

            cardsView.ApplySettings();
        }

        public virtual void SaveSettings()
        {
            tableView.SaveSettings();
            //sortingPanel.SaveSettings();
            quickFilter.SaveSettings();
            categoriesTree.SaveSettings();
            ItemsFace<TField, TDAO>.Settings.CategoryPanelExpanded = categoriesTree.Expanded;
            //ItemsFace<TField, TDAO>.Settings.Sortings = sortingPanel.Sortings;
        }

        private void PrepareLoadingPanel()
        {
            loadingPanel.Parent = tabControl;
            loadingPanel.Visible = false;
            loadingPanel.Margins.TopOx = OxSize.Large;
            loadingPanel.Borders.SetSize(OxSize.Small);
        }

        private void StartLoading(IOxPane? parentPanel = null)
        {
            loadingPanel.Parent = parentPanel == null ? this : (Control)parentPanel;
            loadingPanel.StartLoading();
        }

        private void LoadingStartedHandler(object? sender, EventArgs e) =>
            StartLoading(sender == null ? this : ((ItemsView<TField, TDAO>)sender).ContentContainer);

        private void LoadingEndedHandler(object? sender, EventArgs e) =>
            EndLoading();

        private void EndLoading() => loadingPanel.EndLoading();

        private void PrepareQuickFilter()
        {
            quickFilter.Parent = tabControlPanel;
            quickFilter.Dock = DockStyle.Top;
            quickFilter.Changed += QuickFilterChangedHandler;
            quickFilter.Margins.SetSize(OxSize.Large);
            quickFilter.RenewFilterControls();
            quickFilter.OnPinnedChanged += QuickFilterPinnedChangedHandler;
            quickFilter.VisibleChanged += QuickFilterVisibleChangedHandler;
            quickFilter.RecalcPaddings();
        }

        private void QuickFilterPinnedChangedHandler(object? sender, EventArgs e) => 
            categoriesTree.RecalcPinned();

        private void QuickFilterVisibleChangedHandler(object? sender, EventArgs e) =>
            quickFilter.RecalcPaddings();

        private void PrepareCategoriesTree()
        {
            //categoriesTree.OnExpandedChanged += CategoriesTreeExpandedHandler;
            //categoriesTree.OnAfterCollapse += CategoriesTreeAfterCollapseHandler;

            categoriesTree.Parent = this;
            categoriesTree.Dock = DockStyle.Left;
            categoriesTree.Margins.TopOx = OxSize.Large;
            categoriesTree.Margins.LeftOx = OxSize.Medium;
            categoriesTree.Margins.BottomOx = OxSize.Small;
            categoriesTree.Margins.RightOx = OxSize.None;
            categoriesTree.Paddings.SetSize(OxSize.Medium);
            categoriesTree.Borders[OxDock.Right].Visible = false;
            categoriesTree.ActiveCategoryChanged += ActiveCategoryChangedHandler;
            categoriesTree.ActiveCategoryChanged += RenewFilterControls;
        }

        private void QuickFilterChangedHandler(object? sender, EventArgs e) =>
            ApplyQuickFilter();

        private void TableFillCompleteHandler(object? sender, EventArgs e) =>
            ApplyQuickFilter();

        /*
        private void SortChangedHandler(DAO dao, DAOEntityEventArgs e)
        {
            ItemsFace<TField, TDAO>.Settings.Sortings = sortingPanel.Sortings;
            SortList();
        }
        */

        private void ActivatePageHandler(object sender, OxTabControlEventArgs e) =>
            ApplyQuickFilter(true);

        private void DeactivatePageHandler(object sender, OxTabControlEventArgs e)
        {
            //sortingPanel.Visible = e.Page != tableView;
        }

        private void ListChangedHandler(object? sender, EventArgs e) =>
            ApplyQuickFilter(true);

        public void RenewFilterControls(object? sender, CategoryEventArgs<TField, TDAO> e)
        {
            if (e.IsFilterChanged)
                quickFilter.RenewFilterControls();
        }

        public void RenewFilterControls(object? sender, EventArgs e) =>
            quickFilter.RenewFilterControls();

        private void ActiveCategoryChangedHandler(object? sender, CategoryEventArgs<TField, TDAO> e)
        {
            StartLoading(tabControl);

            try
            {
                ListController.Category = categoriesTree?.ActiveCategory;

                if (e.IsFilterChanged)
                    tableView.FillGrid();
            }
            finally
            {
                EndLoading();
            }
        }

        /*
        private void CategoriesTreeAfterCollapseHandler(object? sender, EventArgs e) =>
            categoriesTree.Enabled = true;//TODO:

        private void CategoriesTreeExpandedHandler(object? sender, EventArgs e) =>
            categoriesTree.Enabled = true;
        */

        public void FillData()
        {
            tableView.FillGrid();
            summaryView.RefreshData(true);
            ApplyQuickFilter(true);
        }

        protected override void PrepareColors()
        {
            base.PrepareColors();

            if (statisticPanel != null)
                statisticPanel.BaseColor = BaseColor;
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            quickFilter.RecalcPaddings();
        }

        public SettingsPart ActiveSettingsPart => tabControl.ActivePage != tableView 
            ? SettingsPart.View
            : SettingsPart.Table;

        public override void ReAlignControls()
        {
            base.ReAlignControls();
            /*
            categoriesTree?.SendToBack();
            quickFilter?.SendToBack();
            tabControl?.SendToBack();
            */
        }

        private readonly TableView<TField, TDAO> tableView;
        private readonly ItemsView<TField, TDAO> cardsView;
        private readonly ItemsView<TField, TDAO> iconsView;
        private readonly SummaryView<TField, TDAO> summaryView;
        private readonly QuickFilterPanel<TField, TDAO> quickFilter = new(QuickFilterVariant.Base);
        private readonly CategoriesTree<TField, TDAO> categoriesTree = new();
        private readonly OxLoadingPanel loadingPanel = new();
        //private readonly SortingPanel<TField, TDAO> sortingPanel = new(SortingVariant.Global, ControlScope.Table);
        private RootListDAO<TField, TDAO>? actualItemList;
        private readonly OxTabControl tabControl;
        private readonly OxPane tabControlPanel = new();
        private readonly StatisticPanel<TField, TDAO> statisticPanel;
    }
}