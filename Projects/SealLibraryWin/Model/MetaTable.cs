﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Seal Report Dual-License version 1.0; you may not use this file except in compliance with the License described at https://github.com/ariacom/Seal-Report.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Data;
using System.Xml.Serialization;
using Seal.Helpers;
using RazorEngine.Templating;
using System.Globalization;
using System.Data.Common;
using System.Text.RegularExpressions;
using MongoDB.Bson;
#if WINDOWS
using System.Drawing.Design;
using Seal.Forms;
using DynamicTypeDescriptor;
#endif

namespace Seal.Model
{
    /// <summary>
    /// A MetaTable defines a table in a database and contains a list of MetaColumns.
    /// </summary>
    public class MetaTable : RootComponent, ReportExecutionLog
    {
        public const string ParameterNameMongoSync = "mongo_sync";
        public const string ParameterNameMongoDatabase = "mongo_database";
        public const string ParameterNameMongoCollection = "mongo_collection";
        public const string ParameterNameMongoArrayName = "mongo_array_name";
        public const string ParameterNameMongoRestrictionOperator = "mongo_restriction_operator";

#if WINDOWS
        #region Editor

        protected override void UpdateEditorAttributes()
        {
            if (_dctd != null)
            {
                //Disable all properties
                foreach (var property in Properties) property.SetIsBrowsable(false);
                //Then enable
                GetProperty("Name").SetIsBrowsable(!IsSubTable);
                GetProperty("Type").SetIsBrowsable(IsSQL);
                GetProperty("Alias").SetIsBrowsable(IsSQL);
                GetProperty("DynamicColumns").SetIsBrowsable(IsSQL);
                GetProperty("KeepColumnNames").SetIsBrowsable(IsSQL);
                GetProperty("PreSQL").SetIsBrowsable(IsSQL);
                GetProperty("Sql").SetIsBrowsable(IsSQL);
                GetProperty("TemplateName").SetIsBrowsable(!IsSQL);
                GetProperty("ParameterValues").SetIsBrowsable(!IsSQL && Parameters.Count > 0);
                GetProperty("DefinitionInitScript").SetIsBrowsable(!IsSQL);
                GetProperty("DefinitionScript").SetIsBrowsable(!IsSQL);
                GetProperty("MongoStagesScript").SetIsBrowsable(IsMongoDb);
                GetProperty("LoadScript").SetIsBrowsable(!IsSQL);
                GetProperty("Functions").SetIsBrowsable(hasFunctions());
                GetProperty("CacheDuration").SetIsBrowsable(!IsSQL);
                GetProperty("PostSQL").SetIsBrowsable(IsSQL);
                GetProperty("IgnorePrePostError").SetIsBrowsable(IsSQL);
                GetProperty("WhereSQL").SetIsBrowsable(IsSQL);

                GetProperty("HelperRefreshColumns").SetIsBrowsable(!IsSubTable);
                GetProperty("HelperCheckTable").SetIsBrowsable(true);
                GetProperty("Information").SetIsBrowsable(true);
                GetProperty("Error").SetIsBrowsable(true);

                if (IsSubTable) GetProperty("LoadScript").SetDisplayName("Load Script");

                //Readonly
                GetProperty("TemplateName").SetIsReadOnly(true);
                GetProperty("Type").SetIsReadOnly(true);

                GetProperty("HelperRefreshColumns").SetIsReadOnly(true);
                GetProperty("HelperCheckTable").SetIsReadOnly(true);
                GetProperty("Information").SetIsReadOnly(true);
                GetProperty("Error").SetIsReadOnly(true);

                TypeDescriptor.Refresh(this);
            }
        }
        #endregion

#endif
        /// <summary>
        /// Creates a basic MetaTable
        /// </summary>
        public static MetaTable Create()
        {
            return new MetaTable() { GUID = Guid.NewGuid().ToString() };
        }

        /// <summary>
        /// Current execution log
        /// </summary>
        [XmlIgnore]
        public ReportExecutionLog Log = null;

        /// <summary>
        /// Logs message in the execution log
        /// </summary>
        public void LogMessage(string message, params object[] args)
        {
            if (Log != null) Log.LogMessage(message, args);
        }

        /// <summary>
        /// Name of the table in the database. The name can be empty if an SQL Statement is specified.
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("Name"), Description("Name of the table in the database or for the No Sql source. The name can be empty if an SQL Statement is specified."), Id(1, 1)]
#endif
        public override string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        /// <summary>
        /// SQL Select Statement executed to define the table. If empty, the table name is used.
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("SQL Select Statement"), Description("SQL Select Statement executed to define the table. If empty, the table name is used."), Id(2, 1)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
#endif
        public string Sql { get; set; }

        /// <summary>
        /// DataTable used for No SQL Source 
        /// </summary>
        [XmlIgnore]
        public DataTable NoSQLTable = null;

        /// <summary>
        /// ReportModel set for No SQL Source
        /// </summary>
        [XmlIgnore]
        public ReportModel NoSQLModel = null;

        /// <summary>
        /// Pipeline stages for Mongo queries
        /// </summary>
        [XmlIgnore]
        public List<BsonDocument> MongoStages = new List<BsonDocument>();


        /// <summary>
        /// The Razor Script used to built the DataTable object that defines the table
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("Template"), Description("The Template used to define the NoSQL table."), Id(1, 1)]
#endif
        public string TemplateName { get; set; }

        private MetaTableTemplate _tableTemplate = null;
        [XmlIgnore]
        public MetaTableTemplate TableTemplate
        {
            get
            {
                if (IsSQL) return null;
                if (_tableTemplate == null)
                {
                    if (!string.IsNullOrEmpty(TemplateName)) _tableTemplate = RepositoryServer.TableTemplates.FirstOrDefault(i => i.Name == TemplateName);
                    if (_tableTemplate == null) _tableTemplate = RepositoryServer.TableTemplates.FirstOrDefault(i => i.Name == MetaTableTemplate.DefaultName);

                    InitParameters();
                }
                return _tableTemplate;
            }
        }

        /// <summary>
        /// List of Table Parameters
        /// </summary>
        public List<Parameter> Parameters { get; set; } = new List<Parameter>();
        public bool ShouldSerializeParameters() { return Parameters.Count > 0; }

        /// <summary>
        /// Init the  parameters from the template
        /// </summary>
        public void InitParameters()
        {
            if (TableTemplate != null)
            {
                var initialParameters = Parameters.ToList();
                Parameters.Clear();

                var defaultParameters = IsSubTable && RootTable != null ? RootTable.Parameters : TableTemplate.DefaultParameters;
                foreach (var configParameter in defaultParameters)
                {
                    Parameter parameter = initialParameters.FirstOrDefault(i => i.Name == configParameter.Name);
                    if (parameter == null) parameter = new Parameter() { Name = configParameter.Name, Value = configParameter.Value };
                    Parameters.Add(parameter);
                    parameter.InitFromConfiguration(configParameter);
                }
                //Show Error if any
                if (!string.IsNullOrEmpty(TableTemplate.Error)) Error = TableTemplate.Error;

                if (string.IsNullOrEmpty(_name)) _name = "Master"; //Force a name for backward compatibility
            }
        }

        /// <summary>
        /// Helper to check if the 2 MetaTable have the same definition
        /// </summary>
        public bool IsIdentical(MetaTable table)
        {
            bool result =
                TemplateName == table.TemplateName &&
                Helper.CompareTrim(DefinitionInitScript, table.DefinitionInitScript) &&
                Helper.CompareTrim(DefinitionScript, table.DefinitionScript) &&
                Helper.CompareTrim(MongoStagesScript, table.MongoStagesScript) &&
                Helper.CompareTrim(LoadScript, table.LoadScript)
             ;

            if (result)
            {
                foreach (var parameter in Parameters)
                {
                    if (parameter.Value != table.GetValue(parameter.Name))
                    {
                        result = false;
                        break;
                    }
                }
            }

            return result;
        }


        MetaTable _rootTable = null;
        [XmlIgnore]
        MetaTable RootTable
        {
            get
            {
                if (_rootTable == null && IsSubTable)
                {
                    _rootTable = Source.MetaData.Tables.FirstOrDefault(i => i.GUID == GUID);
                }
                return _rootTable;
            }
        }

        /// <summary>
        /// Default definition init script coming either from the template or from the root table (for a subtable)
        /// </summary>
        [XmlIgnore]
        public string DefaultDefinitionInitScript
        {
            get
            {
                string result = null;
                if (IsSubTable && RootTable != null)
                {
                    result = string.IsNullOrEmpty(RootTable.DefinitionInitScript) ? RootTable.DefaultDefinitionInitScript : RootTable.DefinitionInitScript;
                }
                else if (TableTemplate != null && TemplateName != null) result = TableTemplate.DefaultDefinitionInitScript;
                return result ?? "";
            }
        }

        /// <summary>
        /// Default definition script coming either from the template or from the root table (for a subtable)
        /// </summary>
        [XmlIgnore]
        public string DefaultDefinitionScript
        {
            get
            {
                string result = null;
                if (IsSubTable && RootTable != null)
                {
                    result = string.IsNullOrEmpty(RootTable.DefinitionScript) ? RootTable.DefaultDefinitionScript : RootTable.DefinitionScript;
                }
                else if (TableTemplate != null && TemplateName != null) result = TableTemplate.DefaultDefinitionScript;
                return result ?? "";
            }
        }

        /// <summary>
        /// Default load script coming either from the template or from the root table (for a subtable)
        /// </summary>
        [XmlIgnore]
        public string DefaultLoadScript
        {
            get
            {
                string result = null;
                if (IsSubTable && RootTable != null)
                {
                    result = string.IsNullOrEmpty(RootTable.LoadScript) ? RootTable.DefaultLoadScript : RootTable.LoadScript;
                }
                else if (TableTemplate != null && TemplateName != null)
                {
                    result = TableTemplate.DefaultLoadScript;
                }
                return result ?? "";
            }
        }

        //Temporary variables to help for report serialization...
        private List<Parameter> _tempParameters;

        /// <summary>
        /// Operations performed before the serialization
        /// </summary>
        public void BeforeSerialization()
        {
            InitParameters();
            _tempParameters = Parameters.ToList();
            //Remove parameters identical to config
            Parameters.RemoveAll(i => i.Value == null || i.Value == i.ConfigValue);

            if (DefinitionScript != null && DefinitionScript.Trim().Replace("\r\n", "\n") == DefaultDefinitionScript.Trim().Replace("\r\n", "\n")) DefinitionScript = null;
            if (LoadScript != null && LoadScript.Trim().Replace("\r\n", "\n") == DefaultLoadScript.Trim().Replace("\r\n", "\n")) LoadScript = null;

            if (!IsSQL) Alias = "";
        }

        /// <summary>
        /// Operations performed after the serialization
        /// </summary>
        public void AfterSerialization()
        {
            Parameters = _tempParameters;
        }

        /// <summary>
        /// Returns the parameter value
        /// </summary>
        public string GetValue(string name)
        {
            Parameter parameter = Parameters.FirstOrDefault(i => i.Name == name);
            return parameter == null ? "" : (string.IsNullOrEmpty(parameter.Value) ? parameter.ConfigValue : parameter.Value);
        }

        /// <summary>
        /// Returns a parameter boolean value with a default if it does not exist
        /// </summary>
        public bool GetBoolValue(string name, bool defaultValue)
        {
            Parameter parameter = Parameters.FirstOrDefault(i => i.Name == name);
            return parameter == null ? defaultValue : parameter.BoolValue;
        }

        /// <summary>
        /// Returns a parameter integer value
        /// </summary>
        public int GetNumericValue(string name)
        {
            Parameter parameter = Parameters.FirstOrDefault(i => i.Name == name);
            return parameter == null ? 0 : parameter.NumericValue;
        }

        /// <summary>
        /// Returns a parameter double value
        /// </summary>
        public double GetDoubleValue(string name)
        {
            Parameter parameter = Parameters.FirstOrDefault(i => i.Name == name);
            return parameter == null ? 0 : parameter.DoubleValue;
        }

        /// <summary>
        /// Optional Razor Script executed before the execution of the DefinitionScript
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("Definition Init Script"), Description("Optional Razor Script executed before the execution of the DefinitionScript."), Id(1, 1)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
#endif
        public string DefinitionInitScript { get; set; }

        /// <summary>
        /// The Razor Script used to built the DataTable object that defines the table
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("Definition Script"), Description("The Razor Script used to built the DataTable object that defines the table."), Id(2, 1)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
#endif
        public string DefinitionScript { get; set; }

        /// <summary>
        /// Razor Script executed for Mongo DB table to add stages executed on the server before the load. This script is automatically generated from the model definition. It can be overwritten if the 'Generate Mongo DB stages' parameter of the table is set to false. Use the 'Refresh Sub-Models and Sub-Tables' button in the model to generate and view the script. 
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("Mongo DB Stages Script"), Description("Razor Script executed for Mongo DB table to add stages executed on the server before the load. This script is automatically generated from the model definition. It can be overwritten if the 'Generate Mongo DB stages' parameter of the table is set to false.  Use the 'Refresh Sub-Models and Sub-Tables' button in the model to generate and view the script."), Id(4, 1)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
#endif
        public string MongoStagesScript { get; set; }

        /// <summary>
        /// The Default Razor Script used to load the data in the table. This can be overwritten in the model.
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("Default Load Script"), Description("The Default Razor Script used to load the data in the table. This can be overwritten in the model. If the definition script includes also the load of the data, this script can be left empty/blank."), Id(5, 1)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
#endif
        public string LoadScript { get; set; }

        bool hasFunctions()
        {
            return !string.IsNullOrEmpty(LoadScript) && LoadScript.Contains("@functions");
        }

#if WINDOWS
        /// <summary>
        /// The functions got for edition.
        /// </summary>
        [TypeConverter(typeof(ExpandableObjectConverter))]
        [DisplayName("Script functions"), Description("The task functions got from the main script."), Category("Definition"), Id(5, 1)]
        [XmlIgnore]
        public FunctionsEditor Functions
        {
            get
            {
                var editor = new FunctionsEditor();
                editor.Init(LoadScript, this);
                return editor;
            }
        }
#endif

        /// <summary>
        /// Duration in seconds to keep the result DataTable in cache after a load. If 0, the table is always reloaded.
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("Cache duration"), Description("Duration in seconds to keep the result DataTable in cache after a load. If 0, the table is always reloaded."), Id(5, 1)]
        [DefaultValue(0)]
#endif
        public int CacheDuration { get; set; } = 0;
        public bool ShouldSerializeCacheDuration() { return CacheDuration != 0; }


        /// <summary>
        /// If not empty, table alias name used in the SQL statement. The table alias is necessary if a SQL Statement is specified.
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("Table alias"), Description("If not empty, table alias name used in the SQL statement. The table alias is necessary if a SQL Statement is specified."), Id(5, 1)]
#endif
        public string Alias { get; set; }

        bool _dynamicColumns = false;
        /// <summary>
        /// If true, columns are generated automatically from the Table Name or the SQL Select Statement by reading the database catalog
        /// </summary>
#if WINDOWS
        [DefaultValue(false)]
        [Category("Definition"), DisplayName("Dynamic columns"), Description("If true, columns are generated automatically from the Table Name or the SQL Select Statement by reading the database catalog."), Id(6, 1)]
#endif
        public bool DynamicColumns
        {
            get { return _dynamicColumns; }
            set
            {
                _dynamicColumns = value;
                UpdateEditorAttributes();
            }
        }
        public bool ShouldSerializeDynamicColumns() { return _dynamicColumns; }

        /// <summary>
        /// "If true, the display names of the columns are kept when generated from the source SQL
        /// </summary>
#if WINDOWS
        [DefaultValue(false)]
        [Category("Definition"), DisplayName("Keep column names"), Description("If true, the display names of the columns are kept when generated from the source SQL."), Id(7, 1)]
#endif
        public bool KeepColumnNames { get; set; } = false;
        public bool ShouldSerializeKeepColumnNames() { return KeepColumnNames; }

        /// <summary>
        /// Type of the table got from database catalog
        /// </summary>
#if WINDOWS
        [Category("Definition"), DisplayName("Table type"), Description("Type of the table got from database catalog."), Id(8, 1)]
#endif
        public string Type { get; set; }

#if WINDOWS
        /// <summary>
        /// The parameter values for edition.
        /// </summary>
        [TypeConverter(typeof(ExpandableObjectConverter))]
        [DisplayName("Table Parameters"), Description("The table parameter values."), Category("Definition"), Id(10, 1)]
        [XmlIgnore]
        public ParametersEditor ParameterValues
        {
            get
            {
                var editor = new ParametersEditor();
                editor.Init(Parameters);
                return editor;
            }
        }
#endif

        /// <summary>
        /// If true, the table must be refreshed for dynamic columns
        /// </summary>
        [DefaultValue(false)]
        public bool MustRefresh { get; set; } = false;
        public bool ShouldSerializeMustRefresh() { return MustRefresh; }

        /// <summary>
        /// SQL Statement executed before the query when the table is involved. The statement may contain Razor script if it starts with '@'.
        /// </summary>
#if WINDOWS
        [Category("SQL"), DisplayName("Pre SQL statement"), Description("SQL Statement executed before the query when the table is involved. The statement may contain Razor script if it starts with '@'."), Id(2, 2)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
#endif
        public string PreSQL { get; set; }

        /// <summary>
        /// SQL Statement executed after the query when the table is involved. The statement may contain Razor script if it starts with '@'.
        /// </summary>
#if WINDOWS
        [Category("SQL"), DisplayName("Post SQL statement"), Description("SQL Statement executed after the query when the table is involved. The statement may contain Razor script if it starts with '@'."), Id(3, 2)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
#endif
        public string PostSQL { get; set; }

        /// <summary>
        /// If true, errors occuring during the Pre or Post SQL statements are ignored and the execution continues
        /// </summary>
#if WINDOWS
        [DefaultValue(false)]
        [Category("SQL"), DisplayName("Ignore Pre and Post SQL errors"), Description("If true, errors occuring during the Pre or Post SQL statements are ignored and the execution continues."), Id(4, 2)]
#endif
        public bool IgnorePrePostError { get; set; } = false;
        public bool ShouldSerializeIgnorePrePostError() { return IgnorePrePostError; }

        /// <summary>
        /// Additional SQL added in the WHERE clause when the table is involved in a query. The text may contain Razor script if it starts with '@'.
        /// </summary>
#if WINDOWS
        [Category("SQL"), DisplayName("Additional WHERE clause"), Description("Additional SQL added in the WHERE clause when the table is involved in a query. The text may contain Razor script if it starts with '@'."), Id(5, 2)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
#endif
        public string WhereSQL { get; set; } = null;

        /// <summary>
        /// List of MetColumn defined for the table
        /// </summary>
        public List<MetaColumn> Columns { get; set; } = new List<MetaColumn>();
        public bool ShouldSerializeColumns() { return Columns.Count > 0; }

        /// <summary>
        /// Alias name of the table
        /// </summary>
        [XmlIgnore]
        public string AliasName
        {
            get
            {
                if ((IsSQL || string.IsNullOrEmpty(_name)) && !string.IsNullOrEmpty(Alias)) return Alias;
                return _name;
            }
        }

        /// <summary>
        /// Name of the DataTable LINQ Result: Source name for SQL, table name for No SQL
        /// </summary>
        [XmlIgnore, Browsable(false)]
        public string LINQResultName
        {
            get
            {
                var sourceName = "";
                if (Source is ReportSource) sourceName = ((ReportSource)Source).MetaSourceName;
                if (string.IsNullOrEmpty(sourceName)) sourceName = Source.Name;

                return Regex.Replace(IsSQL ? sourceName : AliasName, "[^A-Za-z]", "");
            }
        }


        /// <summary>
        /// LINQ expression of the table name
        /// </summary>
        [XmlIgnore, Browsable(false)]
        public string LINQExpressionName
        {
            get
            {
                return string.Format("{0} in model.ExecResultTables[\"{0}\"].AsEnumerable()", LINQResultName);
            }
        }


        /// <summary>
        /// Full SQL name of the table
        /// </summary>
        [XmlIgnore]
        public string FullSQLName
        {
            get
            {
                if (!string.IsNullOrEmpty(Sql))
                {
                    return string.Format("(\r\n{0}\r\n) {1}", Sql, AliasName);
                }
                else if (!string.IsNullOrEmpty(_name) && !string.IsNullOrEmpty(Alias))
                {
                    return string.Format("{0} {1}", _name, Alias);
                }
                return AliasName;
            }
        }

        /// <summary>
        /// Display name including the type
        /// </summary>
        [XmlIgnore]
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Type)) return AliasName;
                return string.Format("{0} ({1})", AliasName, Type);
            }
        }

        /// <summary>
        /// Display name including the type and the source name
        /// </summary>
        [XmlIgnore]
        public string FullDisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Type)) return string.Format("{0}: {1}", Source.Name, AliasName);
                return string.Format("{0}: {1} ({2})", Source.Name, AliasName, Type);
            }
        }

        /// <summary>
        /// True if the table is editable
        /// </summary>
        [XmlIgnore]
        public bool IsEditable = true;

        /// <summary>
        /// True if the source containing the table is a standard SQL source
        /// </summary>
        [XmlIgnore]
        public bool IsSQL
        {
            get { return !Source.IsNoSQL; }
        }

        /// <summary>
        /// True if the table is a sub-table of a model
        /// </summary>
        [XmlIgnore]
        public bool IsSubTable
        {
            get { return Model != null && Model.IsLINQ; }
        }

        /// <summary>
        /// True if the table is for a SQL Model
        /// </summary>
        [XmlIgnore]
        public bool IsForSQLModel
        {
            get { return Model != null && !Model.IsLINQ; }
        }

        /// <summary>
        /// True if the table is for a Mongo DB
        /// </summary>
        public bool IsMongoDb
        {
            get { return TemplateName == MetaTableTemplate.MongoDBName; }
        }

        /// <summary>
        /// True if the table has the same mongo DB connection, database and collection
        /// </summary>
        public bool HasSameMongoCollection(MetaTable table)
        {
            return GetValue(ParameterNameMongoDatabase) == table.GetValue(ParameterNameMongoDatabase)
                && GetValue(ParameterNameMongoCollection) == table.GetValue(ParameterNameMongoCollection)
                && LINQSourceGUID == LINQSourceGUID;
        }

        /// <summary>
        /// Report Model when the MetaTable comes from a SQL Model or when is a SubTable of a LINQ query
        /// </summary>
        [XmlIgnore]
        public ReportModel Model = null;

        /// <summary>
        /// Returns the SQL with the name and the CTE (Common Table Expression)
        /// </summary>
        public void GetExecSQLName(ref string CTE, ref string name)
        {
            CTE = "";
            string sql = "";
            if (Sql != null && Sql.Length > 5 && Sql.ToLower().Trim().StartsWith("with"))
            {
                var startIndex = Sql.IndexOf("(");
                var depth = 1;
                if (startIndex > 0)
                {
                    bool inComment = false, inQuote = false;
                    for (int i = startIndex + 1; i < Sql.Length - 5; i++)
                    {
                        switch (Sql[i])
                        {
                            case ')':
                                if (!inComment && !inQuote)
                                {
                                    depth--;
                                    if (depth == 0)
                                    {
                                        CTE = Sql.Substring(0, i + 1).Trim() + "\r\n";
                                        sql = Sql.Substring(i + 1).Trim();
                                        if (sql.ToLower().StartsWith("select"))
                                        {
                                            //end of CTE
                                            i = Sql.Length;
                                        }
                                    }
                                }
                                break;
                            case '(':
                                if (!inComment && !inQuote)
                                {
                                    depth++;
                                }
                                break;
                            case '\'':
                                inQuote = !inQuote;
                                break;
                            case '/':
                                if (Sql[i + 1] == '*')
                                {
                                    inComment = true;
                                }
                                break;
                            case '*':
                                if (inComment && Sql[i + 1] == '/')
                                {
                                    inComment = false;
                                }
                                break;
                            case '-':
                                if (!inComment && Sql[i + 1] == '-')
                                {
                                    while (i < Sql.Length - 5 && Sql[i] != '\r' && Sql[i] != '\n') i++;
                                }
                                break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(CTE) || string.IsNullOrEmpty(sql))
            {
                name = FullSQLName;
            }
            else
            {
                name = string.Format("(\r\n{0}\r\n) {1}", sql, AliasName); ;
            }
        }

        /// <summary>
        /// Current MetaSource
        /// </summary>
        [XmlIgnore, Browsable(false)]
        public MetaSource Source { get; set; }

        /// <summary>
        /// Source GUID for the LINQ Sub-models
        /// </summary>
        public string LINQSourceGUID
        {
            get
            {
                if (Source is ReportSource)
                {
                    return ((ReportSource)Source).MetaSourceGUID ?? Source.GUID;
                }
                return Source.GUID;
            }
        }

        /// <summary>
        /// Returns the last order to display the columns
        /// </summary>
        public int GetLastDisplayOrder()
        {
            if (Columns.Count > 0) return Columns.Max(i => i.DisplayOrder) + 1;
            return 1;
        }

        /// <summary>
        /// For No SQL Source, build the DataTable from the DefinitionScript, if withLoad is true, the table is then loaded with the LoadScript
        /// </summary>
        public DataTable BuildNoSQLTable(bool withLoad)
        {
            lock (this)
            {
                WithDataLoad = withLoad;

                var definitionInitScript = DefinitionInitScript;
                if (string.IsNullOrEmpty(definitionInitScript)) definitionInitScript = DefaultDefinitionInitScript;
                var definitionScript = DefinitionScript;
                if (string.IsNullOrEmpty(definitionScript)) definitionScript = DefaultDefinitionScript;

                if (!string.IsNullOrEmpty(definitionScript))
                {
                    MongoStages.Clear();
                    if (!string.IsNullOrEmpty(definitionInitScript)) RazorHelper.CompileExecute(definitionInitScript, this);
                    if (withLoad && !string.IsNullOrEmpty(MongoStagesScript)) RazorHelper.CompileExecute(MongoStagesScript, this);

                    RazorHelper.CompileExecute(definitionScript, this);
                    if (withLoad)
                    {
                        var loadScript = LoadScript;
                        if (string.IsNullOrEmpty(loadScript)) loadScript = DefaultLoadScript;
                        if (!string.IsNullOrEmpty(loadScript)) RazorHelper.CompileExecute(loadScript, this);
                    }
                }
                else NoSQLTable = new DataTable(Name);
            }
            return NoSQLTable;
        }

        DataTable GetDefinitionTable(string sql)
        {
            DataTable result = null;
            var finalSQL = sql;
            try
            {
                if (IsSQL)
                {
                    DbConnection connection = (Model != null ? Model.Connection.GetOpenConnection() : Source.GetOpenConnection());

                    Helper.ExecutePrePostSQL(connection, Model == null ? ReportModel.ClearCommonRestrictions(PreSQL) : Model.ParseCommonRestrictions(PreSQL), this, IgnorePrePostError);
                    finalSQL = Model == null ? ReportModel.ClearCommonRestrictions(sql) : Model.ParseCommonRestrictions(sql);
                    result = Helper.GetDataTable(connection, finalSQL);
                    Helper.ExecutePrePostSQL(connection, Model == null ? ReportModel.ClearCommonRestrictions(PostSQL) : Model.ParseCommonRestrictions(PostSQL), this, IgnorePrePostError);
                    connection.Close();
                }
                else
                {
                    result = BuildNoSQLTable(false);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message + "\r\n" + finalSQL);
            }

            return result;
        }

        /// <summary>
        /// Refresh the dynamic columns
        /// </summary>
        public void Refresh()
        {
            if (Source == null || !DynamicColumns) return;

            try
            {
                Information = "";
                Error = "";
                MustRefresh = true;
                //Build table def from SQL or table name

                var sql = "";
                if (IsForSQLModel)
                {
                    if (Model.UseRawSQL) sql = Sql;
                    else sql = string.Format("SELECT * FROM ({0}) a WHERE 1=0", Sql);
                }
                else if (IsSQL)
                {
                    string CTE = "", name = "";
                    GetExecSQLName(ref CTE, ref name);
                    sql = string.Format("{0}SELECT * FROM {1} WHERE 1=0", CTE, name);
                }

                DataTable defTable = GetDefinitionTable(sql);

                int position = 1;
                var sourceAliasName = Source.GetTableName(AliasName);
                foreach (DataColumn column in defTable.Columns)
                {
                    string columnName = (IsSQL ? Source.GetColumnName(column.ColumnName) : column.ColumnName);
                    string fullColumnName = (IsSQL && !IsForSQLModel ? sourceAliasName + "." : "") + columnName;
                    MetaColumn newColumn = Columns.FirstOrDefault(i => i.Name.ToLower() == fullColumnName.ToLower());
                    column.ColumnName = fullColumnName; //Set it here to clear the columns later
                    ColumnType type = Helper.NetTypeConverter(column.DataType);
                    if (newColumn == null)
                    {
                        newColumn = MetaColumn.Create(fullColumnName);
                        newColumn.Source = Source;
                        newColumn.DisplayName = (KeepColumnNames ? column.ColumnName.Trim() : Helper.DBNameToDisplayName(columnName).Trim());
                        newColumn.Category = AliasName;
                        newColumn.DisplayOrder = GetLastDisplayOrder();
                        Columns.Add(newColumn);
                        newColumn.Type = type;
                        newColumn.SetStandardFormat();
                    }
                    newColumn.Source = Source;
                    if (type != newColumn.Type)
                    {
                        newColumn.Type = type;
                        newColumn.SetStandardFormat();
                    }
                    newColumn.DisplayOrder = position++;
                }

                //Clear columns for No SQL or SQL Model
                if (!IsSQL || IsForSQLModel)
                {
                    Columns.RemoveAll(i => !defTable.Columns.Contains(i.Name));
                }

                MustRefresh = false;
                Information = "Dynamic columns have been refreshed successfully";
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Information = "Error got when refreshing dynamic columns.";
            }
            Information = Helper.FormatMessage(Information);
            UpdateEditorAttributes();
        }

        /// <summary>
        /// Sort the table columns either by alphanumeric order or by position
        /// </summary>
        public void SortColumns(bool byPosition)
        {
            if (Source == null) return;

            try
            {
                Information = "";
                Error = "";

                List<string> colNames = new List<string>();

                if (byPosition)
                {
                    DataTable defTable = GetDefinitionTable(string.Format("SELECT * FROM {0} WHERE 1=0", FullSQLName));
                    foreach (DataColumn column in defTable.Columns)
                    {
                        colNames.Add(Source.GetTableName(AliasName) + "." + Source.GetColumnName(column.ColumnName));
                    }
                }
                else
                {
                    foreach (var col in Columns) colNames.Add(col.DisplayName);
                    colNames.Sort();
                }

                foreach (var col in Columns) col.DisplayOrder = -1;

                int position = 1;
                foreach (string columnName in colNames)
                {
                    var cols = Columns.Where(i => (byPosition && i.Name.Trim().ToLower() == columnName.Trim().ToLower()) || (!byPosition && i.DisplayName.Trim().ToLower() == columnName.Trim().ToLower()));
                    foreach (var col in cols) col.DisplayOrder = position++;
                }

                foreach (var col in Columns) if (col.DisplayOrder == -1) col.DisplayOrder = GetLastDisplayOrder();

                Information = "Columns have been sorted by " + (byPosition ? "SQL position" : "Name");
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Information = "Error got when sorting columns.";
            }
            Information = Helper.FormatMessage(Information);
            UpdateEditorAttributes();
        }

        /// <summary>
        /// Reset the Display Order 
        /// </summary>
        public void ResetDisplayOrder()
        {
            if (Source == null) return;
            Information = "";
            Error = "";

            foreach (var col in Columns) col.DisplayOrder = 1;
            Information = "Display Order properties have been reset";
            Information = Helper.FormatMessage(Information);
            UpdateEditorAttributes();
        }

        /// <summary>
        /// Check the table. If a MetaColumn is specified, only the column is checked.
        /// </summary>
        public void CheckTable(MetaColumn column)
        {
            if (Source == null) return;

            Information = "";
            Error = "";
            try
            {
                if (IsSQL)
                {
                    string colNames = "", groupByNames = "";
                    foreach (var col in Columns)
                    {
                        if (column != null && col != column) continue;
                        Helper.AddValue(ref colNames, ",", col.Name);
                        if (!col.IsAggregate) Helper.AddValue(ref groupByNames, ",", col.Name);
                    }
                    if (string.IsNullOrEmpty(colNames)) colNames = "1";

                    string CTE = "", name = "";
                    GetExecSQLName(ref CTE, ref name);
                    string sql = string.Format("{0}SELECT {1} FROM {2} WHERE 1=0", CTE, colNames, name);

                    if (!string.IsNullOrWhiteSpace(WhereSQL))
                    {
                        var where = RazorHelper.CompileExecute(WhereSQL, this);
                        if (!string.IsNullOrWhiteSpace(where)) sql += string.Format("\r\nAND ({0})", RazorHelper.CompileExecute(where, this));
                    }
                    if (Columns.Exists(i => i.IsAggregate) && !string.IsNullOrEmpty(groupByNames))
                    {
                        sql += string.Format("\r\nGROUP BY {0}", groupByNames);
                    }
                    Error = Source.CheckSQL(sql, new List<MetaTable>() { this }, null, false);
                }
                else
                {
                    BuildNoSQLTable(!IsMongoDb);
                }

                if (string.IsNullOrEmpty(Error)) Information = "Table checked successfully";
                else Information = "Error got when checking table.";
                if (column != null)
                {
                    column.Error = Error;
                    if (string.IsNullOrEmpty(column.Error)) column.Information = "Column checked successfully";
                    else column.Information = "Error got when checking column.";
                    column.Information = Helper.FormatMessage(column.Information);
                }
            }
            catch (TemplateCompilationException ex)
            {
                Error = Helper.GetExceptionMessage(ex);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Information = "Error got when checking the table.";
            }
            Information = Helper.FormatMessage(Information);
            UpdateEditorAttributes();
        }

        /// <summary>
        /// Return the values from a column in the table
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public string ShowValues(MetaColumn column)
        {
            string result = "";
            try
            {
                int cnt = 1000;
                if (IsSQL)
                {
                    string sql = $"SELECT {column.Name}, count(0) FROM {FullSQLName} GROUP BY {column.Name} ORDER BY count(0) DESC";
                    result = string.Format("{0}\r\n\r\n{1}:\r\n", sql, column.DisplayName);
                    DbConnection connection = Source.GetOpenConnection();
                    DbCommand command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.CommandTimeout = 20000;
                    var reader = command.ExecuteReader();
                    while (reader.Read() && --cnt >= 0)
                    {
                        string valueStr = "";
                        if (!reader.IsDBNull(0))
                        {
                            object value = reader[0];
                            CultureInfo culture = (Source.Report != null ? Source.Report.ExecutionView.CultureInfo : Source.Repository.CultureInfo);
                            if (value is IFormattable) valueStr = ((IFormattable)value).ToString(column.Format, culture);
                            else valueStr = value.ToString();
                        }
                        result += $"'{valueStr}' : {reader[1]} time(s)\r\n";
                    }
                    reader.Close();
                    command.Connection.Close();
                }
                else
                {
                    result = string.Format("{0}:\r\n", column.DisplayName);
                    DataTable table = BuildNoSQLTable(!IsMongoDb);
                    foreach (DataRow row in table.Rows)
                    {
                        if (--cnt == 0) break;
                        result += string.Format("{0}\r\n", row[column.Name]);
                    }
                }
                if (cnt <= 0) result += "\r\nWarning: Only the first 1000 records are shown.\r\n";
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// Load date time to handle caching for No SQL tables
        /// </summary>
        [XmlIgnore]
        public DateTime LoadDate = DateTime.MinValue;

        /// <summary>
        /// DataTable used for Cache Table (No SQL Source)
        /// </summary>
        [XmlIgnore]
        public DataTable NoSQLCacheTable = null;


        /// <summary>
        /// Set to true if the Load of the table must also include data 
        /// </summary>
        [XmlIgnore]
        public bool WithDataLoad = false;

        #region Helpers

        /// <summary>
        /// Editor Helper: Create or update dynamic columns for this table
        /// </summary>
#if WINDOWS
        [Category("Helpers"), DisplayName("Refresh dynamic columns"), Description("Create or update dynamic columns for this table."), Id(2, 10)]
        [Editor(typeof(HelperEditor), typeof(UITypeEditor))]
#endif
        public string HelperRefreshColumns
        {
            get { return "<Click to refresh dynamic columns>"; }
        }

        /// <summary>
        /// Editor Helper: Check the table definition
        /// </summary>
#if WINDOWS
        [Category("Helpers"), DisplayName("Check table"), Description("Check the table definition."), Id(3, 10)]
        [Editor(typeof(HelperEditor), typeof(UITypeEditor))]
#endif
        public string HelperCheckTable
        {
            get { return IsSQL ? "<Click to check the table in the database>" : "<Click to check the table>"; }
        }

        /// <summary>
        /// Last information message
        /// </summary>
#if WINDOWS
        [Category("Helpers"), DisplayName("Information"), Description("Last information message."), Id(4, 10)]
        [EditorAttribute(typeof(InformationUITypeEditor), typeof(UITypeEditor))]
#endif
        [XmlIgnore]
        public string Information { get; set; }

        /// <summary>
        /// Last error message
        /// </summary>
#if WINDOWS
        [Category("Helpers"), DisplayName("Error"), Description("Last error message."), Id(5, 10)]
        [EditorAttribute(typeof(ErrorUITypeEditor), typeof(UITypeEditor))]
#endif
        [XmlIgnore]
        public string Error { get; set; }

        #endregion



    }
}
