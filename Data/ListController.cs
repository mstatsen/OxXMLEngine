﻿using OxLibrary.Dialogs;
using OxLibrary.Panels;
using OxXMLEngine.ControlFactory;
using OxXMLEngine.Data.Decorator;
using OxXMLEngine.Data.Fields;
using OxXMLEngine.Data.Filter;
using OxXMLEngine.Data.History;
using OxXMLEngine.Data.Sorting;
using OxXMLEngine.Editor;
using OxXMLEngine.Export;
using OxXMLEngine.Grid;
using OxXMLEngine.Settings;
using OxXMLEngine.Statistic;
using OxXMLEngine.Summary;
using OxXMLEngine.View;
using System.Xml;

namespace OxXMLEngine.Data
{
    public abstract class ListController<TField, TDAO, TFieldGroup, TListController> 
        : IListController<TField, TDAO, TFieldGroup>
        where TField : notnull, Enum
        where TDAO : RootDAO<TField>, new()
        where TFieldGroup : notnull, Enum
        where TListController : ListController<TField, TDAO, TFieldGroup, TListController>, new()
    {
        private readonly RootListDAO<TField, TDAO> fullItemsList;
        public RootListDAO<TField, TDAO> FullItemsList => fullItemsList;
        public bool Modified => FullItemsList.Modified;

        public ListController()
        {
            fieldHelper = RegisterFieldHelper();
            fieldGroupHelper = RegisterFieldGroupHelper();
            RegisterHelpers();
            Settings = new DAOSettings<TField, TDAO>();
            fullItemsList = new();
            SetListHandlers();
        }

        private void AfterLoad() =>
            OnAfterLoad?.Invoke(this, EventArgs.Empty);

        protected virtual void AfterSave() { }

        protected virtual void BeforeLoad() { }

        protected virtual void BeforeSave() { }

        public void Save(XmlElement? parentElement)
        {
            BeforeSave();
            FullItemsList.Save(parentElement);
            Sort();
            AfterSave();
        }

        public void Load(XmlElement? parentElement)
        {
            BeforeLoad();
            FullItemsList.Load(parentElement);
            AfterLoad();
        }

        protected void NotifyAll()
        {
            if (FullItemsList.Loading)
                return;

            ProcessNotifyAll();
        }

        protected virtual void ProcessNotifyAll()
        {
            Sort();
            ListChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Sort() =>
            FullItemsList.Sort(Settings.Sortings.SortingsList, true);

        protected void SetListHandlers()
        {
            FullItemsList.ChangeHandler += ListChangedHandler;
            FullItemsList.ModifiedChangeHandler += ListModifiedChangeHandler;
            FullItemsList.FieldModified += FieldModifiedHanlder;
            FullItemsList.ItemAddHandler += (d, e) => AddHandler?.Invoke(d, e);
            FullItemsList.ItemRemoveHandler += (d, e) => RemoveHandler?.Invoke(d, e);
            FullItemsList.SortChangeHandler += ItemListSortChanger;
        }

        private void FieldModifiedHanlder(FieldModifiedEventArgs<TField> e)
        {
            if (e.DAO is TDAO eDao)
                History.ChangeField(eDao, e.Field, e.OldValue);
        }

        private void ItemListSortChanger(object? sender, EventArgs e)
        {
            RenewVisibleItems();
            ItemsSortChangeHandler?.Invoke(this, e);
        }

        public ItemHistoryList<TField, TDAO> History { get; } = new();

        protected virtual void ListModifiedChangeHandler(DAO dao, DAOModifyEventArgs e)
        {
            Sort();
            RenewListsAndNotifyAll();
            ModifiedHandler?.Invoke(dao, e);

            if (!e.Modified)
                History.Clear();
        }

        private void ListChangedHandler(DAO dao, DAOEntityEventArgs? e) =>
            RenewListsAndNotifyAll();

        private Category<TField, TDAO>? category;

        public Category<TField, TDAO>? Category
        {
            get => category;
            set
            {
                category = value;
                CategoryChanged?.Invoke(category, EventArgs.Empty);
                RenewListsAndNotifyAll();
            }
        }

        public event EventHandler? CategoryChanged;

        protected void RenewListsAndNotifyAll()
        {
            if (RenewVisibleItems())
                NotifyAll();
        }

        private RootListDAO<TField, TDAO>? visibleItemsList;

        public RootListDAO<TField, TDAO> VisibleItemsList
        {
            get
            {
                if (visibleItemsList == null)
                    RenewVisibleItems();

                return visibleItemsList ?? new();
            }
        }

        public bool RenewVisibleItems()
        {
            RootListDAO<TField, TDAO> newVisibleItems = FullItemsList.FilteredList(Category, 
                Settings.Sortings.SortingsList);

            if (newVisibleItems.Equals(visibleItemsList))
                return false;

            visibleItemsList = newVisibleItems;
            return true;
        }

        private void EditNewItem(TDAO item)
        {
            if (GetItemEditor(item).ShowDialog() == DialogResult.OK)
            {
                History.AddDAO(item);
                FullItemsList.NotifyAboutItemAdded(item);
                item.NotifyAll(DAOOperation.Add);
                FullItemsList.Add(item);
                RenewVisibleItems();
                Sort();
                ItemFieldChanged?.Invoke(item, new DAOEntityEventArgs(DAOOperation.Add));
            }
        }

        public void AddItem() => 
            EditNewItem(new());

        public void EditItem(TDAO? item)
        {
            if (item == null)
                return;

            if (GetItemEditor(item).ShowDialog() == DialogResult.OK)
            {
                RenewListsAndNotifyAll();
                ItemFieldChanged?.Invoke(item, new DAOEntityEventArgs(DAOOperation.Modify));
            }
        }

        private ItemSelector<TField, TDAO>? itemSelector;

        public bool SelectItem(out TDAO? selectedItem, OxPane parentPane, TDAO? initialItem = null, IMatcher<TField>? filter = null)
        {
            if (itemSelector == null)
                itemSelector = new();

            itemSelector.BaseColor = parentPane.BaseColor;
            itemSelector.Filter = filter;
            itemSelector.SelectedItem = initialItem;

            bool result = itemSelector.ShowAsDialog(OxDialogButton.OK | OxDialogButton.Cancel) == DialogResult.OK;
            selectedItem = result ? itemSelector.SelectedItem : null;
            return result;
        }

        public void CopyItem(TDAO? item)
        {
            if (item == null)
                return;

            TDAO newItem = new();
            newItem.CopyFrom(item, true);
            EditNewItem(newItem);
        }

        public void ViewItem(TField field, object? value, ItemViewMode viewMode = ItemViewMode.Simple) =>
            ViewItem(Item(field, value), viewMode);

        public void ViewItem(TDAO? item, ItemViewMode viewMode = ItemViewMode.Simple)
        {
            if (item == null)
                return;

            IItemCard<TField, TDAO>? card = ControlFactory.CreateCard(viewMode);

            if (card == null)
            {
                EditItem(item);
                return;
            }

            card.Item = item;
            card.ShowAsDialog();
        }

        public void ViewItems(TField field, object? value)
        {
            ItemsViewer<TField, TDAO>? itemsViewer = new();

            try
            {

                if (value == null)
                    itemsViewer.Text = $"{Name} where {fieldHelper.Name(field)} blank";
                else itemsViewer.Text = $"{Name} where {fieldHelper.Name(field)} = {value}";

                itemsViewer.Filter = new SimpleFilter<TField, TDAO>().AddFilter(field, value);
                itemsViewer.ShowAsDialog(OxDialogButton.Cancel);
            }
            finally
            {
                itemsViewer.Dispose();
            }
        }

        private class HistoryGridPainter : GridPainter<TField, ItemHistory<TField, TDAO>>
        {
            private readonly GridPainter<TField, TDAO>? DAOPainter;
            //TODO: additional columns paint
            public override DataGridViewCellStyle? GetCellStyle(ItemHistory<TField, TDAO>? item, TField field, bool selected = false) => 
                DAOPainter?.GetCellStyle(item?.DAO, field, selected);

            public HistoryGridPainter(ItemsGrid<TField, ItemHistory<TField, TDAO>> grid) : base(grid.GridFieldColumns)
            {
                DAOPainter = DataManager.ControlFactory<TField, TDAO>().CreateGridPainter(grid.GridFieldColumns, grid.Usage);
            }
        }

        public void ViewHistory()
        {
            ItemsGrid<TField, ItemHistory<TField, TDAO>> historyGrid = new(History, GridUsage.ViewItems)
            {
                Text = "Items History",
                Fields = new List<TField>()
                {
                    fieldHelper.TitleField
                },
                AdditionalColumns = new()
                {
                    new CustomGridColumn<TField, ItemHistory<TField, TDAO>>("Operation",
                        (h) => h.Operation,
                    80),
                    new CustomGridColumn<TField, ItemHistory<TField, TDAO>>("Field", 
                        (h) => h is FieldHistory<TField, TDAO> fh ? fieldHelper.Name(fh.Field) : string.Empty, 
                    80),
                    new CustomGridColumn<TField, ItemHistory<TField, TDAO>>("Old Value",
                        (h) => h is FieldHistory<TField, TDAO> fh ? fh.OldValue : string.Empty,
                    80),
                    new CustomGridColumn<TField, ItemHistory<TField, TDAO>>("New Value",
                        (h) => h is FieldHistory<TField, TDAO> fh ? fh.NewValue : string.Empty,
                    80)
                }, 
            };

            try
            {
                historyGrid.Painter = new HistoryGridPainter(historyGrid);
                historyGrid.GridView.DoubleClick += (s, e) => ViewItem(historyGrid.CurrentItem?.DAO);
                historyGrid.SetContentSize(1024, 768);
                historyGrid.Fill();
                historyGrid.ShowAsDialog(OxDialogButton.Cancel);
            }
            finally
            {
                historyGrid.Dispose();
            }
        }


        private DAOEditor<TField, TDAO, TFieldGroup>? editor;

        public DAOEditor<TField, TDAO, TFieldGroup> Editor
        {
            get
            {
                if (editor == null)
                    editor = CreateEditor();

                return editor;
            }
        }

        private DAOEditor<TField, TDAO, TFieldGroup> GetItemEditor(TDAO item)
        {
            Editor.Item = item;
            return Editor;
        }


        protected abstract DAOEditor<TField, TDAO, TFieldGroup> CreateEditor();
        protected abstract DAOWorker<TField, TDAO, TFieldGroup> CreateWorker();

        private DAOWorker<TField, TDAO, TFieldGroup>? worker;
        public DAOWorker<TField, TDAO, TFieldGroup> Worker
        {
            get
            {
                if (worker == null)
                    worker = CreateWorker();

                return worker;
            }
        }

        public int TotalCount => FullItemsList.Count;
        public int FilteredCount => VisibleItemsList.Count;
        public int ModifiedCount => History.DistinctModifiedDAOCount;
        public int AddedCount => History.AddedCount;
        public int RemovedCount => History.RemovedCount;
        

        public void Delete(RootListDAO<TField, TDAO> list)
        {
            string messageBase = "Are you sure you want to delete selected";
            string messageSuffix = list.Count > 1 ? $"({list.Count}) items" : "item";

            if (!OxMessage.Confirmation($"{messageBase} {messageSuffix}?"))
                return;

            foreach (TDAO item in list)
                item.NotifyAll(DAOOperation.Remove);

            FullItemsList.StartSilentChange();

            foreach (TDAO item in list)
                if (FullItemsList.Remove(item))
                    History.RemoveDAO(item);

            FullItemsList.FinishSilentChange();
            RenewVisibleItems();
        }

        protected virtual void RegisterHelpers() { }

        protected string GetFileName() => $"{Name}.xml";
        public string FileName => GetFileName();

        public abstract string Name { get; }

        public static void Init()
        {
            DataManager.Init();
            TListController instance = (TListController)DataManager.Register<TField>(new TListController());
            SettingsManager.Register<TField>(instance.Settings);
        }

        public DAOSettings<TField, TDAO> Settings { get; private set; }

        public IDAOSettings<TField> SettingsByField => SettingsManager.DAOSettings<TField>();

        public TDAO? Item(TField field, object? value) =>
            FullItemsList.Find(d => (d[field] != null) && d[field]!.Equals(value));

        private readonly FieldHelper<TField> fieldHelper;

        protected abstract FieldHelper<TField> RegisterFieldHelper();

        public FieldHelper<TField> FieldHelper => fieldHelper;

        private readonly FieldGroupHelper<TField, TFieldGroup> fieldGroupHelper;

        protected abstract FieldGroupHelper<TField, TFieldGroup> RegisterFieldGroupHelper();

        public FieldGroupHelper<TField, TFieldGroup> FieldGroupHelper => fieldGroupHelper;

        public DecoratorFactory<TField, TDAO>? decoratorFactory;

        public DecoratorFactory<TField, TDAO> DecoratorFactory
        {
            get
            {
                if (decoratorFactory == null)
                    decoratorFactory = CreateDecoratorFactory();

                return decoratorFactory;
            }
        }

        public ControlFactory<TField, TDAO>? controlFactory;

        public ControlFactory<TField, TDAO> ControlFactory
        {
            get
            {
                if (controlFactory == null)
                    controlFactory = CreateControlFactory();

                return controlFactory;
            }
        }

        public DAOEntityEventHandler? AddHandler { get; set; }
        public DAOEntityEventHandler? RemoveHandler { get; set; }
        public event ModifiedChangeHandler? ModifiedHandler;
        public EventHandler? ListChanged { get; set; }
        public EventHandler? OnAfterLoad { get; set; }

        public event EventHandler? ItemsSortChangeHandler;
        public DAOEntityEventHandler? ItemFieldChanged { get; set; } 

        public bool IsSystem => false;

        protected abstract DecoratorFactory<TField, TDAO> CreateDecoratorFactory();

        protected abstract ControlFactory<TField, TDAO> CreateControlFactory();

        public virtual FieldSortings<TField, TDAO>? DefaultSorting() => null;

        public virtual Categories<TField, TDAO>? SystemCategories => null;

        public ItemsFace<TField, TDAO> Face
        {
            get
            { 
                if (face == null)
                    face = new ItemsFace<TField, TDAO>();

                return face;
            }
        }

        public SettingsPart ActiveSettingsPart => Face.ActiveSettingsPart;

        private ItemsFace<TField, TDAO>? face;
        ISettingsController IDataController.Settings => Settings;
        OxPane? IDataController.Face => Face;

        public virtual List<ISummaryPanel>? GeneralSummaries => null;

        public ExportController<TField, TDAO>? exportController;

        public ExportController<TField, TDAO> ExportController
        {
            get
            {
                if (exportController == null)
                    exportController = new();

                return exportController;
            }
        }
    }
}