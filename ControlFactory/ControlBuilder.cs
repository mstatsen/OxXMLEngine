﻿using OxLibrary.Controls;
using OxXMLEngine.ControlFactory.Accessors;
using OxXMLEngine.ControlFactory.Context;
using OxXMLEngine.Data;
using OxXMLEngine.Data.Decorator;
using OxXMLEngine.Data.Fields;
using OxXMLEngine.Data.Filter;
using OxXMLEngine.Data.Types;

namespace OxXMLEngine.ControlFactory
{
    public class ControlBuilder<TField, TDAO> 
        where TField : notnull, Enum
        where TDAO : RootDAO<TField>, new()
    {
        public ControlScope Scope { get; set; }
        public bool Modified { get; internal set; }

        private readonly Dictionary<IBuilderContext<TField, TDAO>, IControlAccessor> Accessors = new();

        private readonly Dictionary<TField, FieldContext<TField, TDAO>> FieldContexts = new();

        private readonly Dictionary<string, IBuilderContext<TField, TDAO>> SimpleContexts = new();

        private void ModifiedHandler(object? sender, EventArgs e) =>
            Modified = true;

        public ControlBuilder(ControlFactory<TField, TDAO> controlFactory, ControlScope scope)
        {
            Scope = scope;
            Factory = controlFactory;
            Layouter = new ControlLayouter<TField, TDAO>(this);
        }

        internal void DisposeControls()
        {
            foreach (IControlAccessor accessor in Accessors.Values)
                accessor.Control.Dispose();

            Accessors.Clear();
        }

        private IControlAccessor Accessor(IBuilderContext<TField, TDAO> context, 
            Func<IBuilderContext<TField, TDAO>, IControlAccessor>? createFunction = null)
        {
            if (!Accessors.TryGetValue(context, out var controlAccessor))
            {
                controlAccessor = 
                    createFunction != null 
                        ? createFunction(context) 
                        : Factory.CreateAccessor(context);

                if (controlAccessor == null)
                    throw new KeyNotFoundException($"Control Accessor not exist {context}");

                Accessors.Add(context, controlAccessor);
            }

            return controlAccessor;
        }


        public IControlAccessor Accessor(TField field) =>
            Accessor(Context(field));

        public IControlAccessor Accessor(string name, FieldType fieldType, object? additionalContext = null) =>
            Accessor(Context(name, fieldType, additionalContext));

        public EnumAccessor<TField, TDAO, TItem> EnumAccessor<TItem>(object? additionalContext = null)
            where TItem : Enum => 
            (EnumAccessor<TField, TDAO, TItem>)
                Accessor(
                    Context(typeof(TItem).Name, FieldType.Enum, additionalContext),
                    (c) => Factory.CreateEnumAccessor<TItem>(c)
                );

        public IControlAccessor FieldListAccessor(object? additionalContext = null) =>
            Accessor(
                Context("FieldListAccessor", FieldType.Custom, additionalContext),
                c => Factory.CreateFieldListAccessor(c)
            );

        public IControlAccessor SortingListAccessor(object? additionalContext = null) =>
            Accessor(
                Context("SortingListAccessor", FieldType.Custom, additionalContext),
                c => Factory.CreateSortingListAccessor(c)
            );


        public FieldContext<TField, TDAO> Context(TField field)
        {
            if (!FieldContexts.TryGetValue(field, out var context))
            {
                context = new FieldContext<TField, TDAO>(this, field)
                {
                    AvailableDependencies = !BuildOnly
                };
                FieldContexts.Add(field, context);
            }
            
            return context;
        }

        public IBuilderContext<TField, TDAO> Context(string name, FieldType fieldType, object? additionalContext)
        {
            string key = $"{name}_{fieldType}";

            if (additionalContext != null)
                key += $"_{additionalContext}";

            if (!SimpleContexts.TryGetValue(key, out var context))
            {
                context = new AccessorContext<TField, TDAO>(this, name, fieldType)
                {
                    AdditionalContext = additionalContext
                };
                SimpleContexts.Add(key, context);
            }

            return context;
        }


        public void FillControls(TDAO item)
        {
            if (BuildOnly)
                return;

            FieldHelper<TField> fieldHelper = TypeHelper.FieldHelper<TField>();
            List<TField>? availableFields = fieldHelper.AvailableFields(Scope);
            DecoratorFactory<TField, TDAO> decoratorFactory = DataManager.DecoratorFactory<TField, TDAO>();
            Decorator<TField, TDAO> decorator = decoratorFactory.Decorator(Scope, item);
            Decorator<TField, TDAO> simpleDecorator = decoratorFactory.Decorator(DecoratorType.Simple, item);
            Decorator<TField, TDAO>? currentDecorator = null;
            object? value = null;

            foreach (TField field in TypeHelper.All<TField>())
            {
                IControlAccessor? accessor = null;
                currentDecorator = null;

                if (item == null)
                {
                    accessor = this[field];
                    value = null;
                }
                else
                {
                    switch (Scope)
                    {
                        case ControlScope.FullInfoView:
                        case ControlScope.IconView:
                        case ControlScope.CardView:
                            if ((availableFields != null) && availableFields.Contains(field))
                                currentDecorator = decorator;
                            break;
                        default:
                            if (fieldHelper.CalcedFields.Contains(field))
                                currentDecorator = decorator;

                            if (fieldHelper.EditingFieldsExtended.Contains(field))
                                currentDecorator = simpleDecorator;
                            else currentDecorator = null;

                            break;
                    }
                }

                if (currentDecorator != null)
                {
                    value = currentDecorator[field];
                    accessor = this[field];
                }

                if (accessor == null)
                    continue;

                accessor.ValueChangeHandler -= ModifiedHandler;
                accessor.RenewControl();

                if (accessor is IDependedControl dependedAccessor)
                    dependedAccessor.ApplyDependencies();

                accessor.Value = value;
                accessor.ValueChangeHandler += ModifiedHandler;
            }

            Modified = false;
        }

        public void ApplyDependencies()
        {
            if (BuildOnly)
                return;

            ControlScopeHelper scopeHelper = TypeHelper.Helper<ControlScopeHelper>();

            if (scopeHelper.IsView(Scope))
                return;

            FieldHelper<TField> fieldHelper = TypeHelper.FieldHelper<TField>();

            foreach (TField field in TypeHelper.All<TField>())
            {
                if (!fieldHelper.CalcedFields.Contains(field) &&
                    !fieldHelper.EditingFieldsExtended.Contains(field))
                    continue;

                if (this[field] is IDependedControl dependedAccessor)
                    dependedAccessor.ApplyDependencies();
            }
        }

        private static bool CheckFilter(FilterRules<TField>? rules, TField field) =>
            rules == null || rules.RuleExist(field);

        private void GrabEditorControls(IFieldMapping<TField> item, FilterRules<TField>? rules)
        {
            foreach (TField field in TypeHelper.FieldHelper<TField>().EditingFields)
            {
                if (!ControlBuilder<TField, TDAO>.CheckFilter(rules, field))
                    continue;

                item[field] = Accessor(field).Value;
            }
        }

        public void GrabControls(IFieldMapping<TField> item, FilterRules<TField>? rules = null)
        {
            if (BuildOnly)
                return;

            switch (Scope)
            {
                case ControlScope.Editor:
                    GrabEditorControls(item, rules);
                    break;
                case ControlScope.QuickFilter:
                    if (item is SimpleFilter<TField, TDAO> filter)
                        GrabQuickFilterControls(filter, rules);
                    break;
            }
        }

        public void GrabQuickFilterControls(SimpleFilter<TField, TDAO> quickFilter, FilterRules<TField>? rules)
        {
            foreach (TField field in TypeHelper.All<TField>())
                if (rules == null || rules.RuleExist(field))
                    quickFilter.AddFilter(
                        field,
                        Value(field)
                    );
        }

        public IControlAccessor this[TField field] => Accessor(field);

        public T? Value<T>(TField field) => 
            (T?)(this[field].Value == null || 
                    (this[field].Value is not T)
                ? default(T)
                : this[field].Value);

        public object? Value(TField field) => 
            this[field].Value;

        public void SetVisible(TField field, bool visible)
        {
            this[field].Visible = visible;
            OxLabel? label = Layouter.PlacedControl(field)?.Label;

            if (label != null)
                label.Visible = visible;
        }

        public T Control<T>(TField field) where T : Control => 
            (T)this[field].Control;

        public Control Control(TField field) => 
            this[field].Control;

        public void ClearValueConstraints(TField field) => 
            this[field].ClearValueConstraints();

        public ControlFactory<TField, TDAO> Factory { get; set; }

        public void DetachControlsFromParent()
        {
            foreach (IControlAccessor accessor in Accessors.Values)
                accessor.Parent = null;
        }

        public readonly ControlLayouter<TField, TDAO> Layouter;
        public bool BuildOnly = false;
    }
}