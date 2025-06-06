﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Seal Report Dual-License version 1.0; you may not use this file except in compliance with the License described at https://github.com/ariacom/Seal-Report.
//
using System;
using System.Drawing.Design;
using System.ComponentModel;
using System.Windows.Forms;
using Seal.Model;
using System.ComponentModel.Design;
using System.Reflection;
using System.Drawing;
using System.Linq;
using Seal.Renderer;
using Seal.Helpers;

namespace Seal.Forms
{

    public class EntityCollectionEditor : CollectionEditor
    {
        // Define a static event to expose the inner PropertyGrid's
        // PropertyValueChanged event args...
        public delegate void ViewParameterPropertyValueChangedEventHandler(object sender, PropertyValueChangedEventArgs e);
        public static event ViewParameterPropertyValueChangedEventHandler MyPropertyValueChanged;

        RootComponent _component = null;
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            if (context.Instance is RootComponent) _component = (RootComponent)context.Instance;

            if (context.PropertyDescriptor.IsReadOnly) return UITypeEditorEditStyle.None;
            return UITypeEditorEditStyle.Modal;
        }

        void SetModified()
        {
            if (HelperEditor.HandlerInterface != null && _useHandlerInterface) HelperEditor.HandlerInterface.SetModified();
        }

        // Inherit the default constructor from the standard
        // Collection Editor...
        public EntityCollectionEditor(Type type) : base(type) { }
        bool _useHandlerInterface = true;

        // Override this method in order to access the containing user controls
        // from the default Collection Editor form or to add new ones...
        protected override CollectionForm CreateCollectionForm()
        {
            // Getting the default layout of the Collection Editor...
            CollectionForm collectionForm = base.CreateCollectionForm();

            bool allowAdd = false;
            bool allowRemove = false;
            Form frmCollectionEditorForm = collectionForm as Form;
            frmCollectionEditorForm.HelpButton = false;
            frmCollectionEditorForm.Text = "Collection Editor";
            if (CollectionItemType == typeof(ReportRestriction))
            {
                frmCollectionEditorForm.Text = "Restriction Collection Editor";
                if (Context.Instance is ReportModel)
                {
                    var model = Context.Instance as ReportModel;
                    model.InitCommonRestrictions();
                }
                else if (Context.Instance is Report)
                {
                    allowAdd = true;
                    allowRemove = true;
                    frmCollectionEditorForm.Text = "Report Input Value Collection Editor";
                }
            }
            else if (CollectionItemType == typeof(OutputParameter))
            {
                frmCollectionEditorForm.Text = "Custom Output Parameters Collection Editor";
            }
            else if (CollectionItemType == typeof(SecurityParameter))
            {
                frmCollectionEditorForm.Text = "Security Parameter Collection Editor";
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(Parameter))
            {
                frmCollectionEditorForm.Text = "Template Parameter Collection Editor";
            }
            else if (CollectionItemType == typeof(SecurityGroup))
            {
                frmCollectionEditorForm.Text = "Security Group Collection Editor";
                allowAdd = true;
                allowRemove = true;
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(SecurityLogin))
            {
                frmCollectionEditorForm.Text = "Security Login Collection Editor";
                allowAdd = true;
                allowRemove = true;
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(SecurityFolder))
            {
                frmCollectionEditorForm.Text = "Security Folder Collection Editor";
                allowAdd = true;
                allowRemove = true;
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(SecurityColumn))
            {
                frmCollectionEditorForm.Text = "Security Column Collection Editor";
                allowAdd = true;
                allowRemove = true;
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(SecuritySource))
            {
                frmCollectionEditorForm.Text = "Security Data Source Collection Editor";
                allowAdd = true;
                allowRemove = true;
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(SecurityDevice))
            {
                frmCollectionEditorForm.Text = "Security Device Collection Editor";
                allowAdd = true;
                allowRemove = true;
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(SecurityConnection))
            {
                frmCollectionEditorForm.Text = "Security Connection Collection Editor";
                allowAdd = true;
                allowRemove = true;
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(SubReport))
            {
                frmCollectionEditorForm.Text = "Sub-Report Collection Editor";
                allowRemove = true;
                _useHandlerInterface = true;
            }
            else if (CollectionItemType == typeof(ReportViewPartialTemplate))
            {
                frmCollectionEditorForm.Text = "Partial Template Collection Editor";
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(SealServerConfiguration.FileReplacePattern))
            {
                frmCollectionEditorForm.Text = "File Pattern Collection Editor";
                allowAdd = true;
                allowRemove = true;
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(SealServerConfiguration.KeyValue))
            {
                frmCollectionEditorForm.Text = "Key Value Collection Editor";
                allowAdd = true;
                allowRemove = true;
                _useHandlerInterface = false;
            }
            else if (CollectionItemType == typeof(MetaEV))
            {
                frmCollectionEditorForm.Text = "Enum Value Collection Editor";
                allowAdd = true;
                allowRemove = true;
                //Set reference
                var metaEnum = _component as MetaEnum;
                foreach (var ev in metaEnum.Values) ev.MetaEnum = metaEnum;
            }

            TableLayoutPanel tlpLayout = frmCollectionEditorForm.Controls[0] as TableLayoutPanel;

            if (tlpLayout != null)
            {
                // Get a reference to the inner PropertyGrid and hook
                // an event handler to it.
                if (tlpLayout.Controls[5] is PropertyGrid)
                {
                    PropertyGrid propertyGrid = tlpLayout.Controls[5] as PropertyGrid;
                    propertyGrid.HelpVisible = true;
                    propertyGrid.ToolbarVisible = false;
                    propertyGrid.PropertyValueChanged += new PropertyValueChangedEventHandler(propertyGrid_PropertyValueChanged);
                    propertyGrid.LineColor = SystemColors.ControlLight;
                    propertyGrid.Tag = _component;
                }
            }

            //Hide Add/Remove -> Get the forms type
            if (!allowRemove)
            {
                Type formType = frmCollectionEditorForm.GetType();
                FieldInfo fieldInfo = formType.GetField("removeButton", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    Control removeButton = (Control)fieldInfo.GetValue(frmCollectionEditorForm);
                    removeButton.Hide();
                }
            }

            if (!allowAdd)
            {
                Type formType = frmCollectionEditorForm.GetType();
                FieldInfo fieldInfo = formType.GetField("addButton", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    Control addButton = (Control)fieldInfo.GetValue(frmCollectionEditorForm);
                    addButton.Hide();
                }
            }
            return collectionForm;
        }



        void propertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e)
        {
            // Fire our customized collection event...
            if (MyPropertyValueChanged != null)
            {
                MyPropertyValueChanged(this, e);
            }
            SetModified();
        }


        protected override object CreateInstance(Type itemType)
        {

            object instance = Activator.CreateInstance(itemType, true);
            SetModified();
            if (_component != null) _component.UpdateEditor();

            if (instance is ReportRestriction && Context.Instance is Report)
            {
                var result = ReportRestriction.CreateReportRestriction();
                result.TypeRe = ColumnType.Text;
                result.Operator = Operator.Equal;
                result.Report = (Report)Context.Instance;
                instance = result;
            }
            else if (instance is MetaEV)
            {
                ((MetaEV) instance).MetaEnum = _component as MetaEnum;
            }
            else if (instance is SecurityGroup)
            {
                ((SecurityGroup)instance).GUID = Helper.NewGUID();
            }
            else if (instance is SecurityLogin)
            {
                ((SecurityLogin)instance).GUID = Helper.NewGUID();
            }
            return instance;
        }

        protected override void DestroyInstance(object instance)
        {
            base.DestroyInstance(instance);
            SetModified();
            if (_component != null) _component.UpdateEditor();
            if (instance is SecurityGroup)
            {
                Repository.Instance.Security.InitSecurity();
            }
        }

        protected override string GetDisplayText(object value)
        {
            string result = "";
            if (value is RootEditor) ((RootEditor)value).InitEditor();
            if (value is ReportRestriction)
            {
                var restr = value as ReportRestriction;
                if (restr.MetaColumn == null && string.IsNullOrEmpty(restr.Name)) result = restr.DisplayNameEl; //Report input value
                else if (restr.MetaColumn == null) result = restr.Name; //Common restriction
                else if (restr.Model != null) result = string.Format("{0} ({1})", restr.DisplayNameEl, restr.Model.Name);
            }
            else if (value is Parameter) result = ((Parameter)value).DisplayName;
            else if (value is SecurityGroup) result = ((SecurityGroup)value).Name;
            else if (value is SecurityLogin) result = ((SecurityLogin)value).Id;
            else if (value is SecurityFolder) result = ((SecurityFolder)value).Path;
            else if (value is SecurityColumn) result = ((SecurityColumn)value).DisplayName;
            else if (value is SecuritySource) result = ((SecuritySource)value).DisplayName;
            else if (value is SecurityDevice) result = ((SecurityDevice)value).DisplayName;
            else if (value is SecurityConnection) result = ((SecurityConnection)value).DisplayName;
            else if (value is SubReport) result = ((SubReport)value).Name;
            else if (value is ReportComponent) result = ((ReportComponent)value).Name;
            else if (value is SealServerConfiguration.FileReplacePattern) result = ((SealServerConfiguration.FileReplacePattern)value).ToString();
            else if (value is MetaEV)
            {
                var item = value as MetaEV;
                return base.GetDisplayText(string.IsNullOrEmpty(item.Id) ? "<Empty value>" : string.Format("{0}", item.DisplayValue));
            }
            return base.GetDisplayText(string.IsNullOrEmpty(result) ? "<Empty Name>" : result);
        }
    }
}
