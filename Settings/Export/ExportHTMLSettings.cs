﻿using OxXMLEngine.Data;
using OxXMLEngine.Data.Fields;
using OxXMLEngine.Data.Sorting;
using OxXMLEngine.Data.Types;
using OxXMLEngine.XML;
using System.Xml;

namespace OxXMLEngine.Settings
{
    public class ExportHTMLSettings<TField, TDAO> : AbstractTextExportSettings<TField>
        where TField : notnull, Enum
        where TDAO : RootDAO<TField>, new()
    {
        public FieldSortings<TField, TDAO> Sorting { get; internal set; } = new()
        {
            XmlName = "HtmlSorting"
        };
        public bool ZeroSummary { get; set; }

        public override void Clear()
        {
            base.Clear();
            Sorting.Clear();
            Fields.CopyFrom(TypeHelper.FieldHelper<TField>()
                .Columns(FieldsVariant.Html, FieldsFilling.Default));
            ZeroSummary = true;
        }

        public override void Init()
        {
            base.Init();
            ZeroSummary = true;
        }

        protected override void LoadData(XmlElement element)
        {
            base.LoadData(element);
            Sorting.Load(element);
            ZeroSummary = XmlHelper.ValueBool(element, XmlConsts.ZeroSummary);
        }

        protected override void SaveData(XmlElement element, bool clearModified = true)
        {
            base.SaveData(element, clearModified);
            Sorting.Save(element, clearModified);
            XmlHelper.AppendElement(element, XmlConsts.ZeroSummary, ZeroSummary);
        }
    }
}