﻿using System.Drawing;
using OxLibrary;
using OxLibrary.Controls;
using OxXMLEngine.Data.Types;

namespace OxXMLEngine.Data.Fields
{
    public class FieldsFillingHelper : AbstractTypeHelper<FieldsFilling>
    {
        public override FieldsFilling EmptyValue() => FieldsFilling.Full;

        public override string GetName(FieldsFilling value) => 
            value switch
            {
                FieldsFilling.Full => "All",
                FieldsFilling.Default => "Default",
                FieldsFilling.Min => "Min",
                FieldsFilling.Clear => "Clear",
                _ => string.Empty,
            };

        public int ButtonWidth(FieldsFilling value) => 
            value switch
            {
                FieldsFilling.Full or 
                FieldsFilling.Min => 
                    50,
                _ => 
                    80,
            };

        public Bitmap ButtonIcon(FieldsFilling value) => 
            value switch
            {
                FieldsFilling.Clear => OxIcons.eraser,
                _ => OxIcons.plus,
            };
    }
}