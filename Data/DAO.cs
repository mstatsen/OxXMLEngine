﻿using OxXMLEngine.XML;
using System.Xml;

namespace OxXMLEngine.Data
{
    public abstract class DAO : IComparable
    {
        //TODO: add "event" directive
        public DAOEntityEventHandler? ChangeHandler { get; set; }

        //TODO: add "event" directive
        public ModifiedChangeHandler? ModifiedChangeHandler { get; set; }

        public DAO()
        {
            Init();
            SetMembersHandlers();
            Clear();
        }

        public bool Loading { get; internal set; } = false;

        public virtual int CompareTo(DAO? other)
        {
            string? thisString = ToString();
            string? otherString = other?.ToString();

            return thisString == null 
                ? otherString == null 
                    ? 0 
                    : -1 
                : thisString.CompareTo(otherString);
        }

        public virtual string? MatchingString() => ToString();

        protected List<DAO> Members = new();

        protected bool AutoSaveMembers = true;
        protected bool AutoLoadMembers = true;

        private bool silentChange = false;
        public bool SilentChange
        {
            get => silentChange;
            set => silentChange = value;
        }

        private bool modified = false;

        public void StartSilentChange() =>
            SilentChange = true;

        public void FinishSilentChange()
        {
            SilentChange = false;
            ModifiedChangeHandler?.Invoke(this, new DAOModifyEventArgs(Modified, null));

            if (Modified)
                NotifyAll(DAOOperation.Modify);
        }

        public bool Modified
        {
            get => modified;
            set
            {
                bool oldValue = modified;
                modified = value;

                if (!SilentChange)
                {
                    if (oldValue != modified)
                        ModifiedChangeHandler?.Invoke(this, new DAOModifyEventArgs(modified, null));

                    if (modified)
                        NotifyAll(DAOOperation.Modify);
                }
            }
        }

        public void NotifyAll(DAOOperation operation)
        {
            DAOEntityEventArgs e = new(operation);

            if (operation == DAOOperation.Remove)
                ModifiedChangeHandler?.Invoke(this, new DAOModifyEventArgs(true, this));

            ChangeHandler?.Invoke(this, e);
            modified = true;
        }

        protected virtual void InitUniqueCopy() { }

        public void CopyFrom(DAO? item, bool newUnique = false)
        {
            if (item == null)
            {
                Clear();
                return;
            }

            if (Equals(item))
                return;

            XmlDocument document = new();
            document.AppendChild(document.CreateElement("CopyData"));

            string oldXmlName = xmlName;
            string oldOtherXmlName = item.XmlName;

            xmlName = string.Empty;
            item.XmlName = string.Empty;

            try
            {
                if (document.DocumentElement != null)
                {
                    item.Save(document.DocumentElement, false);
                    Load(document.DocumentElement);

                    if (newUnique)
                        InitUniqueCopy();
                }
            }
            finally
            {
                xmlName = oldXmlName;
                item.XmlName = oldOtherXmlName;
            }

            SetMemberHandlers(item);
            Modified = true;
        }

        protected virtual void MemberModifiedHandler(DAO dao, DAOModifyEventArgs e) =>
            Modified |= e.Modified;

        protected virtual void SetMemberHandlers(DAO member, bool set = true)
        {
            if (set)
                member.ModifiedChangeHandler += MemberModifiedHandler;
            else member.ModifiedChangeHandler -= MemberModifiedHandler;
        }

        protected T? ModifyValue<T>(T? oldValue, T? newValue)
        {
            Modified |= CheckValueModified(oldValue, newValue);
            return newValue;
        }

        public static bool CheckValueModified(object? oldValue, object? newValue) =>
            oldValue == null
                ? newValue != null
                : !oldValue.Equals(newValue);

        public virtual bool IsEmpty => false;

        protected virtual void SetMembersHandlers()
        {
            foreach (DAO member in Members)
                SetMemberHandlers(member, true);
        }

        protected void AddMember(DAO member) =>
            Members.Add(member);

        protected void RemoveMember(DAO member)
        {
            member.ModifiedChangeHandler -= MemberModifiedHandler;
            Members.Remove(member);
        }

        public abstract void Init();

        public abstract void Clear();

        public virtual string DefaultXmlElementName
        {
            get
            {
                string typeName = GetType().Name;

                if (typeName.Contains('`'))
                    typeName = typeName[..typeName.IndexOf('`')];

                return typeName;
            }
        }

        private string xmlName = string.Empty;
        public string XmlName
        {
            get => xmlName;
            set => xmlName = value;
        }

        public string XmlElementName =>
            xmlName.Trim() == string.Empty
                ? DefaultXmlElementName
                : xmlName;

        protected abstract void SaveData(XmlElement element, bool clearModified = true);
        protected abstract void LoadData(XmlElement element);

        public bool WithoutXmlNode { get; protected set; } = false;

        public virtual void Save(XmlElement? parentElement, bool clearModified = true)
        {
            if (parentElement == null)
                return;

            BeforeSave();
            XmlElement? element = WithoutXmlNode ? parentElement : XmlHelper.AppendElement(parentElement, XmlElementName, string.Empty);

            if (element == null)
                return;

            SaveData(element, clearModified);
            SaveMembersData(element, clearModified);

            if (clearModified)
                Modified = false;

            AfterSave();
        }

        protected virtual void AfterSave() { }

        protected virtual void BeforeSave() { }

        private void SaveMembersData(XmlElement element, bool clearModified = true)
        {
            if (!AutoSaveMembers)
                return;

            foreach (DAO member in Members)
                if (!member.IsEmpty)
                    member.Save(element, clearModified);
        }

        private void LoadMembersData(XmlElement element)
        {
            if (!AutoLoadMembers)
                return;

            foreach (DAO member in Members)
                member.Load(element);
        }

        public void Load(XmlElement? element)
        {
            Loading = true;

            try
            {
                Clear();

                if (element == null)
                    return;

                if (!WithoutXmlNode && (element.Name != XmlElementName))
                    foreach (XmlNode node in element.ChildNodes)
                        if (node.Name == XmlElementName)
                        {
                            element = (XmlElement)node;
                            break;
                        }

                if (WithoutXmlNode || element.Name == XmlElementName)
                {
                    LoadData(element);
                    LoadMembersData(element);
                }

                Modified = false;
            }
            finally
            {
                Loading = false;
            }
        }

        public virtual string FullTitle() => XmlElementName;

        public int CompareTo(object? obj) =>
            obj is DAO dao ? CompareTo(dao) : -1;

        public virtual object ExtractKeyValue => this;

        public static int IntValue(object? value) =>
            value switch
            {
                null => 0,
                bool boolean =>
                    boolean ? 1 : 0,
                _ =>
                    int.TryParse(value.ToString(), out int intValue)
                        ? intValue
                        : 0,
            };

        public static bool BoolValue(object? value) =>
            bool.TryParse(value?.ToString(), out bool boolValue) && boolValue;

        public static T EnumValue<T>(object? value) =>
            value == null ? default! : (T)value;

        public static T? DAOValue<T>(object? value) where T : DAO =>
            value == null ? null : (T)value;

        public static string StringValue(object? value) =>
            value != null && value.ToString() != null
                ? value.ToString()!
                : string.Empty;

        public static Guid GuidValue(object? value) =>
            value != null
                ? (Guid)value
                : Guid.Empty;
    }
}
