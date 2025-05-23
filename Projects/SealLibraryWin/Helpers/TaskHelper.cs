﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Seal Report Dual-License version 1.0; you may not use this file except in compliance with the License described at https://github.com/ariacom/Seal-Report.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Seal.Model;
using System.Data;
using System.IO;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Threading;
using System.Data.Common;
using OfficeOpenXml;

namespace Seal.Helpers
{
    public class TaskHelper
    {
        ReportTask _task = null;
        public TaskHelper(ReportTask task)
        {
            _task = task;
        }

        public void LogMessage(string message, params object[] args)
        {
            Log.LogMessage(message, args);
        }

        ReportExecutionLog Log
        {
            get { return _task.Report; }
        }


        public ReportTask Task
        {
            get
            {
                return _task;
            }
        }

        public MetaConnection TaskConnection
        {
            get
            {
                return _task.Connection;
            }
        }

        public DbCommand GetDbCommand()
        {
            return _task.GetDbCommand(_task.Connection);
        }

        TaskDatabaseHelper _databaseHelper = null;
        public TaskDatabaseHelper DatabaseHelper
        {
            get
            {
                if (_databaseHelper == null)
                {
                    _databaseHelper = new TaskDatabaseHelper(); ;
                    _databaseHelper.SetDatabaseDefaultConfiguration(_task.Connection.DatabaseType);
                }
                return _databaseHelper;
            }
        }

        public void RefreshRepositoryEnums(string sourceName = "")
        {
            Repository repository = Repository.Create();
            LogMessage("Starting Refresh Enumerated Lists of all Repository sources.");
            foreach (MetaSource source in repository.Sources.OrderBy(i => i.Name).Where(i => string.IsNullOrEmpty(sourceName) || i.Name.ToLower() == sourceName.ToLower()))
            {
                try
                {
                    LogMessage("Processing data source '{0}'", source.Name);
                    foreach (MetaEnum enumItem in source.MetaData.Enums.Where(i => i.IsDynamic).OrderBy(i => i.Name))
                    {
                        LogMessage("Refreshing Enum '{0}'", enumItem.Name);
                        enumItem.RefreshEnum(false);
                        if (!string.IsNullOrEmpty(enumItem.Error))
                        {
                            LogMessage("ERROR:" + enumItem.Error);
                        }
                    }
                    LogMessage("Saving data source '{0}' in '{1}'\r\n", source.Name, source.FilePath);
                    source.SaveToFile();
                }
                catch (Exception ex)
                {
                    LogMessage("\r\n[UNEXPECTED ERROR RECEIVED]\r\n{0}\r\n", ex.Message);
                }
            }
            LogMessage("Refresh Enumerated Lists terminated\r\n");
        }

        public bool CheckForNewFileSource(string loadFolder, string sourceFilePath)
        {
            bool result = false;
            LogMessage("Checking for new version of '{0}'", sourceFilePath);
            string loadPath = Path.Combine(loadFolder, Path.GetFileName(sourceFilePath));
            if (!Directory.Exists(Path.GetDirectoryName(loadPath))) Directory.CreateDirectory(Path.GetDirectoryName(loadPath));
            if (!File.Exists(sourceFilePath)) throw new Exception(string.Format("Invalid Excel source file '{0}'", sourceFilePath));

            //Check if the file has changed
            if (File.Exists(sourceFilePath) && (!File.Exists(loadPath) || File.GetLastWriteTime(loadPath) < File.GetLastWriteTime(sourceFilePath)))
            {
                LogMessage("File has changed, reload it");
                result = true;
            }
            return result;
        }

        void LogDebug()
        {
            if (DatabaseHelper.DebugLog.Length > 0)
            {
                LogMessage("Debug Log:\r\n{0}", DatabaseHelper.DebugLog.ToString());
                DatabaseHelper.DebugLog = new StringBuilder();
            }
        }


        /// <summary>
        /// Load tables from Excel tabs (one table per tab)
        /// </summary>
        public bool LoadTablesFromExcel(string loadFolder, string sourceExcelPath, string[] sourceTabNames, string[] destTableNames = null, bool useAllConnections = false)
        {
            bool result = false;
            try
            {
                string[] destinationTableNames = (destTableNames == null ? sourceTabNames : destTableNames);
                if (sourceTabNames.Length != destinationTableNames.Length) throw new Exception("The number of Source Tabs number and the number of Destination Tables are different.");
                if (CheckForNewFileSource(loadFolder, sourceExcelPath))
                {
                    for (int i = 0; i < sourceTabNames.Length && !_task.Report.Cancel; i++)
                    {
                        LoadTableFromExcel(sourceExcelPath, sourceTabNames[i], destinationTableNames[i], useAllConnections);
                    }
                    File.Copy(sourceExcelPath, Path.Combine(loadFolder, Path.GetFileName(sourceExcelPath)), true);
                    result = true;
                }
                else
                {
                    LogMessage("No import done");
                }
            }
            finally
            {
                LogDebug();
            }
            return result;
        }


        /// <summary>
        /// Load a table from an Excel tab into the database. A start row, and/or column can be specified. An end column can be specified. 
        /// </summary>
        public bool LoadTableFromExcel(string loadFolder, string sourceExcelPath, string sourceTabName, string destinationTableName, bool useAllConnections = false, int startRow = 1, int startColumn = 1, int endColumnIndex = 0, int endRowIndex = 0, bool hasHeader = true, bool forceLoad = false)
        {
            bool result = false;
            try
            {
                if (!forceLoad)
                {
                    loadFolder = _task.Repository.ReplaceRepositoryKeyword(loadFolder);
                    if (string.IsNullOrEmpty(loadFolder)) loadFolder = "Loaded";
                    if (!Directory.Exists(loadFolder))
                    {
                        loadFolder = Path.Combine(Path.GetDirectoryName(sourceExcelPath), loadFolder);
                        Directory.CreateDirectory(loadFolder);
                    }
                    if (!Directory.Exists(loadFolder)) throw new Exception($"Invalid folder '{loadFolder}'");

                    forceLoad = CheckForNewFileSource(loadFolder, sourceExcelPath);
                }
                else
                {
                    loadFolder = "";
                }

                if (forceLoad)
                {
                    LoadTableFromExcel(sourceExcelPath, sourceTabName, destinationTableName, useAllConnections, startRow, startColumn, endColumnIndex, endRowIndex, hasHeader);
                    if (!string.IsNullOrEmpty(loadFolder)) File.Copy(sourceExcelPath, Path.Combine(loadFolder, Path.GetFileName(sourceExcelPath)), true);
                    result = true;
                }
                else
                {
                    LogMessage("No import done");
                }
            }
            finally
            {
                LogDebug();
            }
            return result;
        }


        public int LoadTable(DataTable table, bool useAllConnections = false)
        {
            var result = 0;
            foreach (var connection in _task.Source.Connections.Where(i => useAllConnections || i.GUID == _task.Connection.GUID))
            {
                if (_task.Report.Cancel) break;
                LogMessage("Importing table for connection '{0}'.", connection.Name);
                DatabaseHelper.SetDatabaseDefaultConfiguration(connection.DatabaseType);
                var dbCommand = _task.GetDbCommand(connection);
                try
                {
                    LogMessage("Dropping and creating table '{0}'", table.TableName);
                    DatabaseHelper.CreateTable(dbCommand, table);
                    LogMessage("Copying {0:N0} rows in '{1}'", table.Rows.Count, table.TableName);
                    DatabaseHelper.InsertTable(dbCommand, table, connection.DateTimeFormat, false);
                    result = table.Rows.Count;
                }
                finally
                {
                    dbCommand.Connection.Close();
                }
            }
            return result;
        }

        public int LoadTableFromExcel(string sourceExcelPath, string sourceTabName, string destinationTableName, bool useAllConnections = false, int startRow = 1, int startColumn = 1, int endColumnIndex = 0, int endRowIndex = 0, bool hasHeader = true)
        {
            int result = 0;
            try
            {
                string sourcePath = _task.Repository.ReplaceRepositoryKeyword(sourceExcelPath);

                if (string.IsNullOrEmpty(sourceTabName) || sourceTabName.Contains("*"))
                {
                    //Load all tabs
                    ExcelPackage package = ExcelHelper.GetExcelPackage(sourcePath);
                    var workbook = package.Workbook;
                    var tabs = (from ws in workbook.Worksheets select ws.Name);
                    foreach (var tab in tabs)
                    {
                        if (
                            string.IsNullOrEmpty(sourceTabName) ||
                            Helper.IsMatchWildcard(tab, sourceTabName)
                        )
                        {
                            var tableName = (destinationTableName ?? "") + tab;
                            LoadTableFromExcel(sourcePath, tab, tableName, useAllConnections, startRow, startColumn, endColumnIndex, endRowIndex, hasHeader);
                        }
                    }
                }
                else
                {
                    LogMessage("Starting Loading Excel Table from '{0}'", sourcePath);
                    DataTable table = DatabaseHelper.LoadDataTableFromExcel(sourcePath, sourceTabName, startRow, startColumn, endColumnIndex, endRowIndex, hasHeader);

                    if (!string.IsNullOrEmpty(destinationTableName)) table.TableName = destinationTableName;
                    result = LoadTable(table, useAllConnections);
                }
            }
            finally
            {
                LogDebug();
            }
            return result;
        }

        /// <summary>
        /// Load all Excel files located in a directory into tables, using the tab name as table name. 
        /// </summary>
        public bool LoadTablesFromExcel(string loadFolder, string sourceExcelDirectory, string searchPattern = "*.xlsx", bool useAllConnections = false)
        {
            bool result = false;
            try
            {
                foreach (var f in Directory.GetFiles(sourceExcelDirectory, searchPattern))
                {
                    if (CheckForNewFileSource(loadFolder, f))
                    {
                        ExcelPackage package = ExcelHelper.GetExcelPackage(f);
                        var workbook = package.Workbook;
                        var tabs = (from ws in workbook.Worksheets select ws.Name);
                        foreach (var tab in tabs)
                        {
                            LoadTableFromExcel(f, tab, tab, useAllConnections);
                        }
                        result = true;
                        File.Copy(f, Path.Combine(loadFolder, Path.GetFileName(f)), true);
                    }
                    else
                    {
                        LogMessage("No import done");
                    }
                }
            }
            finally
            {
                LogDebug();
            }
            return result;
        }

        public bool LoadTableFromCSV(string loadFolder, string sourceCsvPath, string destinationTableName, char? separator = null, bool useAllConnections = false, bool useVBParser = true, Encoding encoding = null, bool forceLoad = false)
        {
            bool result = false;
            try
            {
                if (!forceLoad)
                {
                    loadFolder = _task.Repository.ReplaceRepositoryKeyword(loadFolder);
                    if (string.IsNullOrEmpty(loadFolder)) loadFolder = "Loaded";
                    if (!Directory.Exists(loadFolder))
                    {
                        loadFolder = Path.Combine(Path.GetDirectoryName(sourceCsvPath), loadFolder);
                        Directory.CreateDirectory(loadFolder);
                    }
                    if (!Directory.Exists(loadFolder)) throw new Exception($"Invalid folder '{loadFolder}'");

                    forceLoad = CheckForNewFileSource(loadFolder, sourceCsvPath);
                }
                else
                {
                    loadFolder = "";
                }

                if (forceLoad)
                {
                    LoadTableFromCSV(sourceCsvPath, destinationTableName, separator, useAllConnections, useVBParser, encoding);
                    if (!string.IsNullOrEmpty(loadFolder)) File.Copy(sourceCsvPath, Path.Combine(loadFolder, Path.GetFileName(sourceCsvPath)), true);
                    result = true;
                }
                else
                {
                    LogMessage("No import done");
                }
            }
            finally
            {
                LogDebug();
            }
            return result;
        }

        public void LoadTableFromCSV(string sourceCsvPath, string destinationTableName, char? separator = null, bool useAllConnections = false, bool useVBParser = false, Encoding encoding = null)
        {
            try
            {
                string sourcePath = _task.Repository.ReplaceRepositoryKeyword(sourceCsvPath);
                LogMessage("Starting Loading CSV Table from '{0}'", sourcePath);

                if (string.IsNullOrEmpty(destinationTableName)) destinationTableName = Path.GetFileNameWithoutExtension(sourceCsvPath);

                DataTable table = (!useVBParser ? DatabaseHelper.LoadDataTableFromCSV(sourcePath, separator, encoding) : DatabaseHelper.LoadDataTableFromCSVVBParser(sourcePath, separator, encoding));
                table.TableName = destinationTableName;

                LoadTable(table, useAllConnections);
            }
            finally
            {
                LogDebug();
            }
        }

        public bool LoadTableFromDataSource(string reportSourceName, string sourceSelectStatement, string destinationTableName, bool useAllConnections = false, string sourceCheckSelect = "", string destinationCheckSelect = "")
        {

            var source = _task.Report.Sources.FirstOrDefault(i => i.Name == reportSourceName);
            if (source == null) throw new Exception(string.Format("Invalid report source name: '{0}'", reportSourceName));
            return LoadTableFromExternalSource(source.Connection, sourceSelectStatement, destinationTableName, useAllConnections, sourceCheckSelect, destinationCheckSelect);
        }

        public bool LoadTableFromExternalSource(MetaConnection sourceConnection, string sourceSelectStatement, string destinationTableName, bool useAllConnections = false, string sourceCheckSelect = "", string destinationCheckSelect = "")
        {
            bool result = false;
            try
            {
                LogMessage("Starting Loading Table using '{0}'", sourceSelectStatement);
                DataTable table = null;
                foreach (var connection in _task.Source.Connections.Where(i => useAllConnections || i.GUID == _task.Connection.GUID))
                {
                    if (_task.Report.Cancel) break;
                    LogMessage("Importing table for connection '{0}'.", connection.Name);
                    bool doIt = true;
                    if (!string.IsNullOrEmpty(sourceCheckSelect) && !string.IsNullOrEmpty(destinationCheckSelect))
                    {
                        LogMessage("Checking if load is required using '{0}' and '{1}'", sourceCheckSelect, destinationCheckSelect);
                        doIt = false;
                        try
                        {
                            DataTable checkTable1 = LoadDataTable(sourceConnection, sourceCheckSelect);
                            if (_task.Report.Cancel) break;
                            DataTable checkTable2 = LoadDataTable(connection, destinationCheckSelect);
                            if (_task.Report.Cancel) break;
                            if (!DatabaseHelper.AreTablesIdentical(checkTable1, checkTable2)) doIt = true;
                        }
                        catch
                        {
                            doIt = true;
                        }
                    }

                    if (doIt && !_task.Report.Cancel)
                    {
                        result = true;
                        var sourceSelect = _task.Repository.ReplaceRepositoryKeyword(sourceSelectStatement);
                        if (DatabaseHelper.LoadBurstSize > 0 && !string.IsNullOrEmpty(DatabaseHelper.LoadSortColumn))
                        {
                            //Load big tables...
                            int lastIndex = 0;
                            while (true)
                            {
                                if (_task.Report.Cancel) break;
                                string sql = string.Format("select * from (select ROW_NUMBER() over (order by {0}) rn, a.* from ({1}) a) b where rn > {2} and rn <= {3}", DatabaseHelper.LoadSortColumn, sourceSelect, lastIndex, lastIndex + DatabaseHelper.LoadBurstSize);
                                table = LoadDataTable(sourceConnection, sql);
                                if (table.Rows.Count == 0) break;

                                table.TableName = destinationTableName;
                                var dbCommand = _task.GetDbCommand(connection);
                                try
                                {
                                    if (lastIndex == 0)
                                    {
                                        LogMessage("Dropping and creating table '{1}' in '{0}'", connection.Name, destinationTableName);
                                        DatabaseHelper.SetDatabaseDefaultConfiguration(connection.DatabaseType);
                                        DatabaseHelper.CreateTable(dbCommand, table);
                                    }
                                    LogMessage("Copying {0:N0} rows in '{1}' for index {2} to {3}", table.Rows.Count, destinationTableName, lastIndex, lastIndex + DatabaseHelper.LoadBurstSize);
                                    DatabaseHelper.InsertTable(dbCommand, table, connection.DateTimeFormat, false);
                                    lastIndex += DatabaseHelper.LoadBurstSize;
                                }
                                finally
                                {
                                    dbCommand.Connection.Close();
                                }
                            }
                        }
                        else
                        {

                            if (table == null)
                            {
                                table = LoadDataTable(sourceConnection, sourceSelect);
                                table.TableName = destinationTableName;
                            }

                            var dbCommand = _task.GetDbCommand(connection);
                            try
                            {
                                LogMessage("Dropping and creating table '{1}' in '{0}'", connection.Name, destinationTableName);
                                DatabaseHelper.SetDatabaseDefaultConfiguration(connection.DatabaseType);
                                DatabaseHelper.CreateTable(dbCommand, table);
                                LogMessage("Copying {0:N0} rows in '{1}'", table.Rows.Count, destinationTableName);
                                DatabaseHelper.InsertTable(dbCommand, table, connection.DateTimeFormat, false);
                            }
                            finally
                            {
                                dbCommand.Connection.Close();
                            }

                        }
                    }
                    else
                    {
                        LogMessage("No import done");
                    }
                }
            }
            finally
            {
                LogDebug();
            }
            return result;
        }

        public void ExecuteProcess(string path, string arguments = null, string workingDirectory = null)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory,
                    Arguments = arguments
                }
            };
            LogMessage("Executing '{0}'", path);
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            string err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            LogMessage(output);
            if (!string.IsNullOrEmpty(err))
            {
                throw new Exception(err);
            }
        }

        public void ExecuteNonQuery(string sql, bool useAllConnections = false)
        {
            foreach (var connection in _task.Source.Connections.Where(i => useAllConnections || i.GUID == _task.Connection.GUID))
            {
                if (_task.Report.Cancel) break;
                var command = _task.GetDbCommand(connection);
                try
                {
                    command.CommandText = sql;
                    DatabaseHelper.ExecuteCommand(command);
                }
                finally
                {
                    command.Connection.Close();
                }
            }
        }

        public object ExecuteScalar(string sql)
        {
            object result = null;
            var connection = _task.Source.Connections.FirstOrDefault(i => i.GUID == _task.Connection.GUID);
            if (connection != null)
            {
                var command = _task.GetDbCommand(connection);
                try
                {
                    command.CommandText = sql;
                    result = command.ExecuteScalar();
                }
                finally
                {
                    command.Connection.Close();
                }
            }
            return result;
        }

        public List<string> LoadStringList(string sql)
        {
            var connection = _task.Source.Connections.FirstOrDefault(i => i.GUID == _task.Connection.GUID);
            if (connection != null)
            {
                return LoadStringList(connection, sql);
            }
            return null;
        }

        public List<string> LoadStringList(MetaConnection connection, string sql)
        {
            return DatabaseHelper.LoadStringList(connection, sql);
        }

        public DataTable LoadDataTable(string sql)
        {
            var connection = _task.Source.Connections.FirstOrDefault(i => i.GUID == _task.Connection.GUID);
            if (connection != null)
            {
                return LoadDataTable(connection, sql);
            }
            return null;
        }

        public DataTable LoadDataTable(MetaConnection connection, string sql)
        {
            return DatabaseHelper.LoadDataTable(connection, sql);
        }


        public void ExecuteNonQuery(MetaConnection connection, string sql, string commandsSeparator = null)
        {
            DatabaseHelper.ExecuteNonQuery(connection, sql, commandsSeparator);
        }

        public object ExecuteScalar(MetaConnection connection, string sql)
        {
            return DatabaseHelper.ExecuteScalar(connection, sql);
        }


        #region MSSQL

        bool hasStartCommentAtTheEnd(string text)
        {
            int open = text.LastIndexOf("/*");
            int close = text.LastIndexOf("*/");
            if (open >= 0) return (open > close);
            return false;
        }

        bool hasEndCommentAtTheBeginning(string text)
        {
            int open = text.IndexOf("/*");
            int close = text.IndexOf("*/");
            if (close >= 0)
            {
                if (open > 0) return (close < open);
                else return true;
            }
            return false;
        }

        string _mssqlError = "";
        int _mssqlErrorClassLevel = 11;

        /// <summary>
        /// Execute a .sql file
        /// </summary>
        public void ExecuteMSSQLFile(string filePath, bool useAllConnections = false, bool stopOnError = true, int errorClassLevel = 11, int waitCommands = 20, int waitConnections = 100)
        {
            _mssqlError = "";
            _mssqlErrorClassLevel = errorClassLevel;

            LogMessage("Processing file '{0}'", filePath);
            foreach (var connection in _task.Source.Connections.Where(i => useAllConnections || i.GUID == _task.Connection.GUID))
            {
                if (_task.Report.Cancel) break;

                SqlConnection sqlConnection = null;
                Microsoft.Data.SqlClient.SqlConnection msConnection = null;


                string connectionString = connection.FullConnectionString;
                if (connection.ConnectionType == ConnectionType.MSSQLServer)
                {
                    sqlConnection = new SqlConnection(connectionString);
                }
                else if (connection.ConnectionType == ConnectionType.MSSQLServerMicrosoft)
                {
                    msConnection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
                }
                else
                {
                    throw new Exception("Only MSSQL Connections are supported");
                }

                try
                {
                    if (sqlConnection != null)
                    {
                        sqlConnection.FireInfoMessageEventOnUserErrors = true;
                        sqlConnection.InfoMessage += MSSQLConnection_InfoMessage;
                        sqlConnection.Open();
                    }
                    if (msConnection != null)
                    {
                        msConnection.FireInfoMessageEventOnUserErrors = true;
                        msConnection.InfoMessage += MicrosoftMSSQLConnection_InfoMessage;
                        msConnection.Open();
                    }
                    string script = File.ReadAllText(filePath);
                    // split script on GO command
                    IEnumerable<string> commandStrings = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    List<string> commands = new List<string>();
                    string startCmd = "";
                    bool inComment = false;
                    foreach (string commandString in commandStrings)
                    {
                        if (!string.IsNullOrEmpty(commandString.Trim()))
                        {
                            if (!inComment)
                            {
                                if (hasStartCommentAtTheEnd(commandString))
                                {
                                    //start of comment
                                    inComment = true;
                                    startCmd = commandString + "GO";
                                }
                                else
                                {
                                    //normal case
                                    commands.Add(startCmd + commandString);
                                    inComment = false;
                                    startCmd = "";
                                }

                            }
                            else
                            {
                                //in comment
                                if (!hasEndCommentAtTheBeginning(commandString))
                                {
                                    //no end of comment
                                    startCmd += commandString + "GO";
                                }
                                else if (hasEndCommentAtTheBeginning(commandString) && hasStartCommentAtTheEnd(commandString))
                                {
                                    //end of comment, but start again
                                    startCmd += commandString + "GO";
                                }
                                else
                                {
                                    //end of comment
                                    commands.Add(startCmd + commandString);
                                    inComment = false;
                                    startCmd = "";
                                }
                            }
                        }
                    }

                    foreach (string commandString in commands)
                    {
                        if (!string.IsNullOrEmpty(commandString.Trim()))
                        {
                            DateTime startCommand = DateTime.Now;
                            if (sqlConnection != null)
                            {
                                using (var command = new SqlCommand("", sqlConnection))
                                {
                                    command.CommandTimeout = DatabaseHelper.ExecuteTimeout;
                                    command.CommandText = commandString;
                                    command.ExecuteNonQuery();
                                }
                            }
                            if (msConnection != null)
                            {
                                using (var command = new Microsoft.Data.SqlClient.SqlCommand("", msConnection))
                                {
                                    command.CommandTimeout = DatabaseHelper.ExecuteTimeout;
                                    command.CommandText = commandString;
                                    command.ExecuteNonQuery();
                                }
                            }
                            Thread.Sleep(waitCommands);
                            if (!string.IsNullOrEmpty(_mssqlError) && stopOnError) throw new Exception(_mssqlError);
                        }
                    }
                    Thread.Sleep(waitConnections);
                }
                finally
                {
                    if (sqlConnection != null)
                    {
                        sqlConnection.Close();
                    }
                    if (msConnection != null)
                    {
                        msConnection.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Execute .sql files located in a directory
        /// </summary>
        public void ExecuteMSSQLScripts(string scriptsDirectory, bool useAllConnections = false, bool stopOnError = true, int errorClassLevel = 11, string fileNameFilter = "*.sql", int waitCommands = 20, int waitConnections = 100, int waitFiles = 10)
        {
            var files = Directory.GetFiles(scriptsDirectory, fileNameFilter);
            foreach (var f in files.OrderBy(i => i))
            {
                ExecuteMSSQLFile(f, useAllConnections, stopOnError, errorClassLevel, waitCommands, waitConnections);
                Thread.Sleep(waitFiles);
            }
            LogMessage("File execution terminated.");
        }


        public void CreateMSSQLIndex(string tableName, string colNames, bool isCluster = false)
        {
            var indexName = string.Format("IX_{0}_{1}", tableName, colNames.Replace(",", "_"));
            LogMessage("Creating Index for {0}({1})", tableName, colNames);

            string sql = @"
IF EXISTS (SELECT * FROM sysindexes WHERE name = '{0}') 
DROP INDEX {0} on {1};
CREATE {3} INDEX {0} ON {1}({2})
";
            ExecuteNonQuery(string.Format(sql, indexName, tableName, colNames, isCluster ? "CLUSTERED" : "NONCLUSTERED"));
        }

        void MSSQLConnection_InfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            foreach (SqlError err in e.Errors)
            {
                if (err.Class >= _mssqlErrorClassLevel)
                {
                    _mssqlError = e.Message;
                    break;
                }
            }
            LogMessage(e.Message);
            Thread.Sleep(20);
        }

        void MicrosoftMSSQLConnection_InfoMessage(object sender, Microsoft.Data.SqlClient.SqlInfoMessageEventArgs e)
        {
            foreach (Microsoft.Data.SqlClient.SqlError err in e.Errors)
            {
                if (err.Class >= _mssqlErrorClassLevel)
                {
                    _mssqlError = e.Message;
                    break;
                }
            }
            LogMessage(e.Message);
            Thread.Sleep(20);
        }

        #endregion


        //SANDBOX !
        //Just use this to code, compile and debug your Razor Script within Visual Studio...
        //When OK, just cut and paste it into the Script of your Task using the Report Designer
        public void DesignMyRazorScript()
        {
            DataTable table = new DataTable();
            table.Columns.Add(new DataColumn("Id", typeof(string)));
            table.Columns.Add(new DataColumn("Date", typeof(DateTime)));
            table.Columns.Add(new DataColumn("Title", typeof(string)));
            table.Columns.Add(new DataColumn("Summary", typeof(string)));
            table.Columns.Add(new DataColumn("Link", typeof(string)));
            table.Columns.Add(new DataColumn("Categories", typeof(string)));

            var task = this._task;
            TaskHelper helper = this;
            ReportExecutionLog log = task.Report;
            //Just replace helper.DesignMyRazorScript(); with the code below            


            var reader = System.Xml.XmlReader.Create("http://msdn.microsoft.com/en-us/subscriptions/subscription-downloads.rss");
            var feed = System.ServiceModel.Syndication.SyndicationFeed.Load(reader);
            foreach (var item in feed.Items)
            {
                string link = item.Links.Count > 0 ? item.Links[0].Uri.AbsoluteUri : "";
                string categories = "";
                foreach (var category in item.Categories)
                {
                    categories += category.Name + ";";
                }
                table.Rows.Add(item.Id, item.LastUpdatedTime.DateTime, item.Title.Text, item.Summary.Text, link, categories);
            }


            foreach (var path in File.ReadAllLines(@"c:\temp\test.sql"))
            {
                var newPath = path.Replace("@", "").Replace("\"", "").Replace(";", "");
                var command = task.GetDbCommand(task.Connection);
                try
                {
                    command.CommandText = File.ReadAllText(newPath);
                    command.ExecuteScalar();
                }
                finally
                {
                    command.Connection.Close();
                }
            }
        }
    }

}
