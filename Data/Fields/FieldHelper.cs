﻿using OxXMLEngine.ControlFactory;
using OxXMLEngine.Data.Filter;
using OxXMLEngine.Data.Types;

namespace OxXMLEngine.Data.Fields
{
    public class FieldFilterOperation<TField> : FieldDictionary<TField, FilterOperation>
        where TField : notnull, Enum
    { }

    public class FieldObject<TField> : EnumItemObject<TField>
        where TField : notnull, Enum
    {
        public override string? ToString() =>
            TypeHelper.FullName(Value);
    };

    public class FieldFilterOperations<TField> : FieldDictionary<TField, FilterOperations>
        where TField : notnull, Enum
    { }

    public abstract class FieldHelper<TField> : AbstractTypeHelper<TField>
        where TField : notnull, Enum
    {
        public override Type ItemObjectType => typeof(FieldObject<TField>); 

        public abstract FieldType GetFieldType(TField field);

        public abstract ITypeHelper? GetHelper(TField field);

        public bool IsCalcedField(TField field) =>
            CalcedFields.Contains(field);

        public bool IsGroupByField(TField field) =>
            GroupByFields.Contains(field);

        public List<TField>? AvailableFields(ControlScope scope) => 
            scope switch
            {
                ControlScope.CardView => CardFields,
                ControlScope.FullInfoView => FullInfoFields,
                ControlScope.IconView => IconFields,
                _ => FullList(TypeHelper.Helper<FieldVariantHelper>().VariantByScope(scope))
            };

        public int AvailableFieldsCount(ControlScope scope)
        {
            List<TField>? availableFields = AvailableFields(scope);
            return availableFields != null ? availableFields.Count : 0;
        }

        public bool AvailableField(ControlScope scope, TField field) => 
            scope switch
            {
                ControlScope.BatchUpdate or 
                ControlScope.QuickFilter or 
                ControlScope.Sorting or 
                ControlScope.Table or 
                ControlScope.Html or 
                ControlScope.Category or 
                ControlScope.Summary or
                ControlScope.Inline =>
                    AvailableFields(scope) != null && AvailableFields(scope)!.Contains(field),
                ControlScope.Grouping => 
                    IsGroupByField(field),
                _ => true,
            };

        public List<TField> MandatoryFields => GetMandatoryFields();
        public List<TField> CalcedFields => GetCalcedFields();
        public List<TField> IconFields => GetIconFields();
        public List<TField> EditingFields => GetEditingFields();
        public List<TField> EditingFieldsExtended => GetEditedFieldsExtended();
        public List<TField> FullInfoFields => GetFullInfoFields();
        public List<TField> CardFields => GetCardFields();
        public List<TField> GroupByFields => GetGroupByFields();
        public FieldFilterOperations<TField> AvailableFilterOperations => GetAvailableFilterOperations();

        public FilterOperation DefaultFilterOperation(TField field) => GetDefaultFilterOperation(field);

        public List<TField> SelectQuickFilterFields => GetSelectQuickFilterFields();

        public abstract TField FieldMetaData { get; }
        public abstract TField TitleField { get; }
        public abstract TField UniqueField { get; }

        public List<TField> FullList(FieldsVariant variant) => 
            GetFields(variant, FieldsFilling.Full);

        protected abstract List<TField> GetMandatoryFields();
        protected abstract List<TField> GetCalcedFields();
        protected abstract List<TField> GetIconFields();
        protected abstract List<TField> GetEditingFields();
        protected abstract List<TField> GetEditedFieldsExtended();
        protected abstract List<TField> GetFullInfoFields();
        protected abstract List<TField> GetCardFields();
        protected abstract List<TField> GetGroupByFields();
        protected abstract FieldFilterOperations<TField> GetAvailableFilterOperations();
        protected abstract FilterOperation GetDefaultFilterOperation(TField field);
        protected abstract List<TField> GetSelectQuickFilterFields();

        public virtual List<TField> Depended(TField field) => new();

        public List<TField>? GetFields(FieldsVariant variant) =>
            GetFields(variant, FieldsFilling.Full);

        public List<TField> GetFields(FieldsVariant variant, FieldsFilling filling) 
        {
            List<TField>? result = GetFieldsInternal(variant, filling);

            if (result == null || result.Count == 0)
                result = GetFieldsInternal(FieldsVariant.Table, filling);

            if (result == null)
                result = new();

            return result;
        }

        public abstract List<TField>? GetFieldsInternal(FieldsVariant variant, FieldsFilling filling);


        public int FullListCount(FieldsVariant variant)
        {
            List<TField>? fields = GetFieldsInternal(variant, FieldsFilling.Full);
            return fields != null ? fields.Count : 0;
        }

        public FieldColumns<TField> Columns(FieldsVariant variant, FieldsFilling filling)
        {
            FieldColumns<TField> columns = new ();
            columns.AddRange(GetFields(variant, filling));
            return columns;
        }

        public virtual DataGridViewContentAlignment ColumnAlign(TField field) =>
            DataGridViewContentAlignment.MiddleCenter;

        public DataGridViewCellStyle ColumnStyle(TField field) =>
            ColumnAlign(field) == DataGridViewContentAlignment.MiddleLeft
                ? EngineStyles.Cell_LeftAlignment
                : EngineStyles.Cell_Default;

        public virtual int ColumnWidth(TField field) => 100;

        public virtual string ColumnCaption(TField field) => Name(field);
    }
}