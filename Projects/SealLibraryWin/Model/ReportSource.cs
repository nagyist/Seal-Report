﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Seal Report Dual-License version 1.0; you may not use this file except in compliance with the License described at https://github.com/ariacom/Seal-Report.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.ComponentModel;
#if WINDOWS
using DynamicTypeDescriptor;
#endif

namespace Seal.Model
{
    /// <summary>
    /// A ReportSource is a MetaSource dedicated for report executions
    /// </summary>
    public class ReportSource : MetaSource
    {
        public const string DefaultRepositoryConnectionGUID = "1";
        public const string DefaultReportConnectionGUID = "2";

#if WINDOWS
        #region Editor

        protected override void UpdateEditorAttributes()
        {
            if (_dctd != null)
            {
                //Disable all properties
                foreach (var property in Properties) property.SetIsBrowsable(false);
                //Then enable
                GetProperty("ConnectionGUID").SetIsBrowsable(true);

                GetProperty("MetaSourceName").SetIsBrowsable(!string.IsNullOrEmpty(MetaSourceGUID));

                GetProperty("PreSQL").SetIsBrowsable(!IsNoSQL);
                GetProperty("PostSQL").SetIsBrowsable(!IsNoSQL);
                GetProperty("IgnorePrePostError").SetIsBrowsable(!IsNoSQL);
                GetProperty("IsNoSQL").SetIsBrowsable(true);
                GetProperty("ForceLoad").SetIsBrowsable(true);

                GetProperty("InitScript").SetIsBrowsable(true);
                GetProperty("InitScript").SetIsReadOnly(!string.IsNullOrEmpty(MetaSourceGUID));

                GetProperty("Information").SetIsBrowsable(true);
                GetProperty("Error").SetIsBrowsable(true);
                GetProperty("Information").SetIsReadOnly(true);
                GetProperty("Error").SetIsReadOnly(true);

                GetProperty("PreSQL").SetIsReadOnly(!string.IsNullOrEmpty(MetaSourceGUID));
                GetProperty("PostSQL").SetIsReadOnly(!string.IsNullOrEmpty(MetaSourceGUID));
                GetProperty("IgnorePrePostError").SetIsReadOnly(!string.IsNullOrEmpty(MetaSourceGUID));
                GetProperty("IsNoSQL").SetIsReadOnly(true);

                TypeDescriptor.Refresh(this);
            }
        }
        #endregion

#endif
        /// <summary>
        /// Unique identifier of the source
        /// </summary>
        public string MetaSourceGUID { get; set; }

        private string _metaSourceName;
        /// <summary>
        /// Name
        /// </summary>
#if WINDOWS
        [Category("General"), DisplayName("Repository Data Source"), Description("The name of the repository data source"), Id(1, 1)]
#endif
        [XmlIgnore]
        public string MetaSourceName { 
            get
            {
                if (string.IsNullOrEmpty(_metaSourceName) && !string.IsNullOrEmpty(MetaSourceGUID))
                {
                    var metaSource = Repository.Sources.FirstOrDefault(i => i.GUID == MetaSourceGUID);
                    if (metaSource != null) _metaSourceName = metaSource.Name;
                }
                return _metaSourceName;
            }
        }

        /// <summary>
        /// For performance reasons, the source is not loaded during execution if it is not involved in the report (in models, tasks, etc.). Set this flag to true to force the load anyway and use the source in scripts.
        /// </summary>
#if WINDOWS
        [DefaultValue(false)]
        [Category("General"), DisplayName("Force load"), Description("For performance reasons, the source is not loaded during execution if it is not involved in the report (in models, tasks, etc.). Set this flag to true to force the load anyway and use the source in scripts."), Id(5, 1)]
#endif
        public bool ForceLoad { get; set; } = false;
   //     public bool ShouldSerializeForceLoad() { return !ForceLoad; }

        /// <summary>
        /// Reference to the default repository connection
        /// </summary>
        [XmlIgnore]
        public MetaConnection RepositoryConnection
        {
            get
            {
                MetaConnection result = null;
                if (!string.IsNullOrEmpty(MetaSourceGUID) && Repository != null)
                {
                    MetaSource source = Repository.Sources.FirstOrDefault(i => i.GUID == MetaSourceGUID);
                    if (source != null) result = source.Connection;
                }
                return result;
            }
        }

        /// <summary>
        /// Current connection
        /// </summary>
        [XmlIgnore]
        public override MetaConnection Connection
        {
            get
            {
                if (_connectionGUID == DefaultRepositoryConnectionGUID) return RepositoryConnection;
                return base.Connection;
            }
        }

        /// <summary>
        /// Creates a basic ReportSource
        /// </summary>
        static public ReportSource Create(Repository repository, bool createConnection)
        {
            ReportSource result = new ReportSource() { GUID = Guid.NewGuid().ToString(), Name = "Data Source", Repository = repository };

            if (createConnection) result.AddDefaultConnection(repository);

            return result;
        }

        /// <summary>
        /// True if the source has been initialized from the repository
        /// </summary>
        [XmlIgnore]
        public bool Loaded = false;

        /// <summary>
        /// Load the available MetaSources defined in the repository
        /// </summary>
        public void LoadRepositoryMetaSources(Repository repository)
        {
            if (Loaded) return;

            foreach (var connection in Connections)
            {
                connection.IsEditable = true;
            }
            foreach (var table in MetaData.Tables)
            {
                table.IsEditable = true;
            }
            foreach (var link in MetaData.TableLinks)
            {
                link.IsEditable = true;
            }
            foreach (var join in MetaData.Joins)
            {
                join.IsEditable = true;
            }
            foreach (var itemEnum in MetaData.Enums)
            {
                itemEnum.IsEditable = true;
            }

            if (!string.IsNullOrEmpty(MetaSourceGUID))
            {
                MetaSource source = repository.Sources.FirstOrDefault(i => i.GUID == MetaSourceGUID);
                if (source != null)
                {
                    IsDefault = source.IsDefault;
                    IsNoSQL = source.IsNoSQL;
                    InitScript = source.InitScript;
                    _metaSourceName = source.Name;
                    foreach (var item in source.Connections)
                    {
                        item.Source = source;
                        item.IsEditable = false;
                        Connections.Add(item);
                    }
                    foreach (var item in source.MetaData.Tables)
                    {
                        item.Source = source;
                        item.IsEditable = false;
                        MetaData.Tables.Add(item);
                    }
                    foreach (var item in source.MetaData.TableLinks)
                    {
                        item.IsEditable = false;
                        MetaData.TableLinks.Add(item);
                    }
                    foreach (var item in source.MetaData.Joins)
                    {
                        item.Source = source;
                        item.IsEditable = false;
                        MetaData.Joins.Add(item);
                    }
                    foreach (var item in source.MetaData.Enums)
                    {
                        item.Source = source;
                        item.IsEditable = false;
                        MetaData.Enums.Add(item);
                    }

                    PreSQL = source.PreSQL;
                    PostSQL = source.PostSQL;

                    IgnorePrePostError = source.IgnorePrePostError;
                }
                else
                {
                    Report.LoadErrors += string.Format("Unable to find repository source for '{0}' (GUID {1}). Check the data source files in the repository folder.\r\n", Name, MetaSourceGUID);
                }
            }

            if (Connections.Count == 0)
            {
                Connections.Add(MetaConnection.Create(this));
                ConnectionGUID = Connections[0].GUID;
            }

            Loaded = true;
        }

        /// <summary>
        /// Refresh the enumerated list values
        /// </summary>
        public void RefreshEnumsOnDbConnection()
        {
            foreach (var itemEnum in MetaData.Enums.Where(i => i.IsDbRefresh))
            {
                itemEnum.RefreshEnum();
            }
        }
    }
}

