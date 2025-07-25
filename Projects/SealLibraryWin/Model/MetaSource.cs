﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Seal Report Dual-License version 1.0; you may not use this file except in compliance with the License described at https://github.com/ariacom/Seal-Report.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Data;
using System.ComponentModel;
using System.IO;
using Seal.Helpers;
using System.Data.Common;
using System.Data.Odbc;
using System.Xml;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

using System.Data.OleDb;
using System.Data.SQLite;
using AngleSharp.Text;
using DocumentFormat.OpenXml.Spreadsheet;


#if WINDOWS
using Seal.Forms;
using System.Drawing.Design;
using DynamicTypeDescriptor;
using System.ComponentModel.Design;
#endif

namespace Seal.Model
{
    /// <summary>
    /// A MetaSource contains a list of MetaConnection and a MetaData
    /// </summary>
    public class MetaSource : ReportComponent
    {
        const string DefaultConnectionString = "Provider=SQLOLEDB;data source=localhost;initial catalog=adb;Integrated Security=SSPI;";

        /// <summary>
        /// Current file path of the source
        /// </summary>
        [XmlIgnore]
        public string FilePath;

        /// <summary>
        /// Current repository
        /// </summary>
        [XmlIgnore]
        public Repository Repository = null;

#if WINDOWS
        #region Editor

        protected override void UpdateEditorAttributes()
        {
            if (_dctd != null)
            {
                //Disable all properties
                foreach (var property in Properties) property.SetIsBrowsable(false);
                //Then enable
                GetProperty("Description").SetIsBrowsable(true);
                GetProperty("ConnectionGUID").SetIsBrowsable(true);
                GetProperty("PreSQL").SetIsBrowsable(!IsNoSQL);
                GetProperty("PostSQL").SetIsBrowsable(!IsNoSQL);
                GetProperty("IgnorePrePostError").SetIsBrowsable(!IsNoSQL);
                GetProperty("IsDefault").SetIsBrowsable(true);
                GetProperty("ExternalConnections").SetIsBrowsable(true);
                GetProperty("DataSourceReferences").SetIsBrowsable(true);
                GetProperty("IsNoSQL").SetIsBrowsable(true);

                GetProperty("InitScript").SetIsBrowsable(true);

                GetProperty("Information").SetIsBrowsable(true);
                GetProperty("Error").SetIsBrowsable(true);
                GetProperty("Information").SetIsReadOnly(true);
                GetProperty("Error").SetIsReadOnly(true);

                GetProperty("IsNoSQL").SetIsReadOnly(true);

                TypeDescriptor.Refresh(this);
            }
        }

        #endregion

#endif
        /// <summary>
        /// List of MetaConnection
        /// </summary>
        public List<MetaConnection> Connections { get; set; } = new List<MetaConnection>();
        public bool ShouldSerializeConnections() { return Connections.Count > 0; }


        /// <summary>
        /// If true, this source is used as default when a new model is created in a report
        /// </summary>
#if WINDOWS
        [Category("General"), DisplayName("Description"), Description("Description of the data source."), Id(1, 1)]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
#endif
        public string Description { get; set; }


        protected string _connectionGUID;
        /// <summary>
        /// The connection currently used for this data source
        /// </summary>
#if WINDOWS
        [DefaultValue(null)]
        [Category("General"), DisplayName("Current connection"), Description("The connection currently used for this data source"), Id(2, 1)]
        [TypeConverter(typeof(SourceConnectionConverter))]
#endif
        public string ConnectionGUID
        {
            get { return _connectionGUID; }
            set { _connectionGUID = value; }
        }

        /// <summary>
        /// If true, this source is used as default when a new model is created in a report
        /// </summary>
#if WINDOWS
        [DefaultValue(false)]
        [Category("General"), DisplayName("Is Default"), Description("If true, this source is used as default when a new model is created in a report."), Id(3, 1)]
#endif
        public bool IsDefault { get; set; } = false;
        public bool ShouldSerializeIsDefault() { return IsDefault; }

        /// <summary>
        /// If true, the connections are saved in a XML file located beside the Data Source file.
        /// </summary>
#if WINDOWS
        [DefaultValue(false)]
        [Category("General"), DisplayName("Store Connections in a dedicated file"), Description("If true, the connections are saved in a XML file located beside the Data Source file. This may be useful for deployment."), Id(4, 1)]
#endif
        public bool ExternalConnections { get; set; } = false;
        public bool ShouldSerializeExternalConnections() { return ExternalConnections; }

        /// <summary>
        /// Defines other reference Data Sources loaded with the Data Source.
        /// </summary>
#if WINDOWS
        [Category("General"), DisplayName("Data Source References"), Description("Defines other reference Data Sources loaded with the Data Source."), Id(5, 1)]
        [Editor(typeof(DataSourcesSelector), typeof(UITypeEditor))]
#endif
        public List<string> DataSourceReferences { get; set; } = new List<string>();
        public bool ShouldSerializeDataSourceReferences() { return DataSourceReferences.Count > 0; }

        /// <summary>
        /// If true, this source contains only tables built from dedicated Razor Scripts (one for the definition and one for the load). The a LINQ query will then be used to fill the models.
        /// </summary>
#if WINDOWS
        [DefaultValue(false)]
        [Category("General"), DisplayName("Is LINQ"), Description("If true, this source contains only tables built from dedicated Razor Scripts (one for the definition and one for the load). The a LINQ query will then be used to fill the models."), Id(6, 1)]
#endif
        public bool IsNoSQL { get; set; } = false;
        public bool ShouldSerializeIsNoSQL() { return IsNoSQL; }

        /// <summary>
        /// If true, this source contains only a table built from a database. The SQL engine will be used to fill the models.
        /// </summary>
        [XmlIgnore]
        public bool IsSQL { get { return !IsNoSQL; } }

        /// <summary>
        /// If set, the script is executed when a report is initialized for an execution. This may be useful to change dynamically components of the source (e.g. modifying connections, tables, columns, enums, etc.).
        /// </summary>
#if WINDOWS
        [Category("Scripts"), DisplayName("Report Execution Init Script"), Description("If set, the script is executed when a report is initialized for an execution. This may be useful to change dynamically components of the source (e.g. modifying connections, tables, columns, enums, etc.)."), Id(4, 3)]
        [Editor(typeof(TemplateTextEditor), typeof(UITypeEditor))]
#endif
        public string InitScript { get; set; } = "";
        public bool ShouldSerializeInitScript() { return !string.IsNullOrEmpty(InitScript); }

        /// <summary>
        /// SQL Statement executed after the connection is open and before the query is executed. The statement may contain Razor script if it starts with '@'.
        /// </summary>
#if WINDOWS
        [Category("SQL"), DisplayName("Pre SQL Statement"), Description("SQL Statement executed after the connection is open and before the query is executed. The statement may contain Razor script if it starts with '@'."), Id(5, 4)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
#endif
        public string PreSQL { get; set; }
        public bool ShouldSerializePreSQL() { return !string.IsNullOrEmpty(PreSQL); }

        /// <summary>
        /// SQL Statement executed before the connection is closed and after the query is executed. The statement may contain Razor script if it starts with '@'.
        /// </summary>
#if WINDOWS
        [Category("SQL"), DisplayName("Post SQL Statement"), Description("SQL Statement executed before the connection is closed and after the query is executed. The statement may contain Razor script if it starts with '@'."), Id(6, 4)]
        [Editor(typeof(SQLEditor), typeof(UITypeEditor))]
#endif
        public string PostSQL { get; set; }
        public bool ShouldSerializePostSQL() { return !string.IsNullOrEmpty(PostSQL); }

        /// <summary>
        /// If true, errors occuring during the Pre or Post SQL statements are ignored and the execution continues
        /// </summary>
#if WINDOWS
        [DefaultValue(false)]
        [Category("SQL"), DisplayName("Ignore Pre and Post SQL Errors"), Description("If true, errors occuring during the Pre or Post SQL statements are ignored and the execution continues."), Id(7, 4)]
#endif
        public bool IgnorePrePostError { get; set; } = false;
        public bool ShouldSerializeIgnorePrePostError() { return IgnorePrePostError; }

        /// <summary>
        /// Meta information that can be used for any purpose
        /// </summary>
        public List<StringPair> MetaInfo { get; set; } = new List<StringPair>();
        public bool ShouldSerializeMetaInfo() { return MetaInfo.Count > 0; }

        /// <summary>
        /// Get a meta information value from its key
        /// </summary>
        public string GetMetaInfo(string key)
        {
            return MetaInfo.FirstOrDefault(i => i.Key == key)?.Value;
        }

        /// <summary>
        /// Set a meta information value from its key
        /// </summary>
        public void SetMetaInfo(string key, string value)
        {
            var info = MetaInfo.FirstOrDefault(i => i.Key == key);
            if (info == null)
            {
                info = new StringPair() { Key = key };
                MetaInfo.Add(info);
            }
            info.Value = value;
        }

        /// <summary>
        /// Last modification Date Time
        /// </summary>
        [XmlIgnore]
        public DateTime LastModification;

        /// <summary>
        /// Last modification Date Time of the Metadata (used for optimization)
        /// </summary>
        [XmlIgnore]
        public DateTime LastMetadataModification = DateTime.Now;

        /// <summary>
        /// Current MetaConnection
        /// </summary>
        [XmlIgnore]
        public virtual MetaConnection Connection
        {
            get
            {
                if (Connections.Count == 0)
                {
                    //add a connection
                    AddConnection();
                }

                MetaConnection result = Connections.FirstOrDefault(i => i.GUID == _connectionGUID);
                if (result == null && Connections.Count > 0 && _connectionGUID != ReportSource.DefaultRepositoryConnectionGUID)
                {
                    result = Connections[0];
                    _connectionGUID = result.GUID;
                }
                return result;
            }
        }

        /// <summary>
        /// Object that can be used at run-time for any purpose
        /// </summary>
        [XmlIgnore]
        public object Tag;

#if WINDOWS
        [XmlIgnore]
        public ConnectionFolder EditorConnectionFolder = new ConnectionFolder();
        [XmlIgnore]
        public TableFolder EditorTableFolder = new TableFolder();
        [XmlIgnore]
        public TableLinkFolder EditorTableLinkFolder = new TableLinkFolder();
#endif

        MetaData _metaData = null;
        /// <summary>
        /// MetaData contained in the source 
        /// </summary>
        public MetaData MetaData
        {
            get
            {
                if (_metaData == null) Refresh();
                return _metaData;
            }
            set { _metaData = value; }
        }

        /// <summary>
        /// Create a MetaConnection in the source 
        /// </summary>
        public MetaConnection AddConnection()
        {
            MetaConnection result = MetaConnection.Create(this);
            result.ConnectionString = DefaultConnectionString;
            result.DatabaseType = Helper.GetDatabaseType(result.ConnectionString);

            result.Name = Helper.GetUniqueName(result.Name, (from i in Connections select i.Name).ToList());
            Connections.Add(result);
            LastMetadataModification = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Remove a MetaConnection from the source
        /// </summary>
        public void RemoveConnection(MetaConnection item)
        {
            if (Connection == item) throw new Exception("This connection is used as the current connection and cannot be removed.");
            Connections.Remove(item);
            LastMetadataModification = DateTime.Now;
        }

        /// <summary>
        /// Add a MetaTable in the source
        /// </summary>
        public MetaTable AddTable(bool forReport)
        {
            MetaTable result = MetaTable.Create();
            result.Name = "NewTable";
            result.DynamicColumns = forReport || IsNoSQL;
            result.Source = this;
            result.Name = Helper.GetUniqueName(result.Name, (from i in MetaData.Tables select i.Name).ToList());
            MetaData.Tables.Add(result);
            LastMetadataModification = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Remove a MetaTable from the source
        /// </summary>
        public void RemoveTable(MetaTable item)
        {
            //remove joins related
            MetaData.Joins.RemoveAll(i => i.LeftTableGUID == item.GUID || i.RightTableGUID == item.GUID);
            MetaData.Tables.Remove(item);
            LastMetadataModification = DateTime.Now;
        }

        /// <summary>
        /// Remove a MetaTableLink from the source
        /// </summary>
        public void RemoveTableLink(MetaTableLink item)
        {
            MetaData.TableLinks.Remove(item);
            LastMetadataModification = DateTime.Now;
        }

        /// <summary>
        /// Add a MetaColumn in a MetaTable
        /// </summary>
        public MetaColumn AddColumn(MetaTable table)
        {
            MetaColumn result = MetaColumn.Create("ColumnName");
            result.Source = this;
            MetaColumn col = table.Columns.FirstOrDefault();
            if (col != null) result.Category = col.Category;
            else result.Category = !string.IsNullOrEmpty(table.AliasName) ? table.AliasName : Helper.DBNameToDisplayName(table.Name.Trim());
            result.DisplayOrder = table.GetLastDisplayOrder();
            table.Columns.Add(result);
            LastMetadataModification = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Add a MetaJoin to the MetaData
        /// </summary>
        public MetaJoin AddJoin()
        {
            MetaJoin result = MetaJoin.Create();
            result.Name = Repository.JoinAutoName;
            result.Name = Helper.GetUniqueName(result.Name, (from i in MetaData.Joins select i.Name).ToList());
            result.Source = this;
            result.IsBiDirectional = true;
            MetaData.Joins.Add(result);
            LastMetadataModification = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Remove a MetaJoin from the MetaData
        /// </summary>
        public void RemoveJoin(MetaJoin item)
        {
            MetaData.Joins.Remove(item);
            LastMetadataModification = DateTime.Now;
        }

        /// <summary>
        /// Add a MetaEnum to the MetaData
        /// </summary>
        public MetaEnum AddEnum()
        {
            MetaEnum result = MetaEnum.Create();
            result.Name = "Enum";
            result.Name = Helper.GetUniqueName(result.Name, (from i in MetaData.Enums select i.Name).ToList());
            MetaData.Enums.Add(result);
            result.Source = this;
            LastMetadataModification = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Helper to create a MetaEnum for a given MetaColumn
        /// </summary>
        public MetaEnum CreateEnumFromColumn(MetaColumn column)
        {
            MetaEnum result = AddEnum();
            result.IsEditable = true;
            result.Name = column.DisplayName;
            result.IsDynamic = true;
            if (!IsNoSQL)
            {
                result.Sql = string.Format("SELECT DISTINCT \r\n{0} \r\nFROM {1} \r\nORDER BY 1", column.Name, column.MetaTable.FullSQLName);
            }
            else
            {
                result.Script = @"@using System.Data
@{
    MetaEnum enumList = Model;
    MetaSource source = enumList.Source;
    MetaTable table = source.MetaData.Tables.FirstOrDefault(i => i.Name == TableName);
    if (table != null)
    {
        DataTable dataTable = table.BuildNoSQLTable(true);
        enumList.Values.Clear();
        foreach (DataRow val in dataTable.Rows)
        {
            if (!enumList.Values.Exists(i => i.Id == val[ColumnName].ToString()))
            {
                enumList.Values.Add(new MetaEV() { Id = val[ColumnName].ToString() });
            }
        }
    }
}
";
                result.Script = result.Script.Replace("TableName", Helper.QuoteDouble(column.MetaTable.Name));
                result.Script = result.Script.Replace("ColumnName", Helper.QuoteDouble(column.Name));
            }
            result.RefreshEnum();
            return result;
        }

        /// <summary>
        /// Remove a MetaEnum from the MetaData
        /// </summary>
        public void RemoveEnum(MetaEnum item)
        {
            //Clean up enum references from columns
            foreach (MetaTable table in MetaData.Tables)
            {
                foreach (MetaColumn column in table.Columns.Where(i => i.EnumGUID == item.GUID))
                {
                    column.EnumGUID = "";
                }
            }
            MetaData.Enums.Remove(item);
            LastMetadataModification = DateTime.Now;
        }

        /// <summary>
        /// Init all object references
        /// </summary>
        public void InitReferences(Repository repository)
        {
            Repository = repository;

            LoadDatasourceReferences();

            //init references in objects
            foreach (var connection in Connections)
            {
                connection.Source = this;
            }
            foreach (var table in MetaData.Tables)
            {
                table.Source = this;
                foreach (var column in table.Columns)
                {
                    column.Source = this;
                }
                table.InitParameters();
            }
            foreach (var link in MetaData.TableLinks)
            {
                //First report GUID
                if (Report != null)
                {
                    link.Source = Report.Sources.FirstOrDefault(i => i.GUID == link.SourceGUID);
                }
                if (link.Source == null)
                {
                    link.Source = repository.Sources.FirstOrDefault(i => i.GUID == link.SourceGUID);
                }
            }
            //remove lost links
            MetaData.TableLinks.RemoveAll(i => i.Source == null);

            foreach (var join in MetaData.Joins)
            {
                join.Source = this;
            }
            foreach (var item in MetaData.Enums)
            {
                item.Source = this;
            }
        }

        /// <summary>
        /// Add a default MetaConnection to the source
        /// </summary>
        public void AddDefaultConnection(Repository repository)
        {
            if (Connections.Count == 0)
            {
                //Add default connection
                MetaConnection connection = MetaConnection.Create(this);
                connection.ConnectionString = DefaultConnectionString;
                Connections.Add(connection);
                ConnectionGUID = connection.GUID;
            }
        }

        /// <summary>
        /// Create a basic MetaSource
        /// </summary>
        static public MetaSource Create(Repository repository)
        {
            MetaSource result = new MetaSource() { GUID = Guid.NewGuid().ToString(), Name = "Data Source", Repository = repository };
            result.AddDefaultConnection(repository);
            return result;
        }

        static string GetConnectionsFilePath(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_Connections.xml");
        }

        void LoadDatasourceReferences()
        {
            foreach (var reference in Repository.Sources.Where(i => DataSourceReferences.Contains(i.GUID)))
            {
                //Add connections
                foreach (var connection in reference.Connections)
                {
                    if (Connections.Exists(i => i.GUID == connection.GUID)) continue;

                    connection.IsEditable = false;
                    Connections.Add(connection);
                }
                //Add tables
                foreach (var table in reference.MetaData.Tables)
                {
                    if (MetaData.Tables.Exists(i => i.GUID == table.GUID)) continue;

                    table.IsEditable = false;
                    MetaData.Tables.Add(table);
                }
                //Add joins
                foreach (var itemJoin in reference.MetaData.Joins)
                {
                    if (MetaData.Joins.Exists(i => i.GUID == itemJoin.GUID)) continue;

                    itemJoin.IsEditable = false;
                    MetaData.Joins.Add(itemJoin);
                }
                //Add enums
                foreach (var itemEnum in reference.MetaData.Enums)
                {
                    if (MetaData.Enums.Exists(i => i.GUID == itemEnum.GUID)) continue;

                    itemEnum.IsEditable = false;
                    MetaData.Enums.Add(itemEnum);
                }
            }
        }

        /// <summary>
        /// Load the MetaSource from a file
        /// </summary>
        static public MetaSource LoadFromFile(string path)
        {
            MetaSource result = null;
            try
            {
                path = FileHelper.ConvertOSFilePath(path);
                if (!File.Exists(path)) throw new Exception("File not found: " + path);

                XmlSerializer serializer = new XmlSerializer(typeof(MetaSource));
                using (XmlReader xr = XmlReader.Create(path))
                {
                    result = (MetaSource)serializer.Deserialize(xr);
                    xr.Close();
                }

                var connectionsPath = GetConnectionsFilePath(path);
                if (result.ExternalConnections)
                {
                    if (File.Exists(connectionsPath))
                    {
                        serializer = new XmlSerializer(typeof(List<MetaConnection>));
                        using (XmlReader xr = XmlReader.Create(connectionsPath))
                        {
                            result.Connections = (List<MetaConnection>)serializer.Deserialize(xr);
                            xr.Close();
                        }
                    }
                }

                result.Name = Path.GetFileNameWithoutExtension(path);
                result.FilePath = path;
                result.LastModification = File.GetLastWriteTime(path);
                result.LastMetadataModification = result.LastModification;
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Unable to read the file '{0}'.\r\n{1}", path, ex.Message));
            }
            return result;
        }

        /// <summary>
        /// Save to the current file
        /// </summary>
        public void SaveToFile()
        {
            SaveToFile(FilePath);
        }

        /// <summary>
        /// Save MetaSource to a file
        /// </summary>
        public void SaveToFile(string path)
        {
            //Check last modification
            if (LastModification != DateTime.MinValue && File.Exists(path))
            {
                if (!Helper.AreEqualToSecond(LastModification,File.GetLastWriteTime(path)))
                {
                    throw new Exception("Unable to save the Data Source file. The file has been modified by another user.");
                }
            }

            try
            {
                SetToTempReferences();

                foreach (var table in MetaData.Tables) table.BeforeSerialization();

                Name = Path.GetFileNameWithoutExtension(path);
                Helper.Serialize(path, this);
                FilePath = path;
                LastModification = File.GetLastWriteTime(path);
                LastMetadataModification = LastModification;

                var connectionsPath = GetConnectionsFilePath(path);
                if (ExternalConnections) Helper.Serialize(connectionsPath, Connections);
                else if (File.Exists(connectionsPath)) File.Delete(connectionsPath);
            }
            finally
            {
                foreach (var table in MetaData.Tables) table.AfterSerialization();

                GetFromTempReferences();
            }
        }


        /// <summary>
        /// Refresh all tables having dynamic columns and needed a refresh 
        /// </summary>
        public void Refresh()
        {
            if (_metaData == null) _metaData = new MetaData();
            foreach (var table in _metaData.Tables.Where(i => i.DynamicColumns == true && i.MustRefresh))
            {
                table.Refresh();
            }
        }

        /// <summary>
        /// Check a SQL statement, the check includes also all the Pre/Post SQL statements defined.
        /// </summary>
        public string CheckSQL(string sql, List<MetaTable> tables, ReportModel model, bool isPrePost)
        {
            string result = "", finalSQL = "";
            if (!string.IsNullOrEmpty(sql))
            {
                try
                {
                    DbConnection connection = (model != null ? model.Connection.GetOpenConnection() : GetOpenConnection());
                    Helper.ExecutePrePostSQL(connection, Helper.ClearAllSQLKeywords(PreSQL, model), this, this.IgnorePrePostError);
                    if (tables != null) foreach (var table in tables) Helper.ExecutePrePostSQL(connection, Helper.ClearAllSQLKeywords(table.PreSQL, model), table, table.IgnorePrePostError);
                    if (!isPrePost && model != null) Helper.ExecutePrePostSQL(connection, Helper.ClearAllSQLKeywords(model.PreSQL, model), model, model.IgnorePrePostError);
                    var command = connection.CreateCommand();
                    finalSQL = Helper.ClearAllSQLKeywords(sql, model);
                    command.CommandText = finalSQL;
                    var reader = command.ExecuteReader();
                    reader.Close();
                    if (isPrePost && model != null) Helper.ExecutePrePostSQL(connection, Helper.ClearAllSQLKeywords(model.PostSQL, model), model, model.IgnorePrePostError);
                    if (tables != null) foreach (var table in tables) Helper.ExecutePrePostSQL(connection, Helper.ClearAllSQLKeywords(table.PostSQL, model), table, table.IgnorePrePostError);
                    Helper.ExecutePrePostSQL(connection, Helper.ClearAllSQLKeywords(PostSQL, model), this, this.IgnorePrePostError);
                    command.Connection.Close();
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                    if (!string.IsNullOrEmpty(finalSQL)) result += "\r\nSQL Executed:\r\n" + finalSQL.Replace("\n", "\r\n");
                }
            }

            return result;
        }

        /// <summary>
        /// Check a LINQ statement
        /// </summary>
        public string CheckLINQ(string linq, List<MetaTable> tables, ReportModel model)
        {
            string result = "";
            if (!string.IsNullOrEmpty(linq))
            {
                try
                {
                    RazorHelper.Compile(linq, typeof(MetaJoin), Helper.NewGUID());
                }
                catch (Exception ex)
                {
                    result = ex.Message;
                }
            }

            return result;
        }


        /// <summary>
        /// Returns an open DbConnection
        /// </summary>
        public DbConnection GetOpenConnection()
        {
            if (Connection == null) throw new Exception("No connection defined for this source. Please configure the database connection");
            return Connection.GetOpenConnection();
        }

        /// <summary>
        /// Returns a datatable containing the columns definiton of the table
        /// </summary>
        public DataTable GetColumnsSchemaTable(DbConnection connection, string tableName)
        {
            //handle if table name = dbname.owner.tablename
            string name = tableName.Replace("[", "").Replace("]", "");
            string[] names = name.Split('.');

            DataTable schemaColumns = null;
            if (names.Length == 3) schemaColumns = connection.GetSchema("Columns", names);
            else if (names.Length == 2) schemaColumns = connection.GetSchema("Columns", new string[] { null, names[0], names[1] });
            else schemaColumns = connection.GetSchema("Columns", new string[] { null, null, name });

            if (schemaColumns.Rows.Count == 0)
            {
                name = names.Last();
                //Try on 1 level upper
                schemaColumns = connection.GetSchema("Columns", new string[] { null, name });
            }
            if (schemaColumns.Rows.Count == 0)
            {
                //Try on 1 level upper
                schemaColumns = connection.GetSchema("Columns", new string[] { name });
            }

            return schemaColumns;
        }


        /// <summary>
        /// Fill a list of columns from a table catalog
        /// </summary>
        public void AddColumnsFromCatalog(List<MetaColumn> columns, DbConnection connection, MetaTable table)
        {
            if (table.Name == null) throw new Exception("No table name has been defined...");

            Helper.ExecutePrePostSQL(connection, ReportModel.ClearCommonRestrictions(table.PreSQL), table, table.IgnorePrePostError);
            DataTable schemaColumns = GetColumnsSchemaTable(connection, table.Name);
            Helper.ExecutePrePostSQL(connection, ReportModel.ClearCommonRestrictions(table.PostSQL), table, table.IgnorePrePostError);

            foreach (DataRow row in schemaColumns.Rows)
            {
                try
                {
                    string tableName = (!string.IsNullOrEmpty(table.AliasName) ? table.AliasName : Helper.DBNameToDisplayName(row["TABLE_NAME"].ToString().Trim()));
                    MetaColumn column = MetaColumn.Create(tableName + "." + GetColumnName(row["COLUMN_NAME"].ToString()));
                    column.DisplayName = table.KeepColumnNames ? row["COLUMN_NAME"].ToString().Trim() : Helper.DBNameToDisplayName(row["COLUMN_NAME"].ToString().Trim());
                    column.DisplayOrder = table.GetLastDisplayOrder();

                    MetaColumn col = table.Columns.FirstOrDefault();
                    if (col != null) column.Category = col.Category;
                    else column.Category = !string.IsNullOrEmpty(table.AliasName) ? table.AliasName : Helper.DBNameToDisplayName(table.Name.Trim());
                    column.Source = this;
                    string dbType = "", dataType = "CHAR";
                    if (row.Table.Columns.Contains("TYPE_NAME")) dbType = row["TYPE_NAME"].ToString();
                    if (row.Table.Columns.Contains("DATA_TYPE")) dataType = row["DATA_TYPE"].ToString();
                    else if (row.Table.Columns.Contains("DATATYPE")) dataType = row["DATATYPE"].ToString();

                    if (connection is OdbcConnection) column.Type = Helper.ODBCToNetTypeConverter(dbType);
                    else if (connection is SqlConnection || connection is Microsoft.Data.SqlClient.SqlConnection) column.Type = Helper.ODBCToNetTypeConverter(dataType);
                    else column.Type = Helper.DatabaseToNetTypeConverter(dataType);
                    column.SetStandardFormat();
                    if (!columns.Exists(i => i.Name == column.Name)) columns.Add(column);
                }
                catch (Exception ex)
                {
                    Helper.WriteLogException("AddColumnsFromCatalog", ex);
                }
            }
        }

        /// <summary>
        /// Returns a datatable containing the key definition of the current database
        /// </summary>
        public DataTable GetTableKeysSchemaTable(DbConnection connection)
        {
            DataTable schemaTables = null;
            if (connection is OleDbConnection)
            {
                schemaTables = ((OleDbConnection)connection).GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys, null);
            }
            else if (connection is SqlConnection || connection is Microsoft.Data.SqlClient.SqlConnection)
            {
                //SQLServer
                var sql = @"
SELECT 
    DB_NAME() AS CATALOG_NAME,
    s_pk.name AS PK_TABLE_SCHEMA,
    tp.name AS PK_TABLE_NAME,
    cp.name AS PK_COLUMN_NAME,
    s_fk.name AS FK_TABLE_SCHEMA,
    tr.name AS FK_TABLE_NAME,
    cr.name AS FK_COLUMN_NAME
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc 
    ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables tp 
    ON fkc.referenced_object_id = tp.object_id
JOIN sys.schemas s_pk 
    ON tp.schema_id = s_pk.schema_id
JOIN sys.columns cp 
    ON fkc.referenced_object_id = cp.object_id 
    AND fkc.referenced_column_id = cp.column_id
JOIN sys.tables tr 
    ON fkc.parent_object_id = tr.object_id
JOIN sys.schemas s_fk 
    ON tr.schema_id = s_fk.schema_id
JOIN sys.columns cr 
    ON fkc.parent_object_id = cr.object_id 
    AND fkc.parent_column_id = cr.column_id
";

                DbDataAdapter adapter = null;
                schemaTables = new DataTable();
                if (connection is SqlConnection) adapter = new SqlDataAdapter(sql, (SqlConnection)connection);
                else if (connection is Microsoft.Data.SqlClient.SqlConnection) adapter = new Microsoft.Data.SqlClient.SqlDataAdapter(sql, (Microsoft.Data.SqlClient.SqlConnection)connection);
                adapter.Fill(schemaTables);
            }
            else if (connection is MySqlConnection)
            {
                //MySQL
                var sql = @"SELECT TABLE_SCHEMA as PK_TABLE_SCHEMA, TABLE_SCHEMA as FK_TABLE_SCHEMA, TABLE_NAME AS FK_TABLE_NAME, 
COLUMN_NAME AS FK_COLUMN_NAME, CONSTRAINT_NAME AS ForeignKey, REFERENCED_TABLE_NAME AS PK_TABLE_NAME, REFERENCED_COLUMN_NAME AS PK_COLUMN_NAME
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
WHERE REFERENCED_TABLE_NAME IS NOT NULL";
                schemaTables = new DataTable();
                DbDataAdapter adapter = new MySqlDataAdapter(sql, (MySqlConnection)connection);
                adapter.Fill(schemaTables);
            }
            else if (connection is OracleConnection)
            {
                //Oracle
                var sql = @"SELECT ac1.OWNER as PK_TABLE_SCHEMA, ac1.OWNER as FK_TABLE_SCHEMA, ac1.TABLE_NAME AS FK_TABLE_NAME, 
acc1.COLUMN_NAME AS FK_COLUMN_NAME, ac2.TABLE_NAME AS PK_TABLE_NAME, acc2.COLUMN_NAME AS PK_COLUMN_NAME
FROM ALL_CONSTRAINTS ac1
JOIN ALL_CONS_COLUMNS acc1 ON ac1.CONSTRAINT_NAME = acc1.CONSTRAINT_NAME AND ac1.OWNER = acc1.OWNER
JOIN ALL_CONSTRAINTS ac2 ON ac1.R_CONSTRAINT_NAME = ac2.CONSTRAINT_NAME AND ac1.OWNER = ac2.OWNER
JOIN ALL_CONS_COLUMNS acc2 ON ac2.CONSTRAINT_NAME = acc2.CONSTRAINT_NAME AND ac2.OWNER = acc2.OWNER AND acc1.POSITION = acc2.POSITION
WHERE ac1.CONSTRAINT_TYPE = 'R'
";
                schemaTables = new DataTable();
                DbDataAdapter adapter = new OracleDataAdapter(sql, (OracleConnection)connection);
                adapter.Fill(schemaTables);
            }
            else if (connection is NpgsqlConnection)
            {
                //Postgres
                var sql = @"SELECT
    current_database() AS CATALOG_NAME,
    n_pk.nspname AS PK_TABLE_SCHEMA,
    rel.relname AS PK_TABLE_NAME,
    a.attname AS PK_COLUMN_NAME,
    n_fk.nspname AS FK_TABLE_SCHEMA,
    frel.relname AS FK_TABLE_NAME,
    af.attname AS FK_COLUMN_NAME,
    con.conname AS CONSTRAINT_NAME
FROM
    pg_constraint con
    JOIN pg_class rel ON rel.oid = con.conrelid
    JOIN pg_namespace n_pk ON n_pk.oid = rel.relnamespace
    JOIN pg_attribute a ON a.attrelid = rel.oid AND a.attnum = con.conkey[1]
    JOIN pg_class frel ON frel.oid = con.confrelid
    JOIN pg_namespace n_fk ON n_fk.oid = frel.relnamespace
    JOIN pg_attribute af ON af.attrelid = frel.oid AND af.attnum = con.confkey[1]
WHERE
    con.contype = 'f'
";
                schemaTables = new DataTable();
                DbDataAdapter adapter = new NpgsqlDataAdapter(sql, (NpgsqlConnection)connection);
                adapter.Fill(schemaTables);
            }
            else if (connection is SQLiteConnection)
            {
                //SQLite
                var sql = @"SELECT m.name AS PK_TABLE_NAME, [from] AS PK_COLUMN_NAME, [table] AS FK_TABLE_NAME, [to] AS FK_COLUMN_NAME
FROM sqlite_master m
JOIN pragma_foreign_key_list(m.name) p
WHERE m.type = 'table';
";
                schemaTables = new DataTable();
                DbDataAdapter adapter = new SQLiteDataAdapter(sql, (SQLiteConnection)connection);
                adapter.Fill(schemaTables);
            }

            return schemaTables;
        }

        /// <summary>
        /// Return list of Joins from the catalog
        /// </summary>
        public List<MetaJoin> GetJoinsFromCatalog(DbConnection connection)
        {
            List<MetaJoin> joins = new List<MetaJoin>();
            DataTable schemaTables = GetTableKeysSchemaTable(connection);
            if (schemaTables == null) return joins;

            foreach (DataRow row in schemaTables.Rows)
            {
                string table1Name = GetTableName(row["PK_TABLE_NAME"].ToString()).ToLower();
                string table2Name = GetTableName(row["FK_TABLE_NAME"].ToString()).ToLower();
                MetaTable table1 = MetaData.Tables.FirstOrDefault(i => i.Name.ToLower() == table1Name);
                MetaTable table2 = MetaData.Tables.FirstOrDefault(i => i.Name.ToLower() == table2Name);

                //Try with schema
                var catalog = "";
                if (schemaTables.Columns.Contains("CATALOG_NAME")) catalog = row["CATALOG_NAME"].ToString().ToLower();

                if (table1 == null)
                {
                    string pkschema = "";
                    if (schemaTables.Columns.Contains("PK_TABLE_SCHEMA")) pkschema = row["PK_TABLE_SCHEMA"].ToString().ToLower();
                    else if (schemaTables.Columns.Contains("PK_TABLE_SCHEM")) pkschema = row["PK_TABLE_SCHEM"].ToString().ToLower();
                    if (!string.IsNullOrEmpty(pkschema)) table1 = MetaData.Tables.FirstOrDefault(i => i.Name.ToLower() == pkschema + "." + table1Name);
                    if (table1 == null && !string.IsNullOrEmpty(catalog)) table1 = MetaData.Tables.FirstOrDefault(i => i.Name.ToLower() == catalog + "." + pkschema + "." + table1Name);
                }

                if (table2 == null)
                {
                    string fkschema = "";
                    if (schemaTables.Columns.Contains("FK_TABLE_SCHEMA")) fkschema = row["FK_TABLE_SCHEMA"].ToString().ToLower();
                    else if (schemaTables.Columns.Contains("FK_TABLE_SCHEM")) fkschema = row["FK_TABLE_SCHEM"].ToString().ToLower();
                    if (!string.IsNullOrEmpty(fkschema)) table2 = MetaData.Tables.FirstOrDefault(i => i.Name.ToLower() == fkschema + "." + table2Name);
                    if (table2 == null && !string.IsNullOrEmpty(catalog)) table2 = MetaData.Tables.FirstOrDefault(i => i.Name.ToLower() == catalog + "." + fkschema + "." + table2Name);
                }

                if (table1 != null && table2 != null && table1.Name != table2.Name && !MetaData.Joins.Exists(i => i.LeftTableGUID == table1.GUID && i.RightTableGUID == table2.GUID))
                {
                    MetaJoin join = joins.FirstOrDefault(i => i.LeftTableGUID == table1.GUID && i.RightTableGUID == table2.GUID);
                    if (join == null)
                    {
                        join = MetaJoin.Create();
                        join.Name = table1.Name + " - " + table2.Name;
                        join.LeftTableGUID = table1.GUID;
                        join.RightTableGUID = table2.GUID;
                        join.Source = this;
                        join.IsBiDirectional = true;
                        joins.Add(join);
                    }

                    if (!string.IsNullOrEmpty(join.Clause)) join.Clause += " AND ";
                    join.Clause += string.Format("{0}.{1} = {2}.{3}\r\n", table1.Name, GetColumnName(row["PK_COLUMN_NAME"].ToString()), table2.Name, GetColumnName(row["FK_COLUMN_NAME"].ToString()));
                    join.JoinType = JoinType.Inner;
                }
            }
            return joins;
        }

        /// <summary>
        /// Synchronize joins and tables
        /// </summary>
        public void SyncJoinsAndTables()
        {

            //Joins
            var joins = GetJoinsFromCatalog(Connection.GetOpenConnection());
            foreach (var join in joins.Where(i => MetaData.Tables.Exists(j => j.GUID == i.LeftTableGUID) && MetaData.Tables.Exists(j => j.GUID == i.RightTableGUID)))
            {
                if (!MetaData.Joins.Exists(i =>
                (i.LeftTableGUID == join.LeftTableGUID && i.RightTableGUID == join.RightTableGUID) ||
                (i.LeftTableGUID == join.RightTableGUID && i.RightTableGUID == join.LeftTableGUID)))
                {
                    MetaData.Joins.Add(join);
                }
            }

            MetaData.Joins.RemoveAll(i => !MetaData.Tables.Exists(j => j.GUID == i.LeftTableGUID) || !MetaData.Tables.Exists(j => j.GUID == i.RightTableGUID));
        }

        static string[] MSSQLKeywords = new string[] { "ADD", "EXTERNAL", "PROCEDURE", "ALL", "FETCH", "PUBLIC", "ALTER", "FILE", "RAISERROR", "AND", "FILLFACTOR", "READ", "ANY", "FOR", "READTEXT", "AS", "FOREIGN", "RECONFIGURE", "ASC", "FREETEXT", "REFERENCES", "AUTHORIZATION", "FREETEXTTABLE", "REPLICATION", "BACKUP", "FROM", "RESTORE", "BEGIN", "FULL", "RESTRICT", "BETWEEN", "FUNCTION", "RETURN", "BREAK", "GOTO", "REVERT", "BROWSE", "GRANT", "REVOKE", "BULK", "GROUP", "RIGHT", "BY", "HAVING", "ROLLBACK", "CASCADE", "HOLDLOCK", "ROWCOUNT", "CASE", "IDENTITY", "ROWGUIDCOL", "CHECK", "IDENTITY_INSERT", "RULE", "CHECKPOINT", "IDENTITYCOL", "SAVE", "CLOSE", "IF", "SCHEMA", "CLUSTERED", "IN", "SECURITYAUDIT", "COALESCE", "INDEX", "SELECT", "COLLATE", "INNER", "SEMANTICKEYPHRASETABLE", "COLUMN", "INSERT", "SEMANTICSIMILARITYDETAILSTABLE", "COMMIT", "INTERSECT", "SEMANTICSIMILARITYTABLE", "COMPUTE", "INTO", "SESSION_USER", "CONSTRAINT", "IS", "SET", "CONTAINS", "JOIN", "SETUSER", "CONTAINSTABLE", "KEY", "SHUTDOWN", "CONTINUE", "KILL", "SOME", "CONVERT", "LEFT", "STATISTICS", "CREATE", "LIKE", "SYSTEM_USER", "CROSS", "LINENO", "TABLE", "CURRENT", "LOAD", "TABLESAMPLE", "CURRENT_DATE", "MERGE", "TEXTSIZE", "CURRENT_TIME", "NATIONAL", "THEN", "CURRENT_TIMESTAMP", "NOCHECK", "TO", "CURRENT_USER", "NONCLUSTERED", "TOP", "CURSOR", "NOT", "TRAN", "DATABASE", "NULL", "TRANSACTION", "DBCC", "NULLIF", "TRIGGER", "DEALLOCATE", "OF", "TRUNCATE", "DECLARE", "OFF", "TRY_CONVERT", "DEFAULT", "OFFSETS", "TSEQUAL", "DELETE", "ON", "UNION", "DENY", "OPEN", "UNIQUE", "DESC", "OPENDATASOURCE", "UNPIVOT", "DISK", "OPENQUERY", "UPDATE", "DISTINCT", "OPENROWSET", "UPDATETEXT", "DISTRIBUTED", "OPENXML", "USE", "DOUBLE", "OPTION", "USER", "DROP", "OR", "VALUES", "DUMP", "ORDER", "VARYING", "ELSE", "OUTER", "VIEW", "END", "OVER", "WAITFOR", "ERRLVL", "PERCENT", "WHEN", "ESCAPE", "PIVOT", "WHERE", "EXCEPT", "PLAN", "WHILE", "EXEC", "PRECISION", "WITH", "EXECUTE", "PRIMARY", "WITHIN GROUP", "EXISTS", "PRINT", "WRITETEXT", "EXIT", "PROC" };

        string[] getKeywords(DatabaseType dbType)
        {
            if (dbType == DatabaseType.MSSQLServer) return MSSQLKeywords;
            //TODO for other db types
            return new string[] { "" };
        }

        /// <summary>
        /// Returns a full table name from a raw name
        /// </summary>
        public string GetTableName(string rawName)
        {
            var keywords = getKeywords(Connection.DatabaseType);
            if ((!rawName.StartsWith(Connection.StartDelimiter) && !rawName.EndsWith((Connection.EndDelimiter)) && rawName.IndexOfAny(" '\"-$-".ToCharArray()) != -1)
                || keywords.Contains(rawName.ToUpper()))
                return string.Format("{0}{1}{2}", Connection.StartDelimiter, rawName, Connection.EndDelimiter);
            return rawName;
        }

        /// <summary>
        /// Returns a full column name from a raw name
        /// </summary>
        public string GetColumnName(string rawName)
        {
            var keywords = getKeywords(Connection.DatabaseType);
            if ((!rawName.StartsWith(Connection.StartDelimiter) && !rawName.EndsWith(Connection.EndDelimiter) && rawName.IndexOfAny(" '\"-$-.".ToCharArray()) != -1)
                || keywords.Contains(rawName.ToUpper())
                || (!string.IsNullOrEmpty(rawName) && char.IsDigit(rawName[0]))
                )
                return string.Format("{0}{1}{2}", Connection.StartDelimiter, rawName, Connection.EndDelimiter);
            return rawName;
        }

        //Temporary variables to help for serialization...
        [XmlIgnore]
        public List<MetaConnection> TempConnections = new List<MetaConnection>();
        [XmlIgnore]
        public List<MetaTable> TempTables = new List<MetaTable>();
        [XmlIgnore]
        public List<MetaTableLink> TempLinks = new List<MetaTableLink>();
        [XmlIgnore]
        public List<MetaJoin> TempJoins = new List<MetaJoin>();
        [XmlIgnore]
        public List<MetaEnum> TempEnums = new List<MetaEnum>();

        public void SetToTempReferences()
        {
            TempConnections = Connections.ToList();
            TempTables = MetaData.Tables.ToList();
            TempLinks = MetaData.TableLinks.ToList();
            TempJoins = MetaData.Joins.ToList();
            TempEnums = MetaData.Enums.ToList();
            Connections.RemoveAll(i => !i.IsEditable);
            MetaData.Tables.RemoveAll(i => !i.IsEditable);
            MetaData.TableLinks.RemoveAll(i => !i.IsEditable);
            MetaData.Joins.RemoveAll(i => !i.IsEditable);
            MetaData.Enums.RemoveAll(i => !i.IsEditable);
        }

        public void GetFromTempReferences()
        {
            Connections = TempConnections;
            MetaData.Tables = TempTables;
            MetaData.TableLinks = TempLinks;
            MetaData.Joins = TempJoins;
            MetaData.Enums = TempEnums;
        }

        #region Helpers

        /// <summary>
        /// Fill list of MetaTable from the catalog
        /// </summary>
        public void AddSchemaTables(DataTable schemaTables, List<MetaTable> tables)
        {
            foreach (DataRow row in schemaTables.Rows)
            {
                //if (row["TABLE_TYPE"].ToString() == "SYSTEM TABLE" || row["TABLE_TYPE"].ToString() == "SYSTEM VIEW") continue;
                MetaTable table = MetaTable.Create();
                string schema = "";
                string catalog = "";
                if (schemaTables.Columns.Contains("TABLE_CATALOG")) catalog = row["TABLE_CATALOG"].ToString();
                if (schemaTables.Columns.Contains("TABLE_SCHEMA")) schema = row["TABLE_SCHEMA"].ToString();
                else if (schemaTables.Columns.Contains("TABLE_SCHEM")) schema = row["TABLE_SCHEM"].ToString();

                var tableName = "";
                if (row.Table.Columns.Contains("TABLE_NAME")) tableName = row["TABLE_NAME"].ToString();
                else if (row.Table.Columns.Contains("VIEW_NAME")) tableName = row["VIEW_NAME"].ToString();

                if (!string.IsNullOrEmpty(schema) || !string.IsNullOrEmpty(catalog))
                {
                    table.Name = catalog + "." + schema + "." + GetTableName(tableName);

                }
                else
                {
                    table.Name = GetTableName(tableName);
                }

                if (schemaTables.Columns.Contains("TABLE_TYPE")) table.Type = row["TABLE_TYPE"].ToString();

                table.Source = this;
                var f = tables.FirstOrDefault(i => i.Name == table.Name);

                if (!tables.Exists(i => i.Name == table.Name)) tables.Add(table);
            }
        }

        /// <summary>
        /// Last information message
        /// </summary>
#if WINDOWS
        [Category("Helpers"), DisplayName("Information"), Description("Last information message."), Id(8, 10)]
        [EditorAttribute(typeof(InformationUITypeEditor), typeof(UITypeEditor))]
#endif
        [XmlIgnore]
        public string Information { get; set; }

        /// <summary>
        /// Last error message
        /// </summary>
#if WINDOWS
        [Category("Helpers"), DisplayName("Error"), Description("Last error message."), Id(9, 10)]
        [EditorAttribute(typeof(ErrorUITypeEditor), typeof(UITypeEditor))]
#endif
        [XmlIgnore]
        public string Error { get; set; }

        #endregion

    }
}

