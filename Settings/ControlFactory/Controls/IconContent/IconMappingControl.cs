﻿using OxXMLEngine.ControlFactory.Controls;
using OxXMLEngine.Data;
using OxXMLEngine.Data.Types;
using OxXMLEngine.Settings.Data;
using OxXMLEngine.SystemEngine;
using OxXMLEngine.View;
using System;

namespace OxXMLEngine.Settings.ControlFactory.Controls
{
    public class IconMappingControl<TField> : ListItemsControl<ListDAO<IconMapping<TField>>, IconMapping<TField>, 
        IconMappingEditor<TField>, DAOSetting, SystemRootDAO<DAOSetting>>
        where TField : notnull, Enum
    {
        protected override string GetText() => "Icon Mapping";

        protected override int MaximumItemsCount => 
            TypeHelper.ItemsCount<IconContent>();

        protected override bool EqualsItems(IconMapping<TField> leftItem, IconMapping<TField> rightItem) =>
            leftItem.Part == rightItem.Part;
    }
}