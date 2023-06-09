﻿using OxXMLEngine.ControlFactory.Accessors;
using OxXMLEngine.ControlFactory.Controls;
using OxXMLEngine.Data;
using OxXMLEngine.Data.Fields;
using OxXMLEngine.Data.Types;
using OxXMLEngine.Settings.ControlFactory.Initializers;
using OxXMLEngine.Settings.Data;
using OxXMLEngine.SystemEngine;
using OxXMLEngine.View;

namespace OxXMLEngine.Settings.ControlFactory.Controls
{
    public partial class IconMappingEditor<TField> : ListItemEditor<IconMapping<TField>, DAOSetting, SystemRootDAO<DAOSetting>>
        where TField : notnull, Enum
    {
        private IControlAccessor ContentPartControl = default!;
        private IControlAccessor FieldControl = default!;

        public override void RenewData()
        {
            base.RenewData();

            if (ExistingItems != null)
                iconContentPartInitializer.ExistingMappings = new ListDAO<IconMapping<TField>>(ExistingItems);

            ContentPartControl.RenewControl(true);
            FieldControl.RenewControl(true);
        }

        public IconMappingEditor() => 
            InitializeComponent();

        protected override string Title => "Icon Content Part";

        private readonly IconContentPartInitializer<TField> iconContentPartInitializer = new();

        private void CreateContentPartControl()
        {
            ContentPartControl = Context.Builder.EnumAccessor<IconContent>();
            ContentPartControl.Context.SetInitializer(iconContentPartInitializer);
            ContentPartControl.Parent = this;
            ContentPartControl.Left = 64;
            ContentPartControl.Top = 12;
            ContentPartControl.Width = MainPanel.ContentContainer.Width - ContentPartControl.Left - 8;
            ContentPartControl.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            CreateLabel("Part: ", ContentPartControl);
        }

        private void CreateFieldControl()
        {
            FieldControl = Context.Builder.Accessor("IconMappingField", FieldType.Custom);
            FieldControl.Parent = this;
            FieldControl.Left = ContentPartControl!.Left;
            FieldControl.Top = ContentPartControl.Bottom + 8;
            FieldControl.Width = MainPanel.ContentContainer.Width - FieldControl.Left - 8;
            FieldControl.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            CreateLabel("Field: ", FieldControl);
        }

        protected override void CreateControls()
        {
            CreateContentPartControl();
            CreateFieldControl();
        }

        protected override void FillControls(IconMapping<TField> item)
        {
            ContentPartControl.Value = item.Part;
            FieldControl.Value = item.Field;
        }

        protected override void GrabControls(IconMapping<TField> item)
        {
            item.Part = TypeHelper.Value<IconContent>(ContentPartControl.Value);
            item.Field = FieldControl.EnumValue<TField>() ?? default!;
        }

        protected override int ContentWidth => 320;
        protected override int ContentHeight => FieldControl.Bottom + 8;

        protected override string EmptyMandatoryField() =>
            ContentPartControl.IsEmpty 
                ? "ContentPart" 
                : base.EmptyMandatoryField();
    }
}