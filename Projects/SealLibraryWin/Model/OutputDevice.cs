﻿//
// Copyright (c) Seal Report (sealreport@gmail.com), http://www.sealreport.org.
// Licensed under the Seal Report Dual-License version 1.0; you may not use this file except in compliance with the License described at https://github.com/ariacom/Seal-Report.
//
using Seal.Helpers;
using System.IO;
using System.Xml.Serialization;

namespace Seal.Model
{
    /// <summary>
    /// Abstract Class to implement an OutputDevice
    /// </summary>
    public abstract class OutputDevice : RootComponent
    {
        /// <summary>
        /// Full name
        /// </summary>
        public virtual string FullName { get; set; }

        /// <summary>
        /// Returns the default processing script for the output device
        /// </summary>
        public abstract string GetProcessingScriptTemplate();

        /// <summary>
        /// Process the report and send it to the device
        /// </summary>
        public abstract void Process(Report report);

        /// <summary>
        /// Validate the device
        /// </summary>
        public abstract void Validate();

        /// <summary>
        /// Current file path
        /// </summary>
        [XmlIgnore]
        public string FilePath;

        /// <summary>
        /// Save to current file
        /// </summary>
        public abstract void SaveToFile();

        /// <summary>
        /// Save the device to a file
        /// </summary>
        public abstract void SaveToFile(string path);

        /// <summary>
        /// Handle the Output ZIP Options: Create a zip file if necessary
        /// </summary>
        /// <param name="report"></param>
        public void HandleZipOptions(Report report)
        {
            ReportOutput output = report.OutputToExecute;
            if (output.ZipResult)
            {
                var zipPath = FileHelper.GetUniqueFileName(Path.Combine(Path.GetDirectoryName(report.ResultFilePath), Path.GetFileNameWithoutExtension(report.ResultFilePath) + ".zip"));
                FileHelper.CreateZIP(report.ResultFilePath, Path.GetFileNameWithoutExtension(report.ResultFileName) + Path.GetExtension(report.ResultFilePath), zipPath, output.ZipPassword);
                report.ResultFilePath = zipPath;
            }
        }

    }
}
