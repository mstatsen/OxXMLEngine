﻿using OxLibrary;
using OxLibrary.Panels;
using OxXMLEngine.ControlFactory.Filter;
using OxXMLEngine.Data;
using OxXMLEngine.Data.Types;
using OxXMLEngine.Grid;

namespace OxXMLEngine.Statistic
{
    public partial class StatisticPanel<TField, TDAO> : OxFrame
        where TField : notnull, Enum
        where TDAO : RootDAO<TField>, new()
    {
        public StatisticPanel(
            ItemsGrid<TField, TDAO> grid,
            QuickFilterPanel<TField, TDAO> quickFilterPanel) : base(new Size(100, 24))
        {
            Grid = grid;
            QuickFilterPanel = quickFilterPanel;
            AccessHandlers();
            SetStatisticText();
            Borders.SetSize(OxSize.None);
            Borders.TopOx = OxSize.Small;
        }

        protected override void PrepareInnerControls()
        {
            base.PrepareInnerControls();
            CreateLabels();
        }

        protected override void PrepareColors()
        {
            base.PrepareColors();

            foreach (StatisticType statisticType in Labels.Keys)
                PrepareStatisticColor(statisticType, 0);
        }

        private readonly IListController<TField, TDAO> ListController = 
            DataManager.ListController<TField, TDAO>();

        private int Statistic(StatisticType type) => 
            type switch
            {
                StatisticType.Visible => ListController.VisibleItemsList.FilteredList(QuickFilterPanel.ActiveFilter).Count,
                StatisticType.Selected => Grid.SelectedCount,
                StatisticType.Modified => ListController.ModifiedCount,
                StatisticType.Added => ListController.AddedCount,
                StatisticType.Deleted => ListController.DeletedCount,
                _ => 0,
            };

        public void Renew() =>
            SetStatisticText();

        private void SetStatisticText()
        {
            foreach (var item in Labels)
            {
                int newStatistic = 0;

                switch (item.Key)
                {
                    case StatisticType.Total:
                        item.Value.StatisticText = $"Total {ListController.Name} : {ListController.TotalCount}";
                        break;
                    case StatisticType.Category:
                        RenewCategoryValue();
                        break;
                    default:
                        newStatistic = Statistic(item.Key);
                        item.Value.StatisticText = $"{helper.Name(item.Key)}: {newStatistic}";
                        break;
                }

                PrepareStatisticColor(item.Key, newStatistic);
            }
        }

        private void RenewCategoryValue() => 
            Labels[StatisticType.Category].StatisticText =
                ListController.Category != null &&
                ListController.Category?.Name != null &&
                ListController.Category?.Name != string.Empty
                    ? $"{ListController.Category?.Name} : {ListController.FilteredCount}"
                    : string.Empty;

        private readonly StatisticTypeHelper helper = TypeHelper.Helper<StatisticTypeHelper>();

        private void CreateLabel(StatisticType type) =>
            Labels.Add(type,
                new StatisticLabel()
                {
                    Parent = ContentContainer,
                    Dock = helper.Dock(type),
                    Width = helper.Width(type)
                });

        private void CreateLabels()
        {
            foreach (StatisticType type in helper.All())
                CreateLabel(type);

            foreach (StatisticLabel label in Labels.Values)
            {
                label.BringToFront();
                label.Margins.SetSize(OxSize.Medium);
            }
        }

        private readonly OxColorHelper CategoryColorHelper = new(EngineStyles.CategoryColor);
        private readonly OxColorHelper QuickFilterColorHelper = new(EngineStyles.QuickFilterColor);

        private void PrepareStatisticColor(StatisticType type, int statistic)
        {
            switch (type)
            {
                case StatisticType.Category:
                    Labels[type].BaseColor = CategoryColorHelper.Darker(
                        ListController.Category == null || ListController.Category.FilterIsEmpty
                        ? 0
                        : 2);
                    break;
                case StatisticType.Visible:
                    Labels[type].BaseColor = QuickFilterColorHelper.Darker(
                            QuickFilterPanel == null ||
                            QuickFilterPanel.ActiveFilter == null ||
                            QuickFilterPanel.ActiveFilter.FilterIsEmpty
                            ? 0
                            : 2
                        );
                    break;
                default:
                    OxColorHelper labelColorHelper = new OxColorHelper(BaseColor).HDarker(1);
                    Labels[type].BaseColor = (statistic > 0) &&
                        (type == StatisticType.Modified
                        || type == StatisticType.Added
                        || type == StatisticType.Deleted)
                        ? labelColorHelper.Redder(6)
                        : labelColorHelper.Lighter(1);
                    break;
            }
        }

        private void AccessHandlers()
        {
            ListController.CategoryChanged += (s, e) => RenewCategoryValue();
            ListController.ListChanged += (s, e) => SetStatisticText();
            ListController.ModifiedHandler += (d, m) => SetStatisticText();
            ListController.ItemFieldChanged += (d, e) => SetStatisticText();
            QuickFilterPanel.Changed += (s, e) => SetStatisticText();
            Grid.CurrentItemChanged += (s, e) => SetStatisticText();
        }

        private readonly Dictionary<StatisticType, StatisticLabel> Labels = new();
        private readonly ItemsGrid<TField, TDAO> Grid;
        private readonly QuickFilterPanel<TField, TDAO> QuickFilterPanel;
    }
}