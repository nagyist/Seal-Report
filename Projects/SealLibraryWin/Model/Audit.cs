﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Seal Report Dual-License version 1.0; you may not use this file except in compliance with the License described at https://github.com/ariacom/Seal-Report.
//
using Seal.Helpers;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;

namespace Seal.Model
{

    /// <summary>
    /// Exception that should not be logged or audited
    /// </summary>
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Exception for Login process
    /// </summary>
    public class LoginException : Exception
    {
        public LoginException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Exception for Session Lost
    /// </summary>
    public class SessionLostException : Exception
    {
        public SessionLostException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Class dedicated to log events for audit purpose
    /// </summary>
    public class Audit
    {
        public const string AuditScriptTemplate = @"@using System.Data
@using System.Data.Common
@using System.Data.OleDb
@using System.Data.Odbc

@{
    Audit audit = Model;
    var auditSource = Repository.Instance.Sources.FirstOrDefault(i => i.Name.StartsWith(""Audit""));  
    if (auditSource != null) {
        var helper = new TaskDatabaseHelper();
        var connection = auditSource.Connection.GetOpenConnection();
        try {
            var command = helper.GetDbCommand(connection);

            //Create audit table if necessary
            checkTableCreation(command);
            command.CommandText = @""insert into sr_audit(event_date,event_type,event_path,event_detail,event_error,user_name,user_groups,user_session,execution_name,execution_context,execution_view,execution_duration,output_type,output_name,output_information,schedule_name)"";
            if (command is OleDbCommand || command is OdbcCommand) {
                command.CommandText += "" values(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"";
            }
            else {
                command.CommandText += "" values(@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15,@p16)"";
            }
        
            var date = DateTime.Now;
            date = new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second);
            int index=1;
            addParameter(command, index++, DbType.DateTime, date); //event_date,
            addParameter(command, index++, DbType.AnsiString, audit.Type.ToString()); //event_type,
            addParameter(command, index++, DbType.AnsiString, audit.Path); //event_path,
            addParameter(command, index++, DbType.AnsiString, audit.Detail); //event_detail,
            addParameter(command, index++, DbType.AnsiString, audit.Error); //event_error,
            addParameter(command, index++, DbType.AnsiString, audit.User != null ? audit.User.Name : null); //user_name,
            addParameter(command, index++, DbType.AnsiString, audit.User != null ? audit.User.SecurityGroupsDisplay : null); //user_groups,
            addParameter(command, index++, DbType.AnsiString, audit.User != null ? audit.User.SessionID : null); //user_session,
            addParameter(command, index++, DbType.AnsiString, audit.Report != null ? audit.Report.ExecutionName : null); //execution_name,
            addParameter(command, index++, DbType.AnsiString, audit.Report != null ? audit.Report.ExecutionContext.ToString() : null); //execution_context,
            addParameter(command, index++, DbType.AnsiString, audit.Report != null ? audit.Report.ExecutionView.Name : null); //execution_view,
            addParameter(command, index++, DbType.Int32, audit.Report != null ? Convert.ToInt32(audit.Report.ExecutionFullDuration.TotalSeconds) : (object) DBNull.Value); //execution_duration,
            addParameter(command, index++, DbType.AnsiString, audit.Report != null && audit.Report.OutputToExecute != null ? audit.Report.OutputToExecute.DeviceName : null); //output_type,
            addParameter(command, index++, DbType.AnsiString, audit.Report != null && audit.Report.OutputToExecute != null ? audit.Report.OutputToExecute.Name : null);//output_name,
            addParameter(command, index++, DbType.AnsiString, audit.Report != null && audit.Report.OutputToExecute != null ? audit.Report.OutputToExecute.Information : null);//output_information,
            addParameter(command, index++, DbType.AnsiString, audit.Schedule != null ? audit.Schedule.Name : null);//schedule_name
            command.ExecuteNonQuery();
        }
        finally {
            connection.Close();
        }
    }

}

@functions {
    void checkTableCreation(DbCommand command)
    {
        if (Audit.CheckTableCreation)
        {
            //Check table creation
            Audit.CheckTableCreation = false;
            try
            {
                command.CommandText = ""select 1 from sr_audit where 1=0"";
                command.ExecuteNonQuery();
            }
            catch
            {
                //Create the table (to be adapted for your database type, e.g. ident identity(1,1), execution_error varchar(max) for SQLServer)
                command.CommandText = @""create table sr_audit (
                        event_date datetime,event_type varchar(255),event_path varchar(255),event_detail varchar(255),event_error varchar(255),user_name varchar(255),user_groups varchar(255),user_session varchar(255),execution_name varchar(255),execution_context varchar(255),execution_view varchar(255),execution_status varchar(255),execution_duration int null,execution_locale varchar(255),execution_error varchar(255),output_type varchar(255),output_name varchar(255),output_information varchar(255),schedule_name varchar(255)
                    )"";
                command.ExecuteNonQuery();
            }
        }
    }

    void addParameter(DbCommand command, int index, DbType type, Object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = ""@p"" + index.ToString();
        parameter.DbType = type;
        if (value == null) value = (object) DBNull.Value;

        if (value is string && ((string)value).Length >= 255) parameter.Value = ((string)value).Substring(0, 254);
        else parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
";

        public static bool CheckTableCreation = true;

        public AuditType Type;
        public string Path;
        public string Detail;
        public string Error;
        public SecurityUser User;
        public Report Report;
        public ReportSchedule Schedule;

        static string Key = Guid.NewGuid().ToString();

        /// <summary>
        /// Audit a report execution
        /// </summary>
        public static void LogReportAudit(Report report, ReportSchedule schedule)
        {
            Audit.LogAudit(report.HasErrors ? AuditType.ReportExecutionError : AuditType.ReportExecution, report.SecurityContext, report.FilePath, null, report.ExecutionErrors, report, schedule);
        }

        /// <summary>
        /// Audit an event
        /// </summary>
        public static void LogEventAudit(AuditType type, string detail)
        {
            Audit.LogAudit(type, null, null, detail);
        }

        /// <summary>
        /// Execute the audit script for a given event
        /// </summary>
        public static void LogAudit(AuditType type, SecurityUser user, string path = null, string detail = null, string error = null, Report report = null, ReportSchedule schedule = null)
        {
            try
            {
                if (type == AuditType.LoginFailure && string.IsNullOrEmpty(user.WebUserName)) return; //No audit for this type

                if (Repository.Instance.Configuration.AuditEnabled)
                {
                    var script = Repository.Instance.Configuration.AuditScript;
                    if (string.IsNullOrEmpty(script)) script = AuditScriptTemplate;

                    var audit = new Audit() { Type = type, User = user, Path = path, Detail = detail, Error = error, Report = report, Schedule = schedule };
                    var auditSource = Repository.Instance.Sources.FirstOrDefault(i => i.Name.StartsWith("Audit"));
                    bool lockDatabase = (auditSource != null && auditSource.Connection.ConnectionType == ConnectionType.SQLite);

                    if (lockDatabase)
                    {
                        lock (Key)
                        {
                            ExecuteWithRetry(() => RazorHelper.CompileExecute(script, audit, Key));
                        }
                    }
                    else
                    {
                        RazorHelper.CompileExecute(script, audit, Key);
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.WriteLogEntry("Seal Audit", EventLogEntryType.Error, $"Error executing the Audit Script:\r\n{ex.Message}");
            }
        }

        private static void ExecuteWithRetry(Action action, int maxRetryCount = 3, int delayMilliseconds = 200)
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    action();
                    break;
                }
                catch (SQLiteException ex) when (ex.Message.Contains("database is locked") && retryCount < maxRetryCount)
                {
                    retryCount++;
                    System.Threading.Thread.Sleep(delayMilliseconds);
                }
            }
        }
    }
}